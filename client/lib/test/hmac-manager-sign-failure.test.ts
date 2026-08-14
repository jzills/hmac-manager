import { assert, test } from "vitest";
import HmacManagerFactory from "../src/hmac-manager-factory";
import HashAlgorithm from "../src/hash-algorithm";

// sign() reports failure through isSuccess rather than throwing. These cover
// the half of that contract that used to be missing: what failed, and that a
// failed signing really does leave the request unsigned rather than partially
// signed.

const policy = {
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

test("sign reports the cause when a scheme header is missing", async () => {
    // X-Email is declared by the scheme and absent from the request.
    const request = new Request("https://localhost:7216/api/weatherforecast", {
        headers: { "X-AccountId": "123" }
    });

    const hmacManager = new HmacManagerFactory([policy]).create("Policy-A", "Scheme");
    const result = await hmacManager!.sign(request);

    assert.isFalse(result.isSuccess);
    assert.isNull(result.hmac);

    // The point of the change: the reason survives instead of being discarded.
    assert.instanceOf(result.error, Error);
    assert.match((result.error as Error).message, /missing headers/i);
});

test("a failed signing leaves the request unsigned", async () => {
    const request = new Request("https://localhost:7216/api/weatherforecast", {
        headers: { "X-AccountId": "123" }
    });

    const hmacManager = new HmacManagerFactory([policy]).create("Policy-A", "Scheme");
    const result = await hmacManager!.sign(request);

    assert.isFalse(result.isSuccess);

    // No partial signing: a caller that ignores isSuccess and calls fetch()
    // sends a request carrying none of these.
    assert.isNull(request.headers.get("Authorization"));
    assert.isNull(request.headers.get("Hmac-Policy"));
    assert.isNull(request.headers.get("Hmac-Nonce"));
    assert.isNull(request.headers.get("Hmac-DateRequested"));
});

test("a successful signing carries no error key", async () => {
    const request = new Request("https://localhost:7216/api/weatherforecast", {
        headers: { "X-AccountId": "123", "X-Email": "my@email.com" }
    });

    const hmacManager = new HmacManagerFactory([policy]).create("Policy-A", "Scheme");
    const result = await hmacManager!.sign(request);

    assert.isTrue(result.isSuccess);
    assert.isNotNull(result.hmac);

    // Absent, not present-and-undefined — a success should not look like it
    // has an empty error to anything enumerating the result.
    assert.isFalse("error" in result);
});
