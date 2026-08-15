---
title: JavaScript client
linkTitle: Client
description: Signing and verifying requests from a browser or Node with the hmac-manager npm package.
weight: 5
---

The [`hmac-manager`](https://www.npmjs.com/package/hmac-manager) npm package
signs requests so they verify against an HmacManager-protected API, and verifies
incoming ones so a Node service can be such an API itself — no .NET hop and no
mesh sidecar required. It builds the same
[signing content](../concepts/signing-content/) the .NET library does.

Signing runs anywhere with WebCrypto. Verifying is server-side by nature: it
needs the private key, and a browser bundle that has one has published it.

```bash
npm install hmac-manager
```

Ships ESM (`dist/index.js`), CommonJS (`dist/index.cjs`) and type declarations.

{{< hm-children >}}
