#!/usr/bin/env bats

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

HMAC_SVC="hmac-manager.hmac-system.svc.cluster.local"
ECHO_SVC="echo.default.svc.cluster.local"
SIGN_PORT=9090

sign_request() {
    local method="$1" uri="$2"
    # -f omitted intentionally: sign endpoint is internal and always 200
    curl -s -X POST "http://localhost:${SIGN_PORT}/sign" \
        -H "Content-Type: application/json" \
        -d "{\"Policy\":\"my-policy\",\"Method\":\"${method}\",\"Uri\":\"${uri}\"}"
}

extract() {
    local json="$1" key="$2"
    echo "$json" | python3 -c "import sys,json; print(json.load(sys.stdin)['${key}'])"
}

send_signed() {
    local sign_json="$1" target="${2:-http://${ECHO_SVC}/}" namespace="${3:-default}" pod="${4:-curl}"
    local auth policy nonce date
    auth=$(extract "$sign_json" "Authorization")
    policy=$(extract "$sign_json" "Hmac-Policy")
    nonce=$(extract "$sign_json" "Hmac-Nonce")
    date=$(extract "$sign_json" "Hmac-DateRequested")

    # -s only (no -f): we want the HTTP status code even on 4xx responses
    kubectl exec -n "$namespace" "$pod" -- curl -s -o /dev/null -w "%{http_code}" \
        -H "Authorization: $auth" \
        -H "Hmac-Policy: $policy" \
        -H "Hmac-Nonce: $nonce" \
        -H "Hmac-DateRequested: $date" \
        "$target"
}

create_ns_with_curl() {
    local ns="$1" ambient="${2:-true}"
    kubectl create namespace "$ns" --save-config 2>/dev/null || true
    if [[ "$ambient" == "true" ]]; then
        kubectl label namespace "$ns" istio.io/dataplane-mode=ambient --overwrite
    fi
    kubectl run curl-ext --image=curlimages/curl:latest -n "$ns" \
        --restart=Never -- sleep 3600 2>/dev/null || true
    kubectl wait --for=condition=Ready pod/curl-ext -n "$ns" --timeout=60s
}

delete_ns() {
    kubectl delete namespace "$1" --ignore-not-found --wait=true --timeout=60s
}

# ---------------------------------------------------------------------------
# Lifecycle
# ---------------------------------------------------------------------------

setup_file() {
    # The dev-only /sign helper listens on the sign port (8081), which the
    # Service deliberately does not expose — forward to the pod directly.
    kubectl port-forward deploy/hmac-manager "${SIGN_PORT}:8081" -n hmac-system \
        >/tmp/pf-hmac.log 2>&1 &
    echo "$!" > /tmp/pf-hmac.pid

    # Poll until the sign endpoint is reachable; http_code "000" means the
    # TCP connection was refused (port-forward not yet listening).
    # curl exits 7 on "connection refused"; set -e would kill setup_file on that
    # exit code inside a command substitution, so || true makes the assignment safe.
    for _ in $(seq 1 30); do
        code=$(curl -s --max-time 1 -o /dev/null -w "%{http_code}" \
            -X POST -H "Content-Type: application/json" \
            -d '{}' "http://localhost:${SIGN_PORT}/sign" 2>/dev/null) || true
        if [[ "$code" != "000" ]]; then break; fi
        sleep 1
    done
}

teardown_file() {
    if [[ -f /tmp/pf-hmac.pid ]]; then
        kill "$(cat /tmp/pf-hmac.pid)" 2>/dev/null || true
        rm -f /tmp/pf-hmac.pid
    fi
}

# ---------------------------------------------------------------------------
# Waypoint enforcement
# ---------------------------------------------------------------------------

@test "unsigned request from default namespace returns 403" {
    run kubectl exec -n default curl -- \
        curl -s -o /dev/null -w "%{http_code}" "http://${ECHO_SVC}/"
    [ "$output" = "403" ]
}

@test "signed request from default namespace returns 200" {
    local sign
    sign=$(sign_request "GET" "http://${ECHO_SVC}/")
    run send_signed "$sign"
    [ "$output" = "200" ]
}

@test "replay attack returns 403" {
    local sign
    sign=$(sign_request "GET" "http://${ECHO_SVC}/")

    # First use succeeds
    run send_signed "$sign"
    [ "$output" = "200" ]

    # Reuse of the same nonce is rejected
    run send_signed "$sign"
    [ "$output" = "403" ]
}

@test "wrong policy name returns 403" {
    local sign auth nonce date
    sign=$(sign_request "GET" "http://${ECHO_SVC}/")
    auth=$(extract "$sign" "Authorization")
    nonce=$(extract "$sign" "Hmac-Nonce")
    date=$(extract "$sign" "Hmac-DateRequested")

    run kubectl exec -n default curl -- curl -s -o /dev/null -w "%{http_code}" \
        -H "Authorization: $auth" \
        -H "Hmac-Policy: WrongPolicy" \
        -H "Hmac-Nonce: $nonce" \
        -H "Hmac-DateRequested: $date" \
        "http://${ECHO_SVC}/"
    [ "$output" = "403" ]
}

# ---------------------------------------------------------------------------
# Cross-namespace enforcement
# ---------------------------------------------------------------------------

setup() {
    # Ensure test-ns and external-ns are clean before each cross-namespace test
    case "$BATS_TEST_NAME" in
        *"ambient-enrolled"*|*"non-ambient"*)
            kubectl delete namespace test-ns external-ns --ignore-not-found --wait=true \
                --timeout=60s 2>/dev/null || true
            ;;
    esac
}

