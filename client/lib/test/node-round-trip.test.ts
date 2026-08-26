import { assert, test } from "vitest";
import { createServer, IncomingMessage, ServerResponse } from "node:http";
import { AddressInfo } from "node:net";
import HmacManagerFactory from "../src/hmac-manager-factory";
import HmacPolicy from "../src/components/hmac-policy";
import HmacVerificationResult from "../src/components/hmac-verification-result";
import HashAlgorithm from "../src/hash-algorithm";
import { fromNodeRequest } from "../src/node/from-node-request";

// A signed request over a real socket into a real Node server.
//
// The unit tests hand `verify` a Request object the signer just finished mutating, which
// cannot catch anything the wire does to a request: header casing, the Host header the
// URL has to be rebuilt from, a body arriving as bytes in however many chunks TCP felt
// like. Those are precisely what the Node adapter exists to get right, and getting any of
// them wrong shows up as a signature mismatch indistinguishable from a forgery.

const policy: HmacPolicy = {
    name: "Policy-A",
    publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
    privateKey: btoa("thisIsMySuperCoolPrivateKey"),
    contentHashAlgorithm: HashAlgorithm.SHA256,
    signatureHashAlgorithm: HashAlgorithm.SHA256,
    schemes: [{ name: "Scheme-A", headers: ["X-Tenant-Id"] }]
};

/** Buffers the request body, the way a body parser would have to. */
const readBody = (request: IncomingMessage): Promise<Buffer> =>
    new Promise((resolve, reject) => {
        const chunks: Buffer[] = [];
        request.on("data", chunk => chunks.push(chunk));
        request.on("end", () => resolve(Buffer.concat(chunks)));
        request.on("error", reject);
    });

/**
 * Runs a server that verifies every request, and reports the outcome back in the
 * response so the test can assert on it.
 */
const withServer = async (
    run: (origin: string) => Promise<void>
): Promise<void> => {
    const verifier = new HmacManagerFactory([policy]);

    const server = createServer(async (request: IncomingMessage, response: ServerResponse) => {
        const body = await readBody(request);
        const result: HmacVerificationResult = await verifier.verify(
            fromNodeRequest(request, { body: body.length > 0 ? body : undefined }));

        response.writeHead(result.isSuccess ? 200 : 401, { "Content-Type": "application/json" });
        response.end(JSON.stringify({
            isSuccess: result.isSuccess,
            reason: result.reason ?? null,
            headerValues: result.headerValues ?? null
        }));
    });

    await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
    const { port } = server.address() as AddressInfo;

    try {
        await run(`http://127.0.0.1:${port}`);
    } finally {
        await new Promise<void>(resolve => { server.close(() => resolve()); });
    }
};

test("NodeRoundTrip_A_Signed_Get_Verifies", async () => {
    await withServer(async origin => {
        const signer = new HmacManagerFactory([policy]);
        const request = new Request(`${origin}/api/orders`);

        const signingResult = await signer.create("Policy-A")!.sign(request);
        assert.isTrue(signingResult.isSuccess);

        const response = await fetch(request);

        assert.equal(response.status, 200);
        assert.deepEqual(await response.json(), {
            isSuccess: true, reason: null, headerValues: {}
        });
    });
});

test("NodeRoundTrip_A_Signed_Post_With_A_Body_Verifies", async () => {
    await withServer(async origin => {
        const signer = new HmacManagerFactory([policy]);
        const request = new Request(`${origin}/api/orders`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ sku: "ABC-1", quantity: 2 })
        });

        await signer.create("Policy-A")!.sign(request);
        const response = await fetch(request);

        assert.equal(response.status, 200);
    });
});

test("NodeRoundTrip_A_Signed_Request_With_A_Query_String_Verifies", async () => {
    await withServer(async origin => {
        const signer = new HmacManagerFactory([policy]);
        const request = new Request(`${origin}/api/orders?status=open&page=2`);

        await signer.create("Policy-A")!.sign(request);
        const response = await fetch(request);

        assert.equal(response.status, 200);
    });
});

test("NodeRoundTrip_A_Signed_Request_With_A_Scheme_Verifies_And_Carries_Its_Claims", async () => {
    await withServer(async origin => {
        const signer = new HmacManagerFactory([policy]);
        const request = new Request(`${origin}/api/orders`, {
            headers: { "X-Tenant-Id": "acme" }
        });

        await signer.create("Policy-A", "Scheme-A")!.sign(request);
        const response = await fetch(request);

        assert.equal(response.status, 200);
        assert.deepEqual((await response.json()).headerValues, { "X-Tenant-Id": "acme" });
    });
});

test("NodeRoundTrip_An_Unsigned_Request_Is_Rejected", async () => {
    await withServer(async origin => {
        const response = await fetch(`${origin}/api/orders`);

        assert.equal(response.status, 401);
        assert.equal((await response.json()).reason, "headers-missing");
    });
});

test("NodeRoundTrip_A_Body_Altered_In_Flight_Is_Rejected", async () => {
    await withServer(async origin => {
        const signer = new HmacManagerFactory([policy]);
        const signed = new Request(`${origin}/api/orders`, {
            method: "POST",
            body: JSON.stringify({ amount: 10 })
        });

        await signer.create("Policy-A")!.sign(signed);

        // The signed headers, a different body — a proxy that rewrote the payload.
        const response = await fetch(`${origin}/api/orders`, {
            method: "POST",
            headers: signed.headers,
            body: JSON.stringify({ amount: 10000 })
        });

        assert.equal(response.status, 401);
        assert.equal((await response.json()).reason, "signature-mismatch");
    });
});

test("NodeRoundTrip_A_Replayed_Request_Is_Rejected_The_Second_Time", async () => {
    await withServer(async origin => {
        const signer = new HmacManagerFactory([policy]);
        const request = new Request(`${origin}/api/orders`);
        await signer.create("Policy-A")!.sign(request);

        // The same signed headers sent twice, which is all a captured request is.
        const first = await fetch(`${origin}/api/orders`, { headers: request.headers });
        const second = await fetch(`${origin}/api/orders`, { headers: request.headers });

        assert.equal(first.status, 200);
        assert.equal(second.status, 401);
        assert.equal((await second.json()).reason, "replayed");
    });
});
