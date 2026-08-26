---
title: Client quickstart
description: Sign a fetch request from a browser or Node, and verify one in a Node service.
weight: 4
---

The `hmac-manager` npm package signs requests and verifies them, so it can be
either end of the exchange — the others being the
[.NET library](../dotnet-quickstart/) and the
[mesh verifier](../kubernetes-quickstart/).

```bash
npm install hmac-manager
```

## Sign a request

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
const result = await factory.create("MyPolicy")!.sign(request);

if (result.isSuccess) {
  const response = await fetch(request);
}
```

`sign` mutates the request's headers in place, so the same `Request` object is
what you hand to `fetch`.

## Two things that will bite you

`create` returns `null` when a name was given and does not resolve — an
unregistered policy, or a scheme that policy does not declare. It does not
throw. The `!` above is fine for a literal you just registered and wrong for
anything dynamic.

A blank scheme is not a failed lookup: `null`, `undefined`, `""` and whitespace
all mean "no scheme", so passing one straight from configuration behaves the
same however absence reaches you.

`sign` never throws either. It returns an `HmacResult` whose `isSuccess` is
`false` and whose `error` carries the cause, so a signing failure is silent
unless you check:

```ts
const result = await factory.create("MyPolicy")!.sign(request);
if (!result.isSuccess) {
  throw new Error(`could not sign the request: ${result.error}`);
}
```

{{% hm-note kind="warn" %}}
The private key is a shared secret. Putting it in browser code publishes it to
everyone who loads the page. In a browser this package belongs in a trusted
context — an extension, a kiosk, a first-party app talking to its own
backend — or behind a proxy that holds the key. Node has no such caveat.
{{% /hm-note %}}

## Verify a request

The same policy set, read from the other direction:

```ts
const result = await factory.verify(request);

if (!result.isSuccess) {
  console.warn("rejected", { reason: result.reason });   // log it, don't return it
  return new Response(null, { status: 401 });
}
```

`verify` on the **factory** reads the policy the request names and resolves it —
which is what a server needs, since it does not know which policy the caller
used until it reads the request. Like `sign`, it never throws; `result.reason`
says which check rejected it.

On Node's `http`, Express, Koa or Fastify there is no `Request` to hand it. Use
`fromNodeRequest(req, { body: rawBody })`, and read
[verifying requests](../../client/verifying-requests/) first — the raw body and
the origin behind a proxy are both easy to get wrong, and both fail as a
signature mismatch that looks exactly like a forgery.

Verification needs the private key, so it is server-side only. The warning above
is the whole reason.

## Requirements

`crypto.randomUUID` and `crypto.subtle` are used for the nonce and the hashes,
so a browser needs a [secure context](https://developer.mozilla.org/en-US/docs/Web/Security/Secure_Contexts)
— `https://` or `localhost`. Node 19+ exposes both globally.

The URL must be absolute. The signing content includes the host, and the
verifier rebuilds it from the request it received; a relative URL would sign
something the server cannot reproduce.

## Next

- [Signing requests](../../client/signing-requests/) — content hashing, bodies, and what gets signed
- [Verifying requests](../../client/verifying-requests/) — replay protection, Node and Express, failure reasons
- [Schemes](../../client/schemes/) — adding header values to the signature
- [API](../../client/api/) — the full surface
