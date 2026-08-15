import { assert, test } from "vitest";
import HmacManagerFactory from "../src/hmac-manager-factory";
import HashAlgorithm from "../src/hash-algorithm";

test("HmacManagerFactory", async () => {
    const request = new Request("https://localhost:7216/api/weatherforecast", {
        headers: {
            "X-AccountId": "123",
            "X-Email": "my@email.com"
        }
    });

    const hmacManagerFactory = new HmacManagerFactory([{
        name: "Policy-A",
        publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
        privateKey: btoa("thisIsMySuperCoolPrivateKey"),
        contentHashAlgorithm: HashAlgorithm.SHA256,
        signatureHashAlgorithm: HashAlgorithm.SHA256,
        schemes: [{
            name: "Scheme",
            headers: ["X-AccountId", "X-Email"]
        }]
    }]);

    const hmacManager = hmacManagerFactory.create("Policy-A");
    await hmacManager?.sign(request);

    const policyHeader = request.headers.get("Hmac-Policy");
    const schemeHeader = request.headers.get("Hmac-Scheme");
    assert.equal(policyHeader, "Policy-A");
    assert.equal(schemeHeader, null);
});

test("HmacManagerFactory_With_SigningContentAccessor", async () => {
    const request = new Request("https://localhost:7216/api/weatherforecast", {
        headers: {
            "X-AccountId": "123",
            "X-Email": "my@email.com"
        }
    });

    const hmacManagerFactory = new HmacManagerFactory([{
        name: "Policy-A",
        publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
        privateKey: btoa("thisIsMySuperCoolPrivateKey"),
        contentHashAlgorithm: HashAlgorithm.SHA256,
        signatureHashAlgorithm: HashAlgorithm.SHA256,
        schemes: [{
            name: "Scheme",
            headers: ["X-AccountId", "X-Email"]
        }],
        signingContentAccessor: context => Promise.resolve(`${context.request?.method}`) // Really bad idea, don't do it
    }]);

    const hmacManager = hmacManagerFactory.create("Policy-A");
    const signingResult = await hmacManager?.sign(request);

    const policyHeader = request.headers.get("Hmac-Policy");
    const schemeHeader = request.headers.get("Hmac-Scheme");
    assert.equal(policyHeader, "Policy-A");
    assert.equal(schemeHeader, null);
    assert.equal(signingResult?.hmac?.signingContent, "GET")
});

// Scheme resolution. create() previously checked only the policy, so a scheme
// name that did not resolve produced a working manager that signed without it.

const policyWithScheme = {
    name: "Policy-A",
    publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
    privateKey: btoa("thisIsMySuperCoolPrivateKey"),
    contentHashAlgorithm: HashAlgorithm.SHA256,
    signatureHashAlgorithm: HashAlgorithm.SHA256,
    schemes: [{
        name: "Scheme",
        headers: ["X-AccountId", "X-Email"]
    }]
};

test("create returns null for an unknown scheme", async () => {
    const factory = new HmacManagerFactory([policyWithScheme]);

    assert.isNull(factory.create("Policy-A", "Scheem"));
});

test("create returns null for an unknown policy", async () => {
    const factory = new HmacManagerFactory([policyWithScheme]);

    assert.isNull(factory.create("Policy-B"));
    assert.isNull(factory.create("Policy-B", "Scheme"));
});

test("create still resolves a policy with no scheme requested", async () => {
    const factory = new HmacManagerFactory([policyWithScheme]);

    assert.isNotNull(factory.create("Policy-A"));
});

test("a resolved scheme signs with its headers", async () => {
    const request = new Request("https://localhost:7216/api/weatherforecast", {
        headers: {
            "X-AccountId": "123",
            "X-Email": "my@email.com"
        }
    });

    const hmacManager = new HmacManagerFactory([policyWithScheme]).create("Policy-A", "Scheme");
    const result = await hmacManager!.sign(request);

    assert.isTrue(result.isSuccess);
    assert.equal(request.headers.get("Hmac-Scheme"), "Scheme");

    // The values the scheme names are in the signing content, which is the
    // property a silently-dropped scheme used to lose.
    assert.include(result!.hmac!.signingContent, "123");
    assert.include(result!.hmac!.signingContent, "my@email.com");
});

// Blank means "no scheme", matching the .NET factory's IsNullOrWhiteSpace rule. #97 guarded on
// `scheme !== null`, which made "" a scheme name that never resolves and so refused a call that
// had worked before it — and still works in .NET.

const policyWithoutSchemes = {
    name: "Policy-B",
    publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
    privateKey: btoa("thisIsMySuperCoolPrivateKey"),
    contentHashAlgorithm: HashAlgorithm.SHA256,
    signatureHashAlgorithm: HashAlgorithm.SHA256,
    schemes: []
};

test("a blank scheme means no scheme, on a policy that has one", async () => {
    const factory = new HmacManagerFactory([policyWithScheme]);

    assert.isNotNull(factory.create("Policy-A", ""));
    assert.isNotNull(factory.create("Policy-A", "   "));
    assert.isNotNull(factory.create("Policy-A", null));
    assert.isNotNull(factory.create("Policy-A", undefined as unknown as null));
});

test("a blank scheme means no scheme, on a policy that has none", async () => {
    const factory = new HmacManagerFactory([policyWithoutSchemes]);

    assert.isNotNull(factory.create("Policy-B"));
    assert.isNotNull(factory.create("Policy-B", ""));
    assert.isNotNull(factory.create("Policy-B", "   "));
    assert.isNotNull(factory.create("Policy-B", null));

    // Asking a schemeless policy for a real name is still a mistake.
    assert.isNull(factory.create("Policy-B", "AnyScheme"));
});

test("a blank scheme signs without one", async () => {
    const request = new Request("https://localhost:7216/api/weatherforecast", {
        headers: { "X-AccountId": "123", "X-Email": "my@email.com" }
    });

    const manager = new HmacManagerFactory([policyWithScheme]).create("Policy-A", "");
    const result = await manager!.sign(request);

    assert.isTrue(result.isSuccess);
    assert.isNull(result.hmac!.scheme);
    assert.isNull(request.headers.get("Hmac-Scheme"));
});

test("a name that is only surrounded by whitespace is still a miss", async () => {
    // Blank is absent; a real name is not trimmed. " Scheme " is not "Scheme", in either
    // implementation, so it must be refused rather than quietly matched.
    const factory = new HmacManagerFactory([policyWithScheme]);

    assert.isNull(factory.create("Policy-A", " Scheme "));
});
