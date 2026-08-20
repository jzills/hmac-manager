import { HmacManagerFactory, HashAlgorithm } from "hmac-manager";

const apiUrl = process.env.API_URL ?? "http://localhost:5140/api/weatherforecast";

// The same policy the .NET Api declares in Program.cs, written out
// independently. Nothing is negotiated at runtime: both ends agree by
// configuration, or every request is rejected.
const factory = new HmacManagerFactory([{
    name: "MyPolicy",
    publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
    privateKey: "dGhpc0lzTXlTdXBlckNvb2xQcml2YXRlS2V5",
    contentHashAlgorithm: HashAlgorithm.SHA256,
    signatureHashAlgorithm: HashAlgorithm.SHA256,
    schemes: [{
        name: "RequireAccountAndEmail",
        headers: ["X-Account", "X-Email"]
    }]
}]);

const hmac = factory.create("MyPolicy", "RequireAccountAndEmail");

const send = async (label, request) => {
    // sign mutates the request, adding the Authorization header and the
    // Hmac-* headers the Api reads.
    const signed = await hmac.sign(request);
    if (!signed.isSuccess) {
        console.error(`${label}: signing failed —`, signed.error?.message);
        return;
    }

    const response = await fetch(request);
    console.log(`${label}: ${response.status} ${await response.text()}`);
};

const withSchemeHeaders = request => {
    // A scheme's headers must be present before signing — their values are
    // part of the signature.
    request.headers.append("X-Account", "myAccountId");
    request.headers.append("X-Email", "someone@example.com");
    return request;
};

await send("GET ", withSchemeHeaders(new Request(apiUrl)));

await send("POST", withSchemeHeaders(new Request(apiUrl, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ summary: "Signed, body and all." })
})));

// Sign, then alter a header the scheme covers. The Api recomputes the
// signature over the value that arrived, which is no longer the one that was
// signed — so this is a 401, indistinguishable to the Api from a forgery.
const tampered = withSchemeHeaders(new Request(apiUrl));
await hmac.sign(tampered);
tampered.headers.set("X-Account", "someoneElsesAccount");

const response = await fetch(tampered);
console.log(`GET  (tampered): ${response.status} — expected 401`);
