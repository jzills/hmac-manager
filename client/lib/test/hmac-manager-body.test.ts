import { assert, test } from "vitest";
import HmacManagerFactory from "../src/hmac-manager-factory";
import HashAlgorithm from "../src/hash-algorithm";

// The end-to-end path for a request that carries a body. computeContentHash is
// unit-tested in hmac-utilities.test.ts; these check it is reached correctly
// through sign() — the body's hash lands in the signing content, and reading it
// does not consume the caller's request.

const policy = {
    name: "Policy-A",
    publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
    privateKey: btoa("thisIsMySuperCoolPrivateKey"),
    contentHashAlgorithm: HashAlgorithm.SHA256,
    signatureHashAlgorithm: HashAlgorithm.SHA256,
    schemes: []
};

const sign = (request: Request) =>
    new HmacManagerFactory([policy]).create("Policy-A")!.sign(request);

test("a request body is covered by the signing content", async () => {
    const body = JSON.stringify({ sku: "ABC-1", quantity: 2 });
    const request = new Request("https://localhost:7216/api/orders", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body
    });

    const result = await sign(request);
    assert.isTrue(result.isSuccess);

    // The content hash is its own segment, so the signing content for a request
    // with a body has one more field than the same request without.
    const withoutBody = await sign(new Request("https://localhost:7216/api/orders", {
        method: "POST"
    }));

    const withBodyFields = result.hmac!.signingContent.split(":").length;
    const withoutBodyFields = withoutBody.hmac!.signingContent.split(":").length;
    assert.equal(withBodyFields, withoutBodyFields + 1);
});

test("a different body produces different signing content", async () => {
    const one = await sign(new Request("https://localhost:7216/api/orders", {
        method: "POST",
        body: JSON.stringify({ quantity: 2 })
    }));

    const two = await sign(new Request("https://localhost:7216/api/orders", {
        method: "POST",
        body: JSON.stringify({ quantity: 3 })
    }));

    assert.isTrue(one.isSuccess);
    assert.isTrue(two.isSuccess);
    assert.notEqual(one.hmac!.signingContent, two.hmac!.signingContent);
});

test("signing does not consume the request body", async () => {
    const body = JSON.stringify({ sku: "ABC-1", quantity: 2 });
    const request = new Request("https://localhost:7216/api/orders", {
        method: "POST",
        body
    });

    await sign(request);

    // sign() clones before reading, so the caller can still send this request.
    assert.isFalse(request.bodyUsed);
    assert.equal(await request.text(), body);
});