@test "unsigned request from ambient-enrolled namespace returns 403" {
    create_ns_with_curl "test-ns" "true"

    run kubectl exec -n test-ns curl-ext -- \
        curl -s -o /dev/null -w "%{http_code}" "http://${ECHO_SVC}/"
    [ "$output" = "403" ]

    delete_ns "test-ns"
}

@test "signed request from ambient-enrolled namespace returns 200" {
    create_ns_with_curl "test-ns" "true"

    local sign
    sign=$(sign_request "GET" "http://${ECHO_SVC}/")
    run send_signed "$sign" "http://${ECHO_SVC}/" "test-ns" "curl-ext"
    [ "$output" = "200" ]

    delete_ns "test-ns"
}

@test "unsigned request from non-ambient namespace bypasses waypoint" {
    create_ns_with_curl "external-ns" "false"

    # Not enrolled in ambient — bypasses the waypoint entirely
    run kubectl exec -n external-ns curl-ext -- \
        curl -s -o /dev/null -w "%{http_code}" "http://${ECHO_SVC}/"
    [ "$output" = "200" ]

    delete_ns "external-ns"
}

# ---------------------------------------------------------------------------
# Ingress gateway enforcement
# ---------------------------------------------------------------------------

setup_ingress() {
    kubectl apply -f - <<'EOF'
apiVersion: gateway.networking.k8s.io/v1
kind: Gateway
metadata:
  name: ingress-gateway
  namespace: default
spec:
  gatewayClassName: istio
  listeners:
  - name: http
    protocol: HTTP
    port: 80
---
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: echo-route
  namespace: default
spec:
  parentRefs:
  - name: ingress-gateway
    namespace: default
  rules:
  - matches:
    - path:
        type: PathPrefix
        value: /
    backendRefs:
    - name: echo
      port: 80
---
apiVersion: security.istio.io/v1
kind: AuthorizationPolicy
metadata:
  name: hmac-manager-ingress-auth
  namespace: default
spec:
  targetRefs:
  - group: gateway.networking.k8s.io
    kind: Gateway
    name: ingress-gateway
  action: CUSTOM
  provider:
    name: hmac-manager
  rules:
  - {}
EOF
    # The Gateway's "Programmed" condition stalls in kind because the istio-class
    # Gateway provisions a LoadBalancer Service that never receives an external
    # address. The tests reach the gateway via port-forward, which only needs the
    # backing deployment — so wait on that instead of the Programmed condition.
    for _ in $(seq 1 30); do
        kubectl get deploy ingress-gateway-istio -n default >/dev/null 2>&1 && break
        sleep 2
    done
    kubectl rollout status deployment/ingress-gateway-istio -n default --timeout=120s

    kubectl port-forward svc/ingress-gateway-istio 8888:80 -n default \
        >/tmp/pf-ingress.log 2>&1 &
    echo "$!" > /tmp/pf-ingress.pid
    sleep 2

    # Wait for the ingress AuthorizationPolicy to take effect before asserting.
    for _ in $(seq 1 30); do
        code=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:8888/" 2>/dev/null || echo "000")
        [[ "$code" == "403" ]] && break
        sleep 2
    done
}

teardown_ingress() {
    if [[ -f /tmp/pf-ingress.pid ]]; then
        kill "$(cat /tmp/pf-ingress.pid)" 2>/dev/null || true
        rm -f /tmp/pf-ingress.pid
    fi
    kubectl delete gateway ingress-gateway -n default --ignore-not-found
    kubectl delete httproute echo-route -n default --ignore-not-found
    kubectl delete authorizationpolicy hmac-manager-ingress-auth -n default --ignore-not-found
}

@test "unsigned request through ingress gateway returns 403" {
    setup_ingress
    run curl -s -o /dev/null -w "%{http_code}" "http://localhost:8888/"
    teardown_ingress
    [ "$output" = "403" ]
}

@test "signed request through ingress gateway returns 200" {
    setup_ingress

    local sign
    sign=$(sign_request "GET" "http://localhost:8888/")
    local auth policy nonce date
    auth=$(extract "$sign" "Authorization")
    policy=$(extract "$sign" "Hmac-Policy")
    nonce=$(extract "$sign" "Hmac-Nonce")
    date=$(extract "$sign" "Hmac-DateRequested")

    run curl -s -o /dev/null -w "%{http_code}" "http://localhost:8888/" \
        -H "Authorization: $auth" \
        -H "Hmac-Policy: $policy" \
        -H "Hmac-Nonce: $nonce" \
        -H "Hmac-DateRequested: $date"
    teardown_ingress
    [ "$output" = "200" ]
}

@test "replay attack through ingress gateway returns 403" {
    setup_ingress

    local sign
    sign=$(sign_request "GET" "http://localhost:8888/")
    local auth policy nonce date
    auth=$(extract "$sign" "Authorization")
    policy=$(extract "$sign" "Hmac-Policy")
    nonce=$(extract "$sign" "Hmac-Nonce")
    date=$(extract "$sign" "Hmac-DateRequested")

    # First use (not via run — we don't need to assert on this)
    curl -s -o /dev/null \
        -H "Authorization: $auth" -H "Hmac-Policy: $policy" \
        -H "Hmac-Nonce: $nonce" -H "Hmac-DateRequested: $date" \
        "http://localhost:8888/"

    run curl -s -o /dev/null -w "%{http_code}" "http://localhost:8888/" \
        -H "Authorization: $auth" -H "Hmac-Policy: $policy" \
        -H "Hmac-Nonce: $nonce" -H "Hmac-DateRequested: $date"
    teardown_ingress
    [ "$output" = "403" ]
}
