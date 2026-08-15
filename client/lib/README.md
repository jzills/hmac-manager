# hmac-manager

[![npm Version](https://img.shields.io/npm/v/hmac-manager?logo=npm&label=npm)](https://www.npmjs.com/package/hmac-manager)

JavaScript and TypeScript client for
[HmacManager](https://github.com/jzills/hmac-manager). Signs requests so they
verify against an HmacManager-protected ASP.NET Core API or Istio ext-authz
service.

Signing only — there is no verification side.

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/client/)**

## Install

```bash
npm install hmac-manager
```

## Usage

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

if (!result.isSuccess) throw new Error(`could not sign: ${result.error}`);

const response = await fetch(request);
```

`sign` adds the HMAC headers to the request in place. It never throws — failures
come back as `result.isSuccess === false`, with the cause on `result.error`, so
check it.

`create` returns `null` when a name was given and does not resolve: an
unregistered policy, or a scheme that policy does not declare. A blank scheme
(`null`, `undefined`, `""` or whitespace) means "no scheme" rather than a failed
lookup.

## Requirements

Uses `crypto.randomUUID` and `crypto.subtle`, so a browser needs a secure
context (`https://` or `localhost`) and Node needs 19 or later.

> [!WARNING]
> The private key is a shared secret. Shipping it in browser JavaScript
> publishes it to everyone who loads the page. See
> [where the private key lives](https://jzills.github.io/hmac-manager/docs/client/install/#where-the-private-key-lives).

## Documentation

- [Signing requests](https://jzills.github.io/hmac-manager/docs/client/signing-requests/)
- [Schemes](https://jzills.github.io/hmac-manager/docs/client/schemes/)
- [API](https://jzills.github.io/hmac-manager/docs/client/api/)

## Source

[github.com/jzills/hmac-manager](https://github.com/jzills/hmac-manager)
