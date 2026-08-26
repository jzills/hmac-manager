---
title: Install and requirements
linkTitle: Install
description: Module formats, runtime requirements, and where the private key can safely live.
weight: 1
---

```bash
npm install hmac-manager
```

```ts
import { HmacManagerFactory, HashAlgorithm, HmacAuthenticationDefaults } from "hmac-manager";
```

CommonJS works too:

```js
const { HmacManagerFactory, HashAlgorithm } = require("hmac-manager");
```

## Runtime requirements

The package uses the Web Crypto API — `crypto.randomUUID` for the nonce and
`crypto.subtle` for the hashes.

| Runtime | Requirement |
| --- | --- |
| Browser | A [secure context](https://developer.mozilla.org/en-US/docs/Web/Security/Secure_Contexts) — `https://` or `localhost`. `crypto.subtle` is `undefined` on plain `http://`. |
| Node | 19 or later, where both are global. |

There is no polyfill path. On an insecure origin, signing fails rather than
falling back to something weaker.

## Where the private key lives

The private key is a shared secret: whoever holds it can mint valid requests
for that policy.

{{% hm-note kind="warn" %}}
Shipping the key in browser JavaScript publishes it. Anyone who loads the page
can read it out of the bundle and sign whatever they like as that policy. No
amount of bundling or obfuscation changes this.
{{% /hm-note %}}

That leaves a short list of places this package belongs:

- **Node** — a server, a CLI, a job. No caveat; this is the normal case.
- **A trusted client** — a browser extension, a kiosk, a desktop app where the
  key is provisioned per install and can be revoked per install.
- **Behind a proxy** — the browser calls your own backend, and the backend
  holds the key and signs.

What it is not for is a public web page talking to a third-party API with a
key baked in. If that is the shape of the problem, the key needs to be on a
server and the browser needs to be calling that server.

### Verifying is server-side only

The same key both signs and verifies — that is what makes HMAC symmetric — so
[`verify`](../verifying-requests/) needs exactly the secret the warning above is
about. There is no browser exception to find: a bundle that can check a
signature can forge one.

Nothing enforces this, because nothing can. `verify` runs wherever
`crypto.subtle` does, and a browser build that calls it will work perfectly
while handing the key to everyone who loads the page.

## Version

The npm package and the NuGet package are released separately and their version
numbers differ. Neither constrains the other — they interoperate because they
build the same signing content, not because their versions match.
