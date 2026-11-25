---
title: "HmacManager"
toc: false
---

<section class="hm-hero">
  <span class="hm-hero__logo">{{< hm-wordmark >}}</span>
  <h1 class="hm-hero__title">HMAC request authentication, in your app or in your mesh.</h1>
  <p class="hm-hero__blurb">
    Sign and verify requests against named policies, with replay protection
    built in. Add it to an ASP.NET Core API as a NuGet package, or enforce it
    across an Istio mesh with no application changes at all.
  </p>
  <div class="hm-hero__actions">
    <a class="hm-button hm-button--primary" href="docs/getting-started/dotnet-quickstart/">Get started</a>
    <a class="hm-button" href="docs/">Documentation</a>
  </div>
  {{< hm-install >}}
  <p class="hm-hero__alt">
    Published to <a href="https://www.nuget.org/packages/HmacManager/">NuGet</a>,
    <a href="https://www.npmjs.com/package/hmac-manager">npm</a>,
    <a href="https://artifacthub.io/packages/helm/zills/hmac-manager">Artifact Hub</a>
    and <a href="https://hub.docker.com/r/zills/hmac-manager">Docker Hub</a>.
  </p>
</section>

<section class="hm-features" id="features" aria-label="What HmacManager does">
  <a class="hm-feature" href="docs/concepts/policies/">
    <div class="hm-feature__head">
      <h2 class="hm-feature__title">Named policies</h2>
      <span class="hm-feature__icon">{{< hm-icon name="key" >}}</span>
    </div>
    <p>A policy is a key pair, a hash algorithm choice, a replay window and a set of schemes. Register several and verify each request against the one it names.</p>
  </a>
  <a class="hm-feature" href="docs/concepts/schemes/">
    <div class="hm-feature__head">
      <h2 class="hm-feature__title">Schemes</h2>
      <span class="hm-feature__icon">{{< hm-icon name="tag" >}}</span>
    </div>
    <p>A named set of headers folded into the signature, so a request cannot be replayed against a different account or tenant. The headers map to claims.</p>
  </a>
  <a class="hm-feature" href="docs/concepts/nonce-and-replay/">
    <div class="hm-feature__head">
      <h2 class="hm-feature__title">Replay protection</h2>
      <span class="hm-feature__icon">{{< hm-icon name="shield-check" >}}</span>
    </div>
    <p>Every signature carries a nonce and a timestamp, cached for the window so a captured request cannot be sent twice. In-process or Redis-backed.</p>
  </a>
  <a class="hm-feature" href="docs/dotnet/dynamic-policies/">
    <div class="hm-feature__head">
      <h2 class="hm-feature__title">Dynamic policies</h2>
      <span class="hm-feature__icon">{{< hm-icon name="refresh" >}}</span>
    </div>
    <p>A static singleton, reloaded from configuration as it changes, or resolved per request from a database, without giving up the built-in handler.</p>
  </a>
  <a class="hm-feature" href="docs/dotnet/logging/">
    <div class="hm-feature__head">
      <h2 class="hm-feature__title">Diagnosable rejections</h2>
      <span class="hm-feature__icon">{{< hm-icon name="document-search" >}}</span>
    </div>
    <p>Structured <code>ILogger</code> output says <em>which</em> check failed, and every outcome is a metric, with a Grafana dashboard in the Helm chart.</p>
  </a>
  <a class="hm-feature" href="docs/kubernetes/hmacpolicy-crd/">
    <div class="hm-feature__head">
      <h2 class="hm-feature__title">Policies as resources</h2>
      <span class="hm-feature__icon">{{< hm-icon name="cube" >}}</span>
    </div>
    <p>An <code>HmacPolicy</code> CRD and an operator that reconciles it, so the mesh's policies live in Git and keys stay in Kubernetes Secrets.</p>
  </a>
</section>

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
            policy.UsePublicKey(Guid.Parse("4a4ab0c4-d724-4874-af72-09dd410f3b91"));
            policy.UsePrivateKey("zvg29s2cQ4idOqbUJWETOw==");
            policy.UseMemoryCache(maxAgeInSeconds: 300);
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
  publicKey: "4a4ab0c4-d724-4874-af72-09dd410f3b91"
  privateKeySecretRef:
    name: my-hmac-secrets
    key: my-policy-privateKey
  algorithms:
    contentHash: SHA256
    signingHash: HMACSHA256
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
  publicKey: "4a4ab0c4-d724-4874-af72-09dd410f3b91",
  privateKey: "zvg29s2cQ4idOqbUJWETOw==",
  contentHashAlgorithm: HashAlgorithm.SHA256,
  signatureHashAlgorithm: HashAlgorithm.SHA256,
  schemes: []
}]);

const request = new Request("https://api.example.com/orders");
await factory.create("MyPolicy")!.sign(request);
const response = await fetch(request);
```

<p class="hm-section__more"><a href="docs/client/">The client documentation →</a></p>

</div>

<section class="hm-closing" id="install">
  <h2>Install HmacManager</h2>
  <p>Three artifacts, versioned independently. Take the one that matches where you want the check to happen.</p>
  {{< hm-install >}}
  <div class="hm-hero__actions">
    <a class="hm-button hm-button--primary" href="docs/getting-started/install/">Installation guide</a>
    <a class="hm-button" href="https://github.com/jzills/hmac-manager">GitHub</a>
  </div>
</section>
