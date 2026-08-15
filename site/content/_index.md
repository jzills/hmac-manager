---
title: "HmacManager"
toc: false
---

<div class="hm-hero">
  <span class="hm-hero__logo">{{< hm-wordmark >}}</span>
  <p class="hm-hero__tagline">HMAC request authentication, in your app or in your mesh.</p>
  <p class="hm-hero__blurb">
    Sign and verify requests against named policies, with replay protection
    built in. Add it to an ASP.NET Core API as a NuGet package, or enforce it
    across an Istio mesh with no application changes at all.
  </p>
  <ul class="hm-hero__links">
    <li><a href="https://www.nuget.org/packages/HmacManager/">NuGet</a></li>
    <li><a href="https://www.npmjs.com/package/hmac-manager">npm</a></li>
    <li><a href="https://artifacthub.io/packages/helm/zills/hmac-manager">Artifact Hub</a></li>
    <li><a href="https://hub.docker.com/r/zills/hmac-manager">Docker Hub</a></li>
    <li><a href="https://github.com/jzills/hmac-manager">GitHub</a></li>
  </ul>
</div>

<div class="hm-section" id="install">
  <h2 class="hm-section__title">Install</h2>
  <p class="hm-section__lede">
    Three artifacts, versioned independently. Take the one that matches where
    you want the check to happen.
  </p>
  <div class="hm-install">
    <div class="hm-install__card">
      <div class="hm-install__label">ASP.NET Core library</div>
      <div class="hm-install__command">dotnet add package HmacManager</div>
    </div>
    <div class="hm-install__card">
      <div class="hm-install__label">Kubernetes / Istio</div>
      <div class="hm-install__command">helm repo add zills https://jzills.github.io/hmac-manager</div>
      <div class="hm-install__command">helm install hmac-manager zills/hmac-manager</div>
    </div>
    <div class="hm-install__card">
      <div class="hm-install__label">JavaScript / TypeScript client</div>
      <div class="hm-install__command">npm install hmac-manager</div>
    </div>
  </div>
</div>

<div class="hm-section" id="dotnet">

<h2 class="hm-section__title">In your ASP.NET Core app</h2>
<p class="hm-section__lede">
Register a policy and the built-in authentication handler verifies every
incoming request — which check failed is reported, not just that one did.
</p>

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(options =>
    {
        options.AddPolicy("MyPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("00000000-0000-0000-0000-000000000001"));
            policy.UsePrivateKey("zvg29s2cQ4idOqbUJWETOw==");
            policy.UseMemoryCache(maxAgeInSeconds: 300); // nonce / replay window
        });
    });
```

<p class="hm-section__lede">
On the calling side, attach the handler to an <code>HttpClient</code> and
outgoing requests are signed for you.
</p>

```csharp
builder.Services
    .AddHttpClient("api", client => client.BaseAddress = new Uri("https://api.example.com"))
    .AddHmacHttpMessageHandler("MyPolicy");
```

<p class="hm-section__more"><a href="docs/dotnet/">The .NET documentation →</a></p>

</div>

<div class="hm-section" id="kubernetes">

<h2 class="hm-section__title">Or at the edge, with no application changes</h2>
<p class="hm-section__lede">
HmacManager ships as an <a href="https://www.envoyproxy.io/docs/envoy/latest/api-v3/extensions/filters/http/ext_authz/v3/ext_authz.proto">Envoy ext-authz</a>
HTTP server. An Istio ingress gateway or an ambient waypoint calls it before
forwarding a request, so the check happens outside your service entirely.
</p>

{{< hm-flow >}}

<p class="hm-section__lede">
Declare policies as Kubernetes resources and the operator reconciles them
into the ConfigMap and Secret the verifier mounts. Keys come from Secrets,
and changes hot-reload without a pod restart.
</p>

```yaml
apiVersion: hmac-manager.io/v1alpha1
kind: HmacPolicy
metadata:
  name: my-policy
  namespace: hmac-system
