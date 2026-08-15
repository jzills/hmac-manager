# Node API

A **Node** API verifying HMAC-signed requests, called by a Node client and a .NET
client. Every other sample verifies in ASP.NET Core; this one is the same exchange with
the verifier written in TypeScript's runtime instead, which is the one thing it changes.

The point of the sample is what the two clients have in common: nothing. Neither knows
what is on the other end. Both ends build the same
[signing content](https://jzills.github.io/hmac-manager/docs/concepts/signing-content/),
so a `.NET → Node` call is configured exactly like a `.NET → .NET` one.

| Project | Runtime | Role |
|---|---|---|
| [Api](Api/server.js) | Node | Verifies. Rejects replays, tampering and expired signatures. |
| [NodeClient](NodeClient/index.js) | Node | Signs with the `hmac-manager` npm package. |
| [DotnetClient](DotnetClient/Program.cs) | .NET | Signs with the `HmacManager` NuGet package. |

## Running it

The Node projects depend on the client library in this repository through a `file:`
link, so build it once first:

```bash
cd client/lib && npm ci && npm run build
```

Then start the API and leave it running:

```bash
cd samples/NodeApiAuthentication/Api
npm install
npm start                       # listening on http://localhost:5200
```

In a second terminal, run either client:

```bash
cd samples/NodeApiAuthentication/NodeClient
npm install && npm start
```

```bash
cd samples/NodeApiAuthentication/DotnetClient
dotnet run
```

Both print a `200` for a `GET` and a `POST`. The Node client additionally sends a
replayed request and a tampered one, which come back `401` — watch the API's terminal,
which logs the reason each was rejected:

```
GET /api/weatherforecast verified for myAccountId <someone@example.com>
GET /api/weatherforecast rejected: replayed
GET /api/weatherforecast rejected: signature-mismatch
```

## The API

One factory for the process — it owns the nonce store, and a store built per request
would only ever hold that request's own nonce, so replay detection would silently never
fire.

```js
const verifier = new HmacManagerFactory([policy]);
```

Then, per request:

```js
const body = await readBody(request);

const result = await verifier.verify(fromNodeRequest(request, {
    body: body.length > 0 ? body : undefined
}));

if (!result.isSuccess) {
    console.warn(`rejected: ${result.reason}`);   // log it, don't return it
    return send(response, 401, { error: "Unauthorized" });
}
```

`verify` on the **factory** reads the policy the request names and resolves it, which is
what a server needs — it does not know which policy the caller used until it reads the
request. It never throws; `result.reason` says which of the checks rejected it.

`fromNodeRequest` turns Node's `IncomingMessage` into the Fetch `Request` that `verify`
takes. Two things it exists to get right, both of which otherwise fail as a signature
mismatch indistinguishable from a forgery:

- **The body must be the raw bytes.** The content hash covers exactly what arrived. This
  sample buffers the body itself and parses it *after* verifying, so the bytes that were
  checked are the bytes that get used. With `express.json()` the raw bytes are gone by
  the time you see the request — capture them during parsing with
  `express.json({ verify: (req, _res, buf) => { req.rawBody = buf; } })`.
- **The origin must be the one the caller signed.** The signing content covers the host.
  By default the `Host` header and the socket's TLS state decide it; behind an ingress
  that terminates TLS, pass `trustProxy: true` so `x-forwarded-proto` is believed. It is
  off by default because those headers are ordinary request headers that anything
  reaching the process directly can set.

## Scheme header values are the claims

The policy declares a scheme, so `X-Account` and `X-Email` are folded into the signature
and cannot be altered in transit. On success they come back by name:

```js
const { "X-Account": account, "X-Email": email } = result.headerValues;
```

These are the only claims on the request worth trusting — every other header travelled
unprotected. The .NET handler turns exactly these into `Claim`s; here they are used
directly. The Node client's `tampered` case changes `X-Account` after signing and gets a
`401`, which is the property in action.

## The keys

Literals committed to the repository so the sample runs with no setup. They are not an
example of key handling — a real private key comes from configuration or a secret store.
The API reads `PORT` and both clients read `API_URL` from the environment if you want to
move things around.

**📖 [Verifying requests](https://jzills.github.io/hmac-manager/docs/client/verifying-requests/)**
