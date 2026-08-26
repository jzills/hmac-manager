import { createServer } from "node:http";
import { HmacManagerFactory, fromNodeRequest } from "hmac-manager";
import { policy } from "./policy.js";

const PORT = Number(process.env.PORT ?? 5200);

// One factory for the process, not one per request. It owns the nonce store, and a
// store built per request would only ever contain that request's own nonce — replay
// detection would silently never fire.
//
// The default store is in-process. This sample is one process, so that is correct here;
// anything running more than one replica needs a shared store behind the same interface.
// See https://jzills.github.io/hmac-manager/docs/client/verifying-requests/
const verifier = new HmacManagerFactory([policy]);

/** Buffers the request body. Node does not do this for you. */
const readBody = request => new Promise((resolve, reject) => {
    const chunks = [];
    request.on("data", chunk => chunks.push(chunk));
    request.on("end", () => resolve(Buffer.concat(chunks)));
    request.on("error", reject);
});

const send = (response, status, payload) => {
    response.writeHead(status, { "Content-Type": "application/json" });
    response.end(JSON.stringify(payload, null, 4));
};

const summaries = [
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
];

const forecast = () => Array.from({ length: 5 }, (_, index) => {
    const date = new Date();
    date.setDate(date.getDate() + index + 1);
    return {
        date: date.toISOString().slice(0, 10),
        temperatureC: Math.floor(Math.random() * 75) - 20,
        summary: summaries[Math.floor(Math.random() * summaries.length)]
    };
});

createServer(async (request, response) => {
    // Read the body before verifying. The content hash covers the raw bytes exactly as
    // they arrived, so they have to be in hand — and once a body parser has consumed the
    // stream, re-serialising what it produced does not reproduce them.
    const body = await readBody(request);

    const result = await verifier.verify(fromNodeRequest(request, {
        body: body.length > 0 ? body : undefined

        // Behind an ingress that terminates TLS, add trustProxy: true — otherwise the
        // origin is rebuilt as http:// while the caller signed https://, and every
        // request fails as a signature mismatch. It is off by default because
        // x-forwarded-* are ordinary request headers that anything reaching this process
        // directly can set.
    }));

    if (!result.isSuccess) {
        // Log the reason; do not return it. A verifier should not narrate to a caller
        // why their forgery was rejected — 401 and nothing else.
        console.warn(`${request.method} ${request.url} rejected: ${result.reason}`);
        return send(response, 401, { error: "Unauthorized" });
    }

    // The scheme header values, which the signature covered. These are the only claims
    // on the request worth trusting: every other header travelled unprotected.
    const { "X-Account": account, "X-Email": email } = result.headerValues;
    console.log(`${request.method} ${request.url} verified for ${account} <${email}>`);

    if (request.url.split("?")[0] !== "/api/weatherforecast") {
        return send(response, 404, { error: "Not Found" });
    }

    if (request.method === "GET") {
        return send(response, 200, { account, email, forecast: forecast() });
    }

    if (request.method === "POST") {
        // Safe to parse now: the bytes just verified are the bytes being parsed.
        return send(response, 200, { account, email, received: JSON.parse(body.toString()) });
    }

    return send(response, 405, { error: "Method Not Allowed" });
}).listen(PORT, () => {
    console.log(`Verifying API listening on http://localhost:${PORT}`);
    console.log(`Policy "${policy.name}", scheme "${policy.schemes[0].name}"`);
});