spec:
  publicKey: "00000000-0000-0000-0000-000000000001"
  privateKeySecretRef:
    name: my-hmac-secrets
    key: my-policy-privateKey
  algorithms:
    contentHash: SHA256      # SHA1 | SHA256 | SHA512
    signingHash: HMACSHA256  # HMACSHA1 | HMACSHA256 | HMACSHA512
  nonce:
    maxAgeInSeconds: 300
```

<p class="hm-section__more"><a href="docs/kubernetes/">The Kubernetes documentation →</a></p>

</div>

<div class="hm-section" id="client">

<h2 class="hm-section__title">Signing from a browser or Node</h2>
<p class="hm-section__lede">
The TypeScript client builds the same signing content as the .NET library, so
a request it signs verifies against an HmacManager-protected API.
</p>

```ts
import { HmacManagerFactory, HashAlgorithm } from "hmac-manager";

const factory = new HmacManagerFactory([{
  name: "MyPolicy",
  publicKey: "00000000-0000-0000-0000-000000000001",
  privateKey: "zvg29s2cQ4idOqbUJWETOw==",
  contentHashAlgorithm: HashAlgorithm.SHA256,
  signatureHashAlgorithm: HashAlgorithm.SHA256,
  schemes: []
}]);

const request = new Request("https://api.example.com/orders");
await factory.create("MyPolicy")!.sign(request); // adds the Hmac headers
const response = await fetch(request);
```

<p class="hm-section__more"><a href="docs/client/">The client documentation →</a></p>

</div>

<div class="hm-section" id="features">
  <h2 class="hm-section__title">What you get</h2>
  <p class="hm-section__lede">
    Each of these has a page in <a href="docs/">the documentation</a>.
  </p>
  <div class="hm-features">
    <div class="hm-feature">
      <p class="hm-feature__title"><a href="docs/concepts/policies/">Named policies</a></p>
      <p class="hm-feature__body">
        A policy is a key pair, a hash algorithm choice, a replay window and a
        set of schemes. Register several and verify each request against the
        one it names, so different callers can hold different keys.
      </p>
    </div>
    <div class="hm-feature">
      <p class="hm-feature__title"><a href="docs/concepts/schemes/">Schemes</a></p>
      <p class="hm-feature__body">
        A named set of headers whose values are folded into the signature — so
        a request cannot be replayed against a different account or tenant.
        Those headers map to claims automatically.
      </p>
    </div>
    <div class="hm-feature">
      <p class="hm-feature__title"><a href="docs/concepts/nonce-and-replay/">Replay protection</a></p>
      <p class="hm-feature__body">
        Every signature carries a nonce and a timestamp, cached for the
        lifetime of the window so a captured request cannot be sent twice.
        In-process or Redis-backed.
      </p>
    </div>
    <div class="hm-feature">
      <p class="hm-feature__title"><a href="docs/dotnet/dynamic-policies/">Dynamic policies</a></p>
      <p class="hm-feature__body">
        Policies can be a static singleton, reloaded from configuration as it
        changes, or resolved per request from a database — without giving up
        the built-in authentication handler.
      </p>
    </div>
    <div class="hm-feature">
      <p class="hm-feature__title"><a href="docs/dotnet/logging/">Diagnosable rejections</a></p>
      <p class="hm-feature__body">
        Structured <code>ILogger</code> output with stable event ids: every
        rejection says <em>which</em> check failed. No message can carry a
        private key, and a test asserts it over the whole sign/verify path.
      </p>
    </div>
    <div class="hm-feature">
      <p class="hm-feature__title"><a href="docs/kubernetes/hmacpolicy-crd/">Policies as resources</a></p>
      <p class="hm-feature__body">
        An <code>HmacPolicy</code> CRD and an operator that reconciles it, so
        the mesh's policies live in Git beside everything else and keys stay
        in Kubernetes Secrets.
      </p>
    </div>
  </div>
</div>
