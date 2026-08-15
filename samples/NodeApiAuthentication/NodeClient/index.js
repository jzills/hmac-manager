import { HmacManagerFactory, HashAlgorithm } from "hmac-manager";

const URL = process.env.API_URL ?? "http://localhost:5200/api/weatherforecast";

// The same policy the API declares, written out independently. Nothing is negotiated
// at runtime: if any field here disagrees with the API's, every request is rejected.
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

// Returns null rather than throwing when the name does not resolve — an unregistered
// policy, or a scheme this policy does not declare.
const manager = factory.create("MyPolicy", "RequireAccountAndEmail");
if (manager === null) {
    throw new Error("No such policy or scheme.");
}

/** The scheme's headers must be on the request before it is signed. */
const withSchemeHeaders = request => {
    request.headers.set("X-Account", "myAccountId");
    request.headers.set("X-Email", "someone@example.com");
    return request;
};

const call = async (label, request) => {
    // sign mutates the request's headers in place, so the same object goes to fetch.
    const result = await manager.sign(request);

    // sign never throws. Ignoring this is how a request goes out unsigned and comes
    // back as a 401 you cannot explain.
    if (!result.isSuccess) {
        console.error(`${label}: could not sign —`, result.error);
        return;
    }

    const response = await fetch(request);
    console.log(`${label}: ${response.status}`);
    console.dir(await response.json(), { depth: null });
};

await call("GET ", withSchemeHeaders(new Request(URL)));

await call("POST", withSchemeHeaders(new Request(URL, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ summary: "This is a test." })
})));

// The same signed request sent twice. The second is refused: its nonce has been spent,
// which is what stops a captured request being replayed inside its validity window.
const replayed = withSchemeHeaders(new Request(URL));
await manager.sign(replayed);
console.log(`replay 1: ${(await fetch(replayed)).status}`);
console.log(`replay 2: ${(await fetch(replayed)).status}   <- rejected, nonce already used`);

// A header the scheme covers, altered after signing. The signature no longer matches,
// so the API cannot be talked into believing a different account.
const tampered = withSchemeHeaders(new Request(URL));
await manager.sign(tampered);
tampered.headers.set("X-Account", "someoneElsesAccount");
console.log(`tampered: ${(await fetch(tampered)).status}   <- rejected, signature mismatch`);
