import { HashAlgorithm } from "hmac-manager";

// The policy this API verifies against.
//
// Every field has to match the signer's, because none of it is negotiated — the two
// ends agree by configuration or the request is rejected. The clients in this sample
// declare the same values independently, which is the point: see NodeClient/src/index.js
// and DotnetClient/src/Program.cs.
//
// The keys are literals committed to the repository so the sample runs with no setup.
// They are not an example of key handling — a real private key comes from configuration
// or a secret store, never from source.
export const policy = {
    name: "MyPolicy",
    publicKey: "eb8e9dae-08bd-4883-80fe-1d9a103b30b5",
    privateKey: "dGhpc0lzTXlTdXBlckNvb2xQcml2YXRlS2V5",
    contentHashAlgorithm: HashAlgorithm.SHA256,
    signatureHashAlgorithm: HashAlgorithm.SHA256,

    // How long a signature stays valid. Also how long a nonce is remembered, so a
    // captured request works exactly once and only inside this window.
    maxAgeInSeconds: 30,

    // A scheme folds these header values into the signature, so they cannot be altered
    // in transit. On a successful verification they come back on the result, and this
    // API uses them the way the .NET handler would use claims.
    schemes: [{
        name: "RequireAccountAndEmail",
        headers: ["X-Account", "X-Email"]
    }]
};
