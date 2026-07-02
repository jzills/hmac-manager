#!/usr/bin/env bash
set -euo pipefail

ISTIO_VERSION="${ISTIO_VERSION:-1.30.2}"
GATEWAY_API_VERSION="${GATEWAY_API_VERSION:-v1.2.0}"
CLUSTER_NAME="${CLUSTER_NAME:-hmac-manager-e2e}"
IMAGE_TAG="${IMAGE_TAG:-e2e-test}"
NAMESPACE="${NAMESPACE:-hmac-system}"
TEST_PUBLIC_KEY="${TEST_PUBLIC_KEY:-00000000-0000-0000-0000-000000000001}"
TEST_PRIVATE_KEY="${TEST_PRIVATE_KEY:-euJ9iXZfURClr78ERyZr8csmHoVkZIfeoSVu3jNZf0w=}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

log() { echo "[setup] $*"; }

# ── 1. Cluster ────────────────────────────────────────────────────────────────
log "Creating kind cluster '$CLUSTER_NAME'..."
if kind get clusters 2>/dev/null | grep -q "^${CLUSTER_NAME}$"; then
    log "Cluster already exists, skipping creation."
else
    kind create cluster --name "$CLUSTER_NAME" --wait 60s
fi
kubectl config use-context "kind-${CLUSTER_NAME}"

# ── 2. Gateway API CRDs ───────────────────────────────────────────────────────
log "Installing Gateway API CRDs ($GATEWAY_API_VERSION)..."
if [[ -n "${GATEWAY_API_CRD_FILE:-}" && -f "$GATEWAY_API_CRD_FILE" ]]; then
    kubectl apply -f "$GATEWAY_API_CRD_FILE"
else
    kubectl apply -f "https://github.com/kubernetes-sigs/gateway-api/releases/download/${GATEWAY_API_VERSION}/standard-install.yaml"
fi

# ── 3. Istio ──────────────────────────────────────────────────────────────────
log "Installing istioctl $ISTIO_VERSION..."
if ! command -v istioctl &>/dev/null || [[ "$(istioctl version --remote=false 2>/dev/null)" != *"$ISTIO_VERSION"* ]]; then
    curl -sSL https://istio.io/downloadIstio | ISTIO_VERSION="$ISTIO_VERSION" TARGET_ARCH=x86_64 sh -
    export PATH="$PWD/istio-${ISTIO_VERSION}/bin:$PATH"
fi

log "Installing Istio with ambient profile..."
istioctl install --set profile=ambient --skip-confirmation
kubectl rollout status deployment/istiod -n istio-system --timeout=120s
kubectl rollout status daemonset/ztunnel -n istio-system --timeout=120s

# ── 4. Namespace setup ────────────────────────────────────────────────────────
# Only enroll the dataplane here. The istio.io/use-waypoint label is applied by
# `istioctl waypoint apply --enroll-namespace` below; setting it up front (before
# the waypoint Gateway exists) makes the apply report the namespace as already
# enrolled and the waypoint never programs (AuthorizationPolicy stays unbound).
log "Labeling default namespace for ambient mode..."
kubectl label namespace default istio.io/dataplane-mode=ambient --overwrite

# ── 5. Waypoint ───────────────────────────────────────────────────────────────
log "Creating waypoint proxy..."
istioctl waypoint apply --enroll-namespace -n default --wait

log "Waiting for the waypoint Gateway to be programmed and ready..."
kubectl wait --for=condition=Programmed gateway/waypoint -n default --timeout=180s
kubectl rollout status deployment/waypoint -n default --timeout=120s

# ── 6. Test workloads ─────────────────────────────────────────────────────────
log "Deploying echo and curl pods..."
kubectl apply -f "$SCRIPT_DIR/manifests/echo.yaml"
kubectl apply -f "$SCRIPT_DIR/manifests/curl.yaml"
kubectl wait --for=condition=Ready pod/echo pod/curl -n default --timeout=60s

# ── 7. Docker images ──────────────────────────────────────────────────────────
log "Building hmac-manager image ($IMAGE_TAG)..."
docker build -f "$REPO_ROOT/kubernetes/service/Dockerfile" \
    -t "zills/hmac-manager:${IMAGE_TAG}" \
    "$REPO_ROOT"

log "Loading hmac-manager image into kind..."
kind load docker-image "zills/hmac-manager:${IMAGE_TAG}" --name "$CLUSTER_NAME"

log "Building hmac-manager-operator image ($IMAGE_TAG)..."
docker build -f "$REPO_ROOT/kubernetes/operator/Dockerfile" \
    -t "zills/hmac-manager-operator:${IMAGE_TAG}" \
    "$REPO_ROOT"

log "Loading hmac-manager-operator image into kind..."
kind load docker-image "zills/hmac-manager-operator:${IMAGE_TAG}" --name "$CLUSTER_NAME"

