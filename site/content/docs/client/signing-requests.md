---
title: Signing requests
description: What gets signed, how bodies are hashed, and handling the result.
weight: 2
---

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

const manager = factory.create("MyPolicy");
if (!manager) throw new Error("no policy named MyPolicy");

const request = new Request("https://api.example.com/orders");
const result = await manager.sign(request);

if (!result.isSuccess) throw new Error("could not sign the request");

const response = await fetch(request);
```

`sign` adds the headers to the request you passed in and returns an
`HmacResult`. The request object is mutated, so it is the same one you hand to
`fetch`.

## Handling the result

Neither call throws on the paths you would expect to.

```ts
type HmacResult = {
  hmac: Hmac | null;
  isSuccess: boolean;
  dateGenerated: Date;
  error?: unknown;
};
```

`factory.create` returns `null` when a name was given and does not resolve — an
unregistered policy, or a scheme that policy does not declare. Leaving the
scheme out is not a failure, and neither is passing a blank one: `null`,
`undefined`, `""` and whitespace all mean "no scheme". `sign` catches
everything internally and reports failure through `isSuccess`, with the cause on
`error`: a missing scheme header, an unusable key, an insecure context.

Ignoring the result means sending an unsigned request and getting a `401` you
cannot explain, so check it:

```ts
const result = await manager.sign(request);
if (!result.isSuccess) {
  // No exception was thrown; this is the only signal.
  console.error("signing failed:", result.error);
  return;
}
```

`error` is typed `unknown` rather than `Error`, because a `catch` binding is —
narrow it before using it.

On success, `result.hmac` carries the `policy`, `scheme`, `nonce`,
`dateRequested`, `signature` and the `signingContent` that was hashed — the
last of which is what you compare against the server's when a signature does
not match.

## Requests with a body

The body is hashed and the hash becomes a segment of the signing content, so
the body is covered by the signature.

```ts
const request = new Request("https://api.example.com/orders", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ sku: "ABC-1", quantity: 2 })
});

await manager.sign(request);
await fetch(request);
```

The request is cloned internally before the body is read, so signing does not
consume it and the original stays usable — `request.bodyUsed` is still `false`
afterwards, and you hand the same object to `fetch`.

The body's stream is drained to completion before hashing, so a body arriving in
several chunks — a large payload, or one built from a `ReadableStream` — is
covered in full. Size makes no difference to correctness.

A request with no body has no content-hash segment at all, on either side — and
so does a request whose body is present but zero-length. The two are treated
identically, so a `POST` carrying an empty string signs the same content as a
`POST` carrying nothing.

## The URL must be absolute

The host is part of the signing content, so it has to be known when the request
is signed:

```ts
new Request("https://api.example.com/orders")   // signs
new Request("/orders")                          // no host to sign
```

The query string is included; the fragment is not, since it never leaves the
client.

Credentials in the URL are not supported:

```ts
new Request("https://user:pass@api.example.com/orders")
// TypeError: Request cannot be constructed from a URL that includes credentials
```

The Fetch `Request` constructor refuses a URL carrying userinfo per
specification, so `sign` never gets the chance to sign it — the failure
happens at `new Request(...)`, before `manager.sign` is even called, and
`result.error` is not involved.

## Matching the server

Everything about the policy must match the verifying side — name, public key,
private key, both algorithms, and the scheme if there is one. There is no
negotiation, so a disagreement shows up as a rejected signature rather than as
a configuration error. See
[signing content](../../concepts/signing-content/) for the exact string both
ends build.

The verifying side can be this same package: see
[verifying requests](../verifying-requests/).
