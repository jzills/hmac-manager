import { assert, test } from "vitest";
import { HmacAuthenticationDefaults } from "../src/hmac-authentication-defaults";
import HmacManagerFactory from "../src/hmac-manager-factory";
import HmacPolicy from "../src/components/hmac-policy";
import HashAlgorithm from "../src/hash-algorithm";

const PrivateKey = btoa("thisIsMySuperCoolPrivateKey");
const Url = "https://localhost:7216/api/weatherforecast";

const createPolicy = (overrides: Partial<HmacPolicy> = {}): HmacPolicy => ({
    name: "Policy-A",
    publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
    privateKey: PrivateKey,
    contentHashAlgorithm: HashAlgorithm.SHA256,
    signatureHashAlgorithm: HashAlgorithm.SHA256,
    schemes: [],
    ...overrides
});

/**
 * A signer and a verifier that share a policy but nothing else, which is the arrangement
 * being tested — two processes agreeing only on configuration.
 */
const createPair = (policy: HmacPolicy = createPolicy(), isConsolidated: boolean = false) => ({
    signer: new HmacManagerFactory([policy], isConsolidated),
    verifier: new HmacManagerFactory([policy], isConsolidated)
});

/** Rebuilds a signed request against a different URL or method, keeping its headers. */
const tamper = (request: Request, url: string = request.url, method: string = request.method) =>
    new Request(url, { method, headers: request.headers });

test("HmacManager_Verify_Accepts_A_Request_It_Signed", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    const signingResult = await signer.create("Policy-A")!.sign(request);
    assert.isTrue(signingResult.isSuccess);

    const result = await verifier.verify(request);

    assert.isTrue(result.isSuccess);
    assert.isUndefined(result.reason);
    assert.equal(result.hmac?.policy, "Policy-A");
    assert.equal(result.hmac?.signature, signingResult.hmac?.signature);
});

test("HmacManager_Verify_Accepts_A_Signed_Request_With_A_Body", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url, {
        method: "POST",
        body: JSON.stringify({ city: "Wellington", days: 3 })
    });

    await signer.create("Policy-A")!.sign(request);
    const result = await verifier.verify(request);

    assert.isTrue(result.isSuccess);
});

test("HmacManager_Verify_Leaves_The_Request_Body_Readable", async () => {
    const { signer, verifier } = createPair();
    const body = JSON.stringify({ city: "Wellington" });
    const request = new Request(Url, { method: "POST", body });

    await signer.create("Policy-A")!.sign(request);
    await verifier.verify(request);

    // Verification reads the body to hash it. Doing that on the request itself rather
    // than a clone would leave the handler that called verify with a consumed stream.
    assert.equal(await request.text(), body);
});

test("HmacManager_Verify_Rejects_A_Tampered_Path", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);
    const result = await verifier.verify(tamper(request, "https://localhost:7216/api/admin"));

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "signature-mismatch");
});

test("HmacManager_Verify_Rejects_A_Tampered_Query_String", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(`${Url}?days=3`);

    await signer.create("Policy-A")!.sign(request);
    const result = await verifier.verify(tamper(request, `${Url}?days=30`));

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "signature-mismatch");
});

test("HmacManager_Verify_Rejects_A_Tampered_Method", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);
    const result = await verifier.verify(tamper(request, Url, "DELETE"));

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "signature-mismatch");
});

test("HmacManager_Verify_Rejects_A_Tampered_Body", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url, { method: "POST", body: JSON.stringify({ amount: 10 }) });

    await signer.create("Policy-A")!.sign(request);

    const tampered = new Request(Url, {
        method: "POST",
        headers: request.headers,
        body: JSON.stringify({ amount: 10000 })
    });

    const result = await verifier.verify(tampered);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "signature-mismatch");
});

