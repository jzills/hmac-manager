# JavaScript client

[Simple authentication](../WebToApiAuthentication/) with the client written in
JavaScript instead of .NET. The Api is unchanged and has no idea its caller is
not .NET — the wire format is the contract, not the language.

The client library is linked from source, so build it first:

```bash
cd ../../client/lib && npm ci && npm run build

cd ../../samples/WebToApiAuthenticationWithJavaScriptClient
dotnet run --project Api          # http://localhost:5140

cd Client && npm install && npm start    # second terminal
```

```
GET : 200 [{"date":"2026-08-16","temperatureC":4,...}]
POST: 200 {"summary":"Signed, body and all.","account":"myAccountId","email":"someone@example.com"}
GET  (tampered): 401 — expected 401
```

The `POST` echoes back the account and email, which arrived as claims on the
.NET side — the scheme's header values, signed by a TypeScript client and
turned into a `ClaimsPrincipal` by the ASP.NET Core handler.

## What to look at

| File | Why |
|---|---|
| [`Client/index.js`](Client/index.js) | `HmacManagerFactory`, the policy, `sign`, and the tampering case |
| [`Api/Program.cs`](Api/Program.cs) | The same policy declared in C#, independently |

The two policy declarations are the thing to compare. Nothing is negotiated at
runtime: both ends agree by configuration — same keys, same algorithms, same
scheme headers — or every request is rejected.

```js
const hmac = factory.create("MyPolicy", "RequireAccountAndEmail");

await hmac.sign(request);       // adds Authorization and the Hmac-* headers
const response = await fetch(request);
```

## This is Node, not a browser

The private key is a shared secret: whoever holds it can mint valid requests.
Shipping it in browser JavaScript publishes it, and no amount of bundling
changes that. This sample runs under Node for that reason.

A browser that needs to reach an HMAC-protected API should call your own
backend, and the backend should hold the key and sign. See
[where the private key lives](https://jzills.github.io/hmac-manager/docs/client/install/#where-the-private-key-lives).

## Notes

Node 19 or later — the package uses `crypto.subtle` and `crypto.randomUUID`,
which are global from 19 on.

`npm install`, not `npm ci`. The dependency is a `file:` link to
`client/lib` in this repository, so there is nothing external to pin and the
lockfile is not committed.

For a Node service on the **verifying** side, see
[the Node API sample](../NodeApiAuthentication/).

**📖 [TypeScript client](https://jzills.github.io/hmac-manager/docs/client/)**
