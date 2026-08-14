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
