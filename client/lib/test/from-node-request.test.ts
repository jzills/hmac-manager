import { assert, test } from "vitest";
import { fromNodeRequest, NodeRequestLike } from "../src/node/from-node-request";

const createNodeRequest = (overrides: Partial<NodeRequestLike> = {}): NodeRequestLike => ({
    url: "/api/weatherforecast",
    method: "GET",
    headers: { host: "api.example.com" },
    ...overrides
});

test("FromNodeRequest_Builds_The_Url_From_The_Host_Header", async () => {
    const request = fromNodeRequest(createNodeRequest());

    assert.equal(request.url, "http://api.example.com/api/weatherforecast");
});

test("FromNodeRequest_Keeps_The_Query_String", async () => {
    const request = fromNodeRequest(createNodeRequest({ url: "/api/weatherforecast?days=3" }));

    assert.equal(request.url, "http://api.example.com/api/weatherforecast?days=3");
});

test("FromNodeRequest_Uses_Https_When_Tls_Terminated_Here", async () => {
    const request = fromNodeRequest(createNodeRequest({ socket: { encrypted: true } }));

    assert.equal(request.url, "https://api.example.com/api/weatherforecast");
});

test("FromNodeRequest_Ignores_Forwarded_Headers_By_Default", async () => {
    // Left on by default these are trivially spoofable: they are ordinary request
    // headers, so anything that can reach the process directly can set them, and the
    // host is inside the signature.
    const request = fromNodeRequest(createNodeRequest({
        headers: {
            host: "api.example.com",
            "x-forwarded-proto": "https",
            "x-forwarded-host": "evil.example.com"
        }
    }));

    assert.equal(request.url, "http://api.example.com/api/weatherforecast");
});

test("FromNodeRequest_Honours_Forwarded_Headers_When_Trusted", async () => {
    const request = fromNodeRequest(createNodeRequest({
        headers: {
            host: "internal.svc.cluster.local",
            "x-forwarded-proto": "https",
            "x-forwarded-host": "api.example.com"
        }
    }), { trustProxy: true });

    assert.equal(request.url, "https://api.example.com/api/weatherforecast");
});

test("FromNodeRequest_Takes_The_Client_Facing_Host_From_A_Proxy_Chain", async () => {
    const request = fromNodeRequest(createNodeRequest({
        headers: {
            host: "internal.svc.cluster.local",
            "x-forwarded-host": "api.example.com, inner.example.com"
        }
    }), { trustProxy: true });

    assert.equal(request.url, "http://api.example.com/api/weatherforecast");
});

test("FromNodeRequest_Takes_The_Client_Facing_Proto_From_A_Proxy_Chain", async () => {
    const request = fromNodeRequest(createNodeRequest({
        headers: {
            host: "api.example.com",
            "x-forwarded-proto": "https, http"
        }
    }), { trustProxy: true });

    assert.equal(request.url, "https://api.example.com/api/weatherforecast");
});

test("FromNodeRequest_Finds_The_Host_Whatever_Its_Casing", async () => {
    // Node lowercases incoming header names, so this only matters for something else
    // shaped like an IncomingMessage — where the alternative is "no Host header" on a
    // request that plainly has one.
    const request = fromNodeRequest(createNodeRequest({ headers: { Host: "api.example.com" } }));

    assert.equal(request.url, "http://api.example.com/api/weatherforecast");
});

test("FromNodeRequest_Prefers_An_Explicit_BaseUrl", async () => {
    const request = fromNodeRequest(createNodeRequest({
        headers: { host: "internal:8080", "x-forwarded-host": "evil.example.com" }
    }), { baseUrl: "https://api.example.com", trustProxy: true });

    assert.equal(request.url, "https://api.example.com/api/weatherforecast");
});

test("FromNodeRequest_Fails_Loudly_When_The_Origin_Cannot_Be_Determined", async () => {
    // Better than defaulting to localhost: the signature would fail to match and the
    // rejection would look exactly like a forgery.
    assert.throws(
        () => fromNodeRequest(createNodeRequest({ headers: {} })),
        /baseUrl/);
});

test("FromNodeRequest_Carries_The_Headers_Across", async () => {
    const request = fromNodeRequest(createNodeRequest({
        headers: { host: "api.example.com", "x-tenant-id": "acme" }
    }));

    assert.equal(request.headers.get("X-Tenant-Id"), "acme");
});

test("FromNodeRequest_Carries_A_Repeated_Header_Across", async () => {
    const request = fromNodeRequest(createNodeRequest({
        headers: { host: "api.example.com", "x-tag": ["a", "b"] }
    }));

    assert.equal(request.headers.get("X-Tag"), "a, b");
});

test("FromNodeRequest_Carries_A_String_Body_Across", async () => {
    const body = JSON.stringify({ city: "Wellington" });
    const request = fromNodeRequest(
        createNodeRequest({ method: "POST" }),
        { body });

    assert.equal(await request.text(), body);
});

test("FromNodeRequest_Carries_Raw_Bytes_Across_Unchanged", async () => {
    // The bytes matter, not their meaning: the content hash is over exactly what arrived,
    // so re-serialising a parsed body would produce a different hash and a signature that
    // does not match.
    const body = new TextEncoder().encode('{"city":"Wellington","days":3}');
    const request = fromNodeRequest(
        createNodeRequest({ method: "POST" }),
        { body });

    assert.equal(await request.text(), '{"city":"Wellington","days":3}');
});

test("FromNodeRequest_Ignores_A_Body_On_A_Get", async () => {
    // Fetch forbids a body on GET or HEAD, and throws rather than dropping it.
    const request = fromNodeRequest(createNodeRequest(), { body: "ignored" });

    assert.equal(request.method, "GET");
    assert.isNull(request.body);
});
