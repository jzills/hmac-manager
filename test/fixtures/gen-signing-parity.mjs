// Generates signing-parity.json.
//
//   node test/fixtures/gen-signing-parity.mjs test/fixtures/signing-parity.json
//
// Run by hand, and only to add a case — regenerating after a change to either
// implementation would defeat the point of the fixture, which is to notice that change.
//
// Deliberately written against node:crypto and string concatenation only. It shares no
// code with the .NET library or the TypeScript client, so those two agreeing with it is
// evidence rather than a tautology. The format it encodes is the one documented at
// site/content/docs/concepts/signing-content.md:
//
//   METHOD:pathAndQuery:authority:dateRequestedMs:publicKey[:contentHash][:headerValues...]:nonce
import crypto from "node:crypto";
import { writeFileSync } from "node:fs";

const cases = [
    {
        name: "get-no-body-no-scheme",
        description: "The simplest request: a GET with no body and no scheme.",
        method: "GET",
        url: "https://api.example.com/api/orders",
        dateRequestedMs: 1700000000000,
        nonce: "3f1a9c62-5d84-4b7e-9a1f-0c2d8e6b4a55",
        publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
        privateKey: "zvg29s2cQ4idOqbUJWETOw==",
        contentHashAlgorithm: "SHA256",
        signatureHashAlgorithm: "SHA256",
        body: null,
        headerValues: []
    },
    {
        name: "get-with-query-and-port",
        description: "A query string, and a non-default port that is part of the authority.",
        method: "GET",
        url: "https://api.example.com:8443/api/orders?status=open&page=2",
        dateRequestedMs: 1700000001234,
        nonce: "c0a80101-1234-4abc-8def-0123456789ab",
        publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
        privateKey: "zvg29s2cQ4idOqbUJWETOw==",
        contentHashAlgorithm: "SHA256",
        signatureHashAlgorithm: "SHA256",
        body: null,
        headerValues: []
    },
    {
        name: "post-with-body",
        description: "A body, which contributes a content hash segment.",
        method: "POST",
        url: "https://api.example.com/api/orders",
        dateRequestedMs: 1700000002000,
        nonce: "9b2e7d41-6c35-4f88-a0d3-71e4f5c6b8a2",
        publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
        privateKey: "zvg29s2cQ4idOqbUJWETOw==",
        contentHashAlgorithm: "SHA256",
        signatureHashAlgorithm: "SHA256",
        body: "{\"sku\":\"ABC-1\",\"quantity\":2}",
        headerValues: []
    },
    {
        name: "post-with-scheme-headers",
        description: "Scheme header values, which sit between the content hash and the nonce.",
        method: "POST",
        url: "https://api.example.com/api/orders",
        dateRequestedMs: 1700000003000,
        nonce: "5e8c1a90-3b72-4d61-9fe8-2a4c6d80b135",
        publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
        privateKey: "zvg29s2cQ4idOqbUJWETOw==",
        contentHashAlgorithm: "SHA256",
        signatureHashAlgorithm: "SHA256",
        body: "{\"sku\":\"ABC-1\",\"quantity\":2}",
        headerValues: [
            { name: "X-Tenant-Id", value: "acme" },
            { name: "X-User-Id", value: "42" }
        ]
    },
    {
        name: "non-ascii-scheme-header",
        description:
            "A scheme header value above ASCII. The signing content is text and is hashed " +
            "as UTF-8; taking the low byte of each code unit instead would produce a " +
            "different signature here and an identical one in every other case.",
        method: "GET",
        url: "https://api.example.com/api/orders",
        dateRequestedMs: 1700000004000,
        nonce: "7d3f2b58-9e14-4c07-b6a5-38f091d2e4c6",
        publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
        privateKey: "zvg29s2cQ4idOqbUJWETOw==",
        contentHashAlgorithm: "SHA256",
        signatureHashAlgorithm: "SHA256",
        body: null,
        headerValues: [{ name: "X-Customer-Name", value: "Zoë Café" }]
    },
    {
        name: "uppercase-public-key",
        description:
            "A public key configured in uppercase. .NET holds it as a Guid, so the wire " +
            "form is the canonical lowercase one whatever case it was configured in; a " +
            "client that signed the configured string verbatim produced a different " +
            "signature from .NET for every request under that policy, and verified " +
            "happily against another copy of itself.",
        method: "GET",
        url: "https://api.example.com/api/orders",
        dateRequestedMs: 1700000007000,
        nonce: "b4c8e21f-7a03-4d95-8e16-5f2739ac0db4",
        publicKey: "EB8E9DAE-08BD-4883-80FE-1D9A103B30B5",
        privateKey: "zvg29s2cQ4idOqbUJWETOw==",
        contentHashAlgorithm: "SHA256",
        signatureHashAlgorithm: "SHA256",
        body: null,
        headerValues: []
    },
    {
        name: "sha512",
        description: "SHA-512 for both the content hash and the signature.",
        method: "PUT",
        url: "https://api.example.com/api/orders/7",
        dateRequestedMs: 1700000005000,
        nonce: "a1b2c3d4-e5f6-4789-9abc-def012345678",
        publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
        privateKey: "zvg29s2cQ4idOqbUJWETOw==",
        contentHashAlgorithm: "SHA512",
        signatureHashAlgorithm: "SHA512",
        body: "{\"quantity\":5}",
        headerValues: []
    },
    {
        name: "sha1",
        description: "SHA-1, still selectable and therefore still part of the contract.",
        method: "DELETE",
        url: "https://api.example.com/api/orders/7",
        dateRequestedMs: 1700000006000,
        nonce: "0fedcba9-8765-4321-a0fe-dcba98765432",
        publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
        privateKey: "zvg29s2cQ4idOqbUJWETOw==",
        contentHashAlgorithm: "SHA1",
        signatureHashAlgorithm: "SHA1",
        body: null,
        headerValues: []
    }
];

const digestName = { SHA1: "sha1", SHA256: "sha256", SHA512: "sha512" };

const build = testCase => {
    const url = new URL(testCase.url);
    const segments = [
        testCase.method,
        `${url.pathname}${url.search}`,
        url.host,
        String(testCase.dateRequestedMs),
        // Lowercased: the segment is a GUID, and its wire form is the canonical
        // lowercase one. .NET gets that for free by holding the key as a Guid.
        testCase.publicKey.toLowerCase()
    ];

    if (testCase.body !== null) {
        segments.push(crypto
            .createHash(digestName[testCase.contentHashAlgorithm])
            .update(Buffer.from(testCase.body, "utf8"))
            .digest("base64"));
    }

    for (const headerValue of testCase.headerValues) {
        segments.push(headerValue.value);
    }

    segments.push(testCase.nonce);

    const signingContent = segments.join(":");
    const signature = crypto
        .createHmac(digestName[testCase.signatureHashAlgorithm],
            Buffer.from(testCase.privateKey, "base64"))
        .update(Buffer.from(signingContent, "utf8"))
        .digest("base64");

    return { ...testCase, signingContent, signature };
};

const fixture = {
    $comment:
        "Cross-implementation parity fixture. Both the .NET library (test/Unit) and the " +
        "TypeScript client (client/lib/test) build the signing content and signature from " +
        "the inputs below and must reproduce these exact strings. Generated by " +
        "site-independent code; see RELEASING.md and the signing-content docs. Do not edit " +
        "by hand — a change here is a change to the wire format.",
    cases: cases.map(build)
};

writeFileSync(process.argv[2], `${JSON.stringify(fixture, null, 4)}\n`);
console.log(`wrote ${fixture.cases.length} cases`);