test("HmacManager_Verify_Rejects_A_Signature_Made_With_A_Different_Private_Key", async () => {
    const signer = new HmacManagerFactory([createPolicy()]);
    const verifier = new HmacManagerFactory([createPolicy({ privateKey: btoa("aDifferentKey") })]);

    const request = new Request(Url);
    await signer.create("Policy-A")!.sign(request);

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "signature-mismatch");
});

test("HmacManager_Verify_Rejects_An_Unsigned_Request", async () => {
    const { verifier } = createPair();

    const result = await verifier.verify(new Request(Url));

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "headers-missing");
});

test("HmacManager_Verify_Rejects_A_Malformed_Authorization_Header", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);
    request.headers.set(HmacAuthenticationDefaults.Headers.Authorization, "Bearer abc123");

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "headers-malformed");
});

test("HmacManager_Verify_Rejects_A_Nonce_That_Is_Not_A_Uuid", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);
    request.headers.set(HmacAuthenticationDefaults.Headers.Nonce, "not-a-uuid");

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "headers-malformed");
});

test("HmacManager_Verify_Accepts_An_Uppercase_Nonce", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);

    // The signature was computed over the lowercase nonce sign() generated. Uppercasing
    // the header the way a third-party signer's Guid.ToString("D").ToUpper() might is
    // only safe to accept if the verifier normalizes back to the same casing before
    // recomputing -- otherwise this is indistinguishable from a tampered nonce.
    const nonce = request.headers.get(HmacAuthenticationDefaults.Headers.Nonce)!;
    request.headers.set(HmacAuthenticationDefaults.Headers.Nonce, nonce.toUpperCase());

    const result = await verifier.verify(request);

    assert.isTrue(result.isSuccess);
});

test("HmacManager_Verify_Rejects_A_DateRequested_That_Is_Not_A_Number", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);
    request.headers.set(HmacAuthenticationDefaults.Headers.DateRequested, "yesterday");

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "headers-malformed");
});

test("HmacManager_Verify_Rejects_A_Signature_Older_Than_The_Policy_Window", async () => {
    const { signer, verifier } = createPair(createPolicy({ maxAgeInSeconds: 5 }));
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);
    request.headers.set(
        HmacAuthenticationDefaults.Headers.DateRequested,
        (Date.now() - 6000).toString());

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "expired");
});

test("HmacManager_Verify_Rejects_A_Signature_Dated_In_The_Future", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);
    request.headers.set(
        HmacAuthenticationDefaults.Headers.DateRequested,
        (Date.now() + 60_000).toString());

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "expired");
});

test("HmacManager_Verify_Rejects_A_Replayed_Request", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);

    const first = await verifier.verify(request);
    const second = await verifier.verify(request);

    assert.isTrue(first.isSuccess);
    assert.isFalse(second.isSuccess);
    assert.equal(second.reason, "replayed");
});

test("HmacManager_Verify_Rejects_An_Unregistered_Policy", async () => {
    const signer = new HmacManagerFactory([createPolicy()]);
    const verifier = new HmacManagerFactory([createPolicy({ name: "Policy-B" })]);

    const request = new Request(Url);
    await signer.create("Policy-A")!.sign(request);

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "policy-not-found");
});

test("HmacManager_Verify_Rejects_A_Request_Naming_A_Scheme_The_Policy_Does_Not_Declare", async () => {
    const { signer, verifier } = createPair(createPolicy({
        schemes: [{ name: "Scheme-A", headers: ["X-Tenant-Id"] }]
    }));

    const request = new Request(Url, { headers: { "X-Tenant-Id": "acme" } });
    await signer.create("Policy-A", "Scheme-A")!.sign(request);
    request.headers.set(HmacAuthenticationDefaults.Headers.Scheme, "Scheme-Z");

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "policy-not-found");
});

