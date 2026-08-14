# Client library

[![npm Version](https://img.shields.io/npm/v/hmac-manager?logo=npm&label=npm)](https://www.npmjs.com/package/hmac-manager)

The JavaScript and TypeScript client, published to npm as
[`hmac-manager`](https://www.npmjs.com/package/hmac-manager). It signs requests
so they verify against an
[HmacManager](https://github.com/jzills/hmac-manager)-protected API.

| Directory | What it is |
| --- | --- |
| [`lib/`](lib/) | The published package — source, tests, and the npm README |
| [`sample/`](sample/) | A small Node consumer that installs the built tarball |

```bash
npm install hmac-manager
```

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/client/)**

## Building

```bash
cd lib
npm ci
npm run build     # Vite -> dist/index.js, dist/index.cjs, dist/index.d.ts
npx vitest run
```

Releasing is driven by an `npm/vX.Y.Z` tag — see [RELEASING.md](../RELEASING.md).
