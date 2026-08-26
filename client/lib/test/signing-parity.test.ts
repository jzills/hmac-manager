import { assert, test } from "vitest";
import { readFileSync } from "node:fs";
import HmacSignatureProvider from "../src/components/hmac-signature-provider";
import SigningContentBuilder from "../src/builders/signing-content-builder";
import HashAlgorithm from "../src/hash-algorithm";

// The other half of test/Unit/Components/SigningContent/Test_SigningContentParity.cs.
// Both read the same fixture and must reproduce the same strings from the same inputs.
//
// This is the only thing that holds the two implementations to one wire format. Every
// other test in either suite signs and verifies within a single implementation, so an
// implementation could drift into being perfectly self-consistent and unable to talk to
// the other — which is exactly what happened before this existed: the signing content
// was hashed as UTF-8 in .NET and as one byte per code unit in TypeScript, and the two
// produced different signatures for any request whose scheme headers carried a non-ASCII
// character. Nothing failed, because nothing compared them.

type ParityCase = {
    name: string;
    description: string;
    method: string;
    url: string;
    dateRequestedMs: number;
    nonce: string;
    publicKey: string;
    privateKey: string;
    contentHashAlgorithm: keyof typeof algorithms;
    signatureHashAlgorithm: keyof typeof algorithms;
    body: string | null;
    headerValues: { name: string; value: string }[];
    signingContent: string;
    signature: string;
};

const algorithms = {
    SHA1: HashAlgorithm.SHA1,
    SHA256: HashAlgorithm.SHA256,
    SHA512: HashAlgorithm.SHA512
};

const fixture: { cases: ParityCase[] } = JSON.parse(
    readFileSync(new URL("../../../test/fixtures/signing-parity.json", import.meta.url), "utf8"));

const compute = (testCase: ParityCase) => {
    const headers = new Headers();
    for (const { name, value } of testCase.headerValues) {
        headers.set(name, value);
    }

    const request = new Request(testCase.url, {
        method: testCase.method,
        headers,
        ...(testCase.body === null ? {} : { body: testCase.body })
    });

    const provider = new HmacSignatureProvider(
        testCase.publicKey,
        testCase.privateKey,
        testCase.headerValues.map(headerValue => headerValue.name),
        algorithms[testCase.contentHashAlgorithm],
        algorithms[testCase.signatureHashAlgorithm],
        new SigningContentBuilder()
    );

    return provider.compute(request, new Date(testCase.dateRequestedMs), testCase.nonce);
};

test("the fixture has cases", () => {
    // A fixture that failed to load would otherwise make every test below vacuously pass.
    assert.isAbove(fixture.cases.length, 0);
});

for (const testCase of fixture.cases) {
    test(`signing content parity: ${testCase.name}`, async () => {
        const { signingContent } = await compute(testCase);
        assert.equal(signingContent, testCase.signingContent, testCase.description);
    });

    test(`signature parity: ${testCase.name}`, async () => {
        const { signature } = await compute(testCase);
        assert.equal(signature, testCase.signature, testCase.description);
    });
}