log "Pulling and loading bundled Redis image into kind..."
docker pull redis:7-alpine
kind load docker-image redis:7-alpine --name "$CLUSTER_NAME"

# ── 8. Policy secret + Helm install ──────────────────────────────────────────
log "Creating namespace and HMAC policy secret..."
kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -
kubectl create secret generic hmac-manager-policy \
    --namespace "$NAMESPACE" \
    --from-literal="MyPolicy-privateKey=$TEST_PRIVATE_KEY" \
    --dry-run=client -o yaml | kubectl apply -f -

log "Installing hmac-manager Helm chart..."
helm upgrade --install hmac-manager "$REPO_ROOT/kubernetes/chart" \
    --namespace "$NAMESPACE" \
    --set image.tag="$IMAGE_TAG" \
    --set image.pullPolicy=Never \
    --set operator.image.tag="$IMAGE_TAG" \
    --set operator.image.pullPolicy=Never \
    --set environment=Development \
    --set "policies[0].name=MyPolicy" \
    --set "policies[0].publicKey=$TEST_PUBLIC_KEY" \
    --set "policies[0].privateKeySecret.name=hmac-manager-policy" \
    --set "policies[0].privateKeySecret.key=MyPolicy-privateKey" \
    --set istio.ingressGateway.enabled=false \
    --set istio.waypoint.enabled=true \
    --set istio.waypoint.authorizationPolicy.enabled=true \
    --set istio.waypoint.name=waypoint \
    --set istio.waypoint.namespace=default \
    --wait --timeout=180s

# ── 8b. Wait for the operator to reconcile the aggregate config ──────────────
# Policies are HmacPolicy resources reconciled by the operator into the ConfigMap
# and Secret the server mounts. The server pod starts with optional (initially
# empty) mounts, so block until the operator has produced the config before the
# ext-authz wiring and signed-request warmup depend on real policies being loaded.
log "Waiting for the operator to reconcile the aggregate ConfigMap..."
RECONCILED=false
for _ in $(seq 1 60); do
    if kubectl get configmap hmac-manager-config -n "$NAMESPACE" >/dev/null 2>&1; then
        RECONCILED=true
        break
    fi
    sleep 2
done
if [[ "$RECONCILED" != "true" ]]; then
    log "ERROR: operator did not produce the aggregate ConfigMap 'hmac-manager-config' after 120s."
    kubectl get pods -n "$NAMESPACE" -o wide || true
    kubectl logs -l app.kubernetes.io/component=operator -n "$NAMESPACE" --tail=100 || true
    kubectl get hmacpolicies -n "$NAMESPACE" -o yaml || true
    exit 1
fi
log "Operator reconciled the aggregate ConfigMap."

# ── 9. MeshConfig: register ext-authz provider ───────────────────────────────
log "Patching Istio MeshConfig with ext-authz provider..."
PATCH=$(python3 - <<'EOF'
import subprocess, yaml, json, sys

result = subprocess.run(
    ['kubectl', 'get', 'configmap', 'istio', '-n', 'istio-system', '-o', 'jsonpath={.data.mesh}'],
    capture_output=True, text=True
)
mesh = yaml.safe_load(result.stdout) or {}
providers = [p for p in mesh.get('extensionProviders', []) if p.get('name') != 'hmac-manager']
providers.append({
    'name': 'hmac-manager',
    'envoyExtAuthzHttp': {
        'service': 'hmac-manager.hmac-system.svc.cluster.local',
        'port': 8080,
        'includeRequestHeadersInCheck': [
            'authorization', 'hmac-policy', 'hmac-nonce', 'hmac-daterequested',
        ],
        'withRequestBody': {'maxRequestBytes': 8192, 'allowPartialMessage': False}
    }
})
mesh['extensionProviders'] = providers
print(json.dumps({'data': {'mesh': yaml.dump(mesh)}}))
EOF
)
kubectl patch configmap istio -n istio-system --type merge -p "$PATCH"

log "Restarting istiod to pick up MeshConfig changes..."
kubectl rollout restart deployment/istiod -n istio-system
kubectl rollout status deployment/istiod -n istio-system --timeout=120s

# ── 10. Wait for enforcement to propagate ─────────────────────────────────────
# The ext-authz filter is delivered to the waypoint via xDS after the istiod
# restart, so there is a window where the waypoint still allows unsigned traffic.
# Block until an unsigned request is actually rejected before handing off to the
# test suite, otherwise the first assertions race the config push.
log "Waiting for waypoint ext-authz enforcement to become active..."
ENFORCED=false
for _ in $(seq 1 60); do
    code=$(kubectl exec -n default curl -- \
        curl -s -o /dev/null -w "%{http_code}" "http://echo.default.svc.cluster.local/" 2>/dev/null || echo "000")
    if [[ "$code" == "403" ]]; then
        ENFORCED=true
        break
    fi
    sleep 2