test("HmacManager_Verify_Accepts_A_Request_Signed_With_A_Scheme", async () => {
    const { signer, verifier } = createPair(createPolicy({
        schemes: [{ name: "Scheme-A", headers: ["X-Tenant-Id", "X-User-Id"] }]
    }));

    const request = new Request(Url, {
        headers: { "X-Tenant-Id": "acme", "X-User-Id": "42" }
    });

    await signer.create("Policy-A", "Scheme-A")!.sign(request);
    const result = await verifier.verify(request);

    assert.isTrue(result.isSuccess);
    assert.equal(result.hmac?.scheme, "Scheme-A");

    // The values the signature covered, which is what makes them usable as claims.
    assert.deepEqual(result.headerValues, { "X-Tenant-Id": "acme", "X-User-Id": "42" });
});

test("HmacManager_Verify_Rejects_A_Tampered_Scheme_Header_Value", async () => {
    const { signer, verifier } = createPair(createPolicy({
        schemes: [{ name: "Scheme-A", headers: ["X-Tenant-Id"] }]
    }));

    const request = new Request(Url, { headers: { "X-Tenant-Id": "acme" } });
    await signer.create("Policy-A", "Scheme-A")!.sign(request);

    // The point of a scheme: these values are inside the signature, so changing one in
    // transit invalidates it rather than quietly becoming the verifier's truth.
    request.headers.set("X-Tenant-Id", "evilcorp");

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "signature-mismatch");
});

test("HmacManager_Verify_Rejects_A_Request_Missing_A_Header_Its_Scheme_Requires", async () => {
    const { signer, verifier } = createPair(createPolicy({
        schemes: [{ name: "Scheme-A", headers: ["X-Tenant-Id"] }]
    }));

    const request = new Request(Url, { headers: { "X-Tenant-Id": "acme" } });
    await signer.create("Policy-A", "Scheme-A")!.sign(request);
    request.headers.delete("X-Tenant-Id");

    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "headers-missing");
});

test("HmacManager_Verify_Accepts_A_Request_Signed_With_Consolidated_Headers", async () => {
    const { signer, verifier } = createPair(createPolicy(), true);
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);
    assert.isNotNull(request.headers.get(HmacAuthenticationDefaults.Headers.Options));

    const result = await verifier.verify(request);

    assert.isTrue(result.isSuccess);
});

test("HmacManager_Verify_Rejects_Consolidated_Headers_When_Not_Configured_For_Them", async () => {
    const signer = new HmacManagerFactory([createPolicy()], true);
    const verifier = new HmacManagerFactory([createPolicy()], false);

    const request = new Request(Url);
    await signer.create("Policy-A")!.sign(request);

    // The two ends disagreeing about header layout is a misconfiguration, and this is
    // what it looks like: the verifier is reading headers the signer never sent.
    const result = await verifier.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "headers-missing");
});

test("HmacManager_Verify_Does_Not_Report_The_Callers_Signature_As_Verified", async () => {
    const { signer, verifier } = createPair();
    const request = new Request(Url);

    await signer.create("Policy-A")!.sign(request);
    const result = await verifier.verify(tamper(request, "https://localhost:7216/api/admin"));

    // A failure must not hand back an hmac at all — anything downstream reading
    // result.hmac without checking isSuccess would be reading the attacker's claims.
    assert.isNull(result.hmac);
});

test("HmacManager_Verify_On_A_Manager_Rejects_A_Request_For_Another_Policy", async () => {
    // Two policies sharing a key pair. The policy name is not part of the signing
    // content, so without the explicit check a manager for one would happily verify a
    // request signed for the other.
    const signer = new HmacManagerFactory([createPolicy({ name: "Policy-A" })]);
    const verifier = new HmacManagerFactory([
        createPolicy({ name: "Policy-A" }),
        createPolicy({ name: "Policy-B" })
    ]);

    const request = new Request(Url);
    await signer.create("Policy-A")!.sign(request);

    const result = await verifier.create("Policy-B")!.verify(request);

    assert.isFalse(result.isSuccess);
    assert.equal(result.reason, "policy-not-found");
});
