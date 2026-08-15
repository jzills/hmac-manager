---
title: JavaScript client
linkTitle: Client
description: Signing requests from a browser or Node with the hmac-manager npm package.
weight: 5
---

The [`hmac-manager`](https://www.npmjs.com/package/hmac-manager) npm package
signs requests so they verify against an HmacManager-protected API. It builds
the same [signing content](../concepts/signing-content/) the .NET library does.

It **signs only**. There is no verification side — that is the .NET library or
the mesh verifier.

```bash
npm install hmac-manager
```

Ships ESM (`dist/index.js`), CommonJS (`dist/index.cjs`) and type declarations.

{{< hm-children >}}