done

if [[ "$ENFORCED" != "true" ]]; then
    log "ERROR: waypoint enforcement not active after 120s (last status: ${code:-none})."

    echo "════════════════ WAYPOINT ENFORCEMENT DIAGNOSTICS ════════════════"
    echo "## waypoint Gateway:"
    kubectl get gateway -n default -o wide 2>/dev/null || true
    kubectl describe gateway waypoint -n default 2>/dev/null | sed -n '/Status:/,$p' || true
    echo "## default-namespace workloads:"
    kubectl get deploy,pods -n default 2>/dev/null || true
    echo "## MeshConfig extensionProviders:"
    kubectl get configmap istio -n istio-system -o jsonpath='{.data.mesh}' 2>/dev/null || true
    echo; echo "## AuthorizationPolicies (all namespaces):"
    kubectl get authorizationpolicy -A -o yaml 2>/dev/null || true
    echo "## istioctl proxy-status:"
    istioctl proxy-status 2>/dev/null || true
    echo "## istioctl analyze (default namespace):"
    istioctl analyze -n default 2>/dev/null || true

    WP=$(kubectl get pods -n default \
        -l gateway.networking.k8s.io/gateway-name=waypoint \
        -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || true)
    echo "## waypoint pod: ${WP:-<none found>}"
    if [[ -n "$WP" ]]; then
        echo "## waypoint listeners — ext_authz filter / provider references:"
        istioctl proxy-config listener "$WP" -n default -o json 2>/dev/null \
            | grep -iE "ext_authz|hmac-manager" || echo "  (no ext_authz filter present on the waypoint)"
        echo "## waypoint clusters — ext-authz upstream:"
        istioctl proxy-config cluster "$WP" -n default 2>/dev/null \
            | grep -iE "hmac|ext.?authz" || echo "  (no hmac-manager upstream cluster present)"
    fi
    echo "═══════════════════════════════════════════════════════════════════"

    exit 1
fi

log "Enforcement active (unsigned request rejected with 403)."

# ── 11. Warmup: verify a signed request returns 200 ───────────────────────────
# This primes the Redis connection pool and confirms ext-authz is passing
# correctly signed requests before the bats test suite runs.
log "Warming up: verifying a signed request returns 200..."
kubectl port-forward deploy/hmac-manager 9090:8081 -n hmac-system >/dev/null 2>&1 &
PF_PID=$!

# Wait for the port-forward to accept connections.
# curl exits 7 on "connection refused"; set -e would kill the script on that
# exit code inside a command substitution, so || true makes the assignment safe.
for _ in $(seq 1 30); do
    code=$(curl -s --max-time 1 -o /dev/null -w "%{http_code}" \
        -X POST -H "Content-Type: application/json" \
        -d '{}' "http://localhost:9090/sign" 2>/dev/null) || true
    if [[ "$code" != "000" ]]; then break; fi
    sleep 1
done

WARMUP_OK=false
for _ in $(seq 1 15); do
    sign=$(curl -s --max-time 2 -X POST "http://localhost:9090/sign" \
        -H "Content-Type: application/json" \
        -d '{"Policy":"MyPolicy","Method":"GET","Uri":"http://echo.default.svc.cluster.local/"}' \
        2>/dev/null) || true

    if echo "$sign" | python3 -c "import sys,json; json.load(sys.stdin)" >/dev/null 2>&1; then
        auth=$(echo "$sign"   | python3 -c "import sys,json; print(json.load(sys.stdin)['Authorization'])")
        policy=$(echo "$sign" | python3 -c "import sys,json; print(json.load(sys.stdin)['Hmac-Policy'])")
        nonce=$(echo "$sign"  | python3 -c "import sys,json; print(json.load(sys.stdin)['Hmac-Nonce'])")
        date=$(echo "$sign"   | python3 -c "import sys,json; print(json.load(sys.stdin)['Hmac-DateRequested'])")

        code=$(kubectl exec -n default curl -- curl -s -o /dev/null -w "%{http_code}" \
            -H "Authorization: $auth" \
            -H "Hmac-Policy: $policy" \
            -H "Hmac-Nonce: $nonce" \
            -H "Hmac-DateRequested: $date" \
            "http://echo.default.svc.cluster.local/" 2>/dev/null || echo "000")

        if [[ "$code" == "200" ]]; then
            WARMUP_OK=true
            break
        fi
    fi
    sleep 2
done

kill "$PF_PID" 2>/dev/null || true

if [[ "$WARMUP_OK" != "true" ]]; then
    log "ERROR: signed requests not passing through ext-authz after warmup."
    exit 1
fi

log "Signed request warmup verified."
log "Setup complete."
