#!/usr/bin/env bats

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

HMAC_SVC="hmac-manager.hmac-system.svc.cluster.local"
ECHO_SVC="echo.default.svc.cluster.local"
HMAC_NS="hmac-system"
SIGN_PORT=9090

sign_request() {
    local method="$1" uri="$2" policy="${3:-my-policy}"
    # -f omitted intentionally: we want the response body even when the sign
    # endpoint returns 404 for an unknown policy, which the CRD-lifecycle tests
    # rely on to tell that a policy is no longer loaded.
    curl -s -X POST "http://localhost:${SIGN_PORT}/sign" \
        -H "Content-Type: application/json" \
        -d "{\"Policy\":\"${policy}\",\"Method\":\"${method}\",\"Uri\":\"${uri}\"}"
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

# --- kubectl-managed HmacPolicy helpers (CRD producer path) ----------------

# Apply an HmacPolicy CR directly (the kubectl producer, not Helm). Reuses the
# hmac-manager-policy Secret created at install time for the private key.
apply_policy() {
    local name="$1" pubkey="$2" secret_key="${3:-my-policy-privateKey}"
    kubectl apply -f - <<EOF
apiVersion: hmac-manager.io/v1alpha1
kind: HmacPolicy
metadata:
  name: ${name}
  namespace: ${HMAC_NS}
  labels:
    e2e-test: kubectl-lifecycle
spec:
  publicKey: "${pubkey}"
  privateKeySecretRef:
    name: hmac-manager-policy
    key: ${secret_key}
EOF
}

# True (exit 0) when the operator-owned aggregate ConfigMap lists a policy by name.
aggregate_has_policy() {
    kubectl get configmap hmac-manager-config -n "$HMAC_NS" -o json 2>/dev/null \
        | POLICY="$1" python3 -c '
import os, sys, json
try:
    cm = json.load(sys.stdin)
    cfg = json.loads(cm["data"]["config.json"])
except Exception:
    sys.exit(1)
names = [p.get("Name") for p in cfg.get("HmacManager", [])]
sys.exit(0 if os.environ["POLICY"] in names else 1)
'
}

wait_for_status_phase() {
    local policy="$1" want="$2" timeout="${3:-60}"
    local deadline=$((SECONDS + timeout)) phase
    while (( SECONDS < deadline )); do
        phase=$(kubectl get hmacpolicy "$policy" -n "$HMAC_NS" -o jsonpath='{.status.phase}' 2>/dev/null || true)
        [[ "$phase" == "$want" ]] && return 0
        sleep 2
    done
    return 1
}

wait_for_aggregate_contains() {
    local policy="$1" timeout="${2:-60}"
    local deadline=$((SECONDS + timeout))
    while (( SECONDS < deadline )); do
        aggregate_has_policy "$policy" && return 0
        sleep 2
    done
    return 1
}

wait_for_aggregate_absent() {
    local policy="$1" timeout="${2:-60}"
    local deadline=$((SECONDS + timeout))
    while (( SECONDS < deadline )); do
        aggregate_has_policy "$policy" || return 0
        sleep 2
    done
    return 1
}

# Poll until a request signed with $policy passes ext-authz (200). Proves the
# operator reconciled the CR *and* the server hot-reloaded the aggregate config.
wait_for_signed_200() {
    local policy="$1" timeout="${2:-150}"
    local deadline=$((SECONDS + timeout)) sign code
    while (( SECONDS < deadline )); do
        sign=$(sign_request "GET" "http://${ECHO_SVC}/" "$policy")
        if echo "$sign" | python3 -c "import sys,json; d=json.load(sys.stdin); assert isinstance(d, dict) and 'Authorization' in d" >/dev/null 2>&1; then
            code=$(send_signed "$sign")
            [[ "$code" == "200" ]] && return 0
        fi
        sleep 3
    done
    return 1
}

# Sign a request that carries a scheme's custom header, so the library folds its value into the
# signature. The /sign helper attaches Headers to the message before signing (see SignHandler).
sign_request_scheme() {
    local method="$1" uri="$2" policy="$3" scheme="$4" userid="$5"
    curl -s -X POST "http://localhost:${SIGN_PORT}/sign" \
        -H "Content-Type: application/json" \
        -d "{\"Policy\":\"${policy}\",\"Method\":\"${method}\",\"Uri\":\"${uri}\",\"Scheme\":\"${scheme}\",\"Headers\":{\"X-UserId\":\"${userid}\"}}"
}

# Replay a scheme-signed request from the curl pod, including the Hmac-Scheme and X-UserId headers the
# verifier needs. $2 is the X-UserId value actually sent — pass a different value than was signed to
# simulate tampering.
send_signed_scheme() {
    local sign_json="$1" userid="$2" target="${3:-http://${ECHO_SVC}/}"
    local auth policy scheme nonce date
    auth=$(extract "$sign_json" "Authorization")
    policy=$(extract "$sign_json" "Hmac-Policy")
    scheme=$(extract "$sign_json" "Hmac-Scheme")
    nonce=$(extract "$sign_json" "Hmac-Nonce")
    date=$(extract "$sign_json" "Hmac-DateRequested")
    kubectl exec -n default curl -- curl -s -o /dev/null -w "%{http_code}" \
        -H "Authorization: $auth" \
        -H "Hmac-Policy: $policy" \
        -H "Hmac-Scheme: $scheme" \
        -H "Hmac-Nonce: $nonce" \
        -H "Hmac-DateRequested: $date" \
        -H "X-UserId: $userid" \
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

teardown() {
    # Remove any CRs the kubectl-lifecycle tests created, including after a mid-test
    # failure. Scoped by test name so the other suites are untouched.
    case "$BATS_TEST_NAME" in
        *kubectl*)
            kubectl delete hmacpolicy -n "$HMAC_NS" -l e2e-test=kubectl-lifecycle \
                --ignore-not-found --wait=true 2>/dev/null || true
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

# ---------------------------------------------------------------------------
# kubectl-managed HmacPolicy (CRD producer path + status)
#
# The install-time policy is templated by Helm; these tests exercise the CRD as
# a co-equal producer — creating, patching and deleting an HmacPolicy directly
# with kubectl — and assert both the reconciled .status.phase and that signing
# reflects the change end-to-end (operator reconcile + server hot-reload).
# ---------------------------------------------------------------------------

@test "kubectl-managed HmacPolicy: create reports Ready, enters the aggregate config, signs end-to-end, then delete removes it" {
    local pubkey="00000000-0000-0000-0000-0000000000a1"

    apply_policy "kubectl-policy" "$pubkey"

    run wait_for_status_phase "kubectl-policy" "Ready" 60
    [ "$status" -eq 0 ]

    run wait_for_aggregate_contains "kubectl-policy" 60
    [ "$status" -eq 0 ]

    # A policy added via kubectl after install must become usable without a pod restart.
    run wait_for_signed_200 "kubectl-policy" 180
    [ "$status" -eq 0 ]

    kubectl delete hmacpolicy kubectl-policy -n "$HMAC_NS" --wait=true

    run wait_for_aggregate_absent "kubectl-policy" 60
    [ "$status" -eq 0 ]

    run kubectl get hmacpolicy kubectl-policy -n "$HMAC_NS"
    [ "$status" -ne 0 ]
}

@test "kubectl-managed HmacPolicy: patching to a missing Secret key reports Invalid and drops it from the aggregate" {
    local pubkey="00000000-0000-0000-0000-0000000000a2"

    apply_policy "kubectl-policy-invalid" "$pubkey"

    run wait_for_status_phase "kubectl-policy-invalid" "Ready" 60
    [ "$status" -eq 0 ]
    run wait_for_aggregate_contains "kubectl-policy-invalid" 60
    [ "$status" -eq 0 ]

    # Repoint the key reference at a key that does not exist in the Secret.
    kubectl patch hmacpolicy kubectl-policy-invalid -n "$HMAC_NS" --type merge \
        -p '{"spec":{"privateKeySecretRef":{"name":"hmac-manager-policy","key":"does-not-exist"}}}'

    run wait_for_status_phase "kubectl-policy-invalid" "Invalid" 60
    [ "$status" -eq 0 ]

    run kubectl get hmacpolicy kubectl-policy-invalid -n "$HMAC_NS" -o jsonpath='{.status.message}'
    [ -n "$output" ]

    run wait_for_aggregate_absent "kubectl-policy-invalid" 60
    [ "$status" -eq 0 ]
}

@test "kubectl-managed HmacPolicy: a scheme signs+verifies with its header (200) and rejects a tampered header (403)" {
    kubectl apply -f - <<EOF 2>&1
apiVersion: hmac-manager.io/v1alpha1
kind: HmacPolicy
metadata:
  name: kubectl-scheme-policy
  namespace: $HMAC_NS
  labels: { e2e-test: kubectl-lifecycle }
spec:
  publicKey: "00000000-0000-0000-0000-0000000000d4"
  privateKeySecretRef: { name: hmac-manager-policy, key: my-policy-privateKey }
  schemes:
    - name: UserContext
      headers:
        - name: X-UserId
          claimType: userId
EOF

    run wait_for_status_phase "kubectl-scheme-policy" "Ready" 60
    [ "$status" -eq 0 ]

    # Gate on the policy being live on the running pod: a schemeless sign+verify (200) proves it
    # has reconciled and hot-reloaded before the scheme-specific assertions below.
    run wait_for_signed_200 "kubectl-scheme-policy" 180
    [ "$status" -eq 0 ]

    # Scheme header present and matching what was signed → the folded X-UserId value verifies → 200.
    local sign
    sign=$(sign_request_scheme "GET" "http://${ECHO_SVC}/" "kubectl-scheme-policy" "UserContext" "user-123")
    run send_signed_scheme "$sign" "user-123"
    [ "$output" = "200" ]

    # Re-sign (fresh nonce) but replay with a different X-UserId than was signed → the recomputed
    # signature no longer matches → 403. This proves the scheme header is genuinely in the signature.
    sign=$(sign_request_scheme "GET" "http://${ECHO_SVC}/" "kubectl-scheme-policy" "UserContext" "user-123")
    run send_signed_scheme "$sign" "user-999"
    [ "$output" = "403" ]
}
