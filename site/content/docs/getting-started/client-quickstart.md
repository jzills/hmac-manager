---
title: Client quickstart
description: Sign a fetch request from a browser or Node so it verifies server-side.
weight: 4
---

The `hmac-manager` npm package signs requests. It does not verify them — the
verifying side is the [.NET library](../dotnet-quickstart/) or the
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

`create` returns `null` when either name does not resolve — an unregistered
policy, or a scheme that policy does not declare. It does not throw. The `!`
above is fine for a literal you just registered and wrong for anything dynamic.

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

## Requirements

`crypto.randomUUID` and `crypto.subtle` are used for the nonce and the hashes,
so a browser needs a [secure context](https://developer.mozilla.org/en-US/docs/Web/Security/Secure_Contexts)
— `https://` or `localhost`. Node 19+ exposes both globally.

The URL must be absolute. The signing content includes the host, and the
verifier rebuilds it from the request it received; a relative URL would sign
something the server cannot reproduce.

## Next

- [Signing requests](../../client/signing-requests/) — content hashing, bodies, and what gets signed
- [Schemes](../../client/schemes/) — adding header values to the signature
- [API](../../client/api/) — the full surface
