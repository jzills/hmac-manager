import { assert, test } from "vitest";
import HmacSignatureProvider from "../src/components/hmac-signature-provider";
import HmacManagerFactory from "../src/hmac-manager-factory";
import HashAlgorithm from "../src/hash-algorithm";

// The public key goes into the signing content, and .NET holds it as a Guid — so what
// lands on the wire there is always Guid.ToString(), the canonical lowercase form,
// however the key was configured. This client takes a string, so an uppercase GUID used
// to go in verbatim and produce a signature .NET could not reproduce.
//
// test/fixtures/signing-parity.json pins the resulting string against the .NET side.
// These cover the property that fixture cannot: that the case a policy is *configured*
// in makes no difference to anything.

const PublicKeyLower = "eb8e9dae-08bd-4883-80fe-1d9a103b30b5";
const PublicKeyUpper = "EB8E9DAE-08BD-4883-80FE-1D9A103B30B5";
const PrivateKey = "dGhpc0lzTXlTdXBlckNvb2xQcml2YXRlS2V5";
const Nonce = "3f1a9c62-5d84-4b7e-9a1f-0c2d8e6b4a55";

const compute = (publicKey: string) => new HmacSignatureProvider(
    publicKey,
    PrivateKey,
    [],
    HashAlgorithm.SHA256,
    HashAlgorithm.SHA256
).compute(
    new Request("https://api.example.com/api/orders"),
    new Date(1700000000000),
    Nonce);

test("a public key GUID is signed in its canonical lowercase form", async () => {
    const { signingContent } = await compute(PublicKeyUpper);
    assert.include(signingContent, PublicKeyLower);
    assert.notInclude(signingContent, PublicKeyUpper);
});

test("the case a public key is configured in does not change the signature", async () => {
    const upper = await compute(PublicKeyUpper);
    const lower = await compute(PublicKeyLower);

    assert.equal(upper.signingContent, lower.signingContent);
    assert.equal(upper.signature, lower.signature);
});

test("a public key that is not a GUID is left exactly as configured", async () => {
    // Nothing to agree with: Guid.Parse rejects it on the .NET side, so there is no .NET
    // rendering of it. Lowercasing it anyway would change a signature that two copies of
    // this client already agree on.
    const { signingContent } = await compute("SOME_Public_Key");
    assert.include(signingContent, "SOME_Public_Key");
});

test("a request signed under an uppercase key verifies under the lowercase one", async () => {
    // The interop property in process. The signer and the verifier here stand in for a
    // Node client configured from one source and an HmacManager-protected API configured
    // from another; before the fix this pairing failed as a signature mismatch.
    const policy = {
        name: "MyPolicy",
        privateKey: PrivateKey,
        contentHashAlgorithm: HashAlgorithm.SHA256,
        signatureHashAlgorithm: HashAlgorithm.SHA256,
        schemes: []
    };

    const signer = new HmacManagerFactory([{ ...policy, publicKey: PublicKeyUpper }]);
    const verifier = new HmacManagerFactory([{ ...policy, publicKey: PublicKeyLower }]);

    const request = new Request("https://api.example.com/api/orders");
    const signed = await signer.create("MyPolicy")!.sign(request);
    assert.isTrue(signed.isSuccess);

    const result = await verifier.verify(request);
    assert.isTrue(result.isSuccess, result.isSuccess ? "" : result.reason);
});
