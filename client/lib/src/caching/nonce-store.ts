/**
 * Records the nonces that have already been used, so a captured request cannot be
 * replayed within its validity window.
 *
 * The counterpart to `INonceCache` on the .NET side. A signature stays valid for as
 * long as its `maxAgeInSeconds` window, so without a store anyone who observes a signed
 * request can resend it verbatim until that window closes and it will verify — the
 * signature is over the request, and a replay *is* the request.
 *
 * Implement this against Redis or any other shared store when more than one process
 * verifies for the same policy. {@link MemoryNonceStore} is per-process, so with
 * several replicas a replay simply needs to land on a different one.
 */
export default interface NonceStore {
    /**
     * Whether this nonce has been seen before.
     *
     * @deprecated Implement {@link tryAdd}; verification only calls this for a store
     * without it. Removed in the next major version.
     */
    has(nonce: string): Promise<boolean>;

    /**
     * Records a nonce as used.
     *
     * @param nonce The nonce to record.
     * @param dateRequested When the request carrying it was signed. An implementation
     * with its own expiry should date the entry from this rather than from now, so the
     * entry outlives the signature it guards by no more than the clock skew between the
     * two machines.
     * @deprecated Implement {@link tryAdd}; verification only calls this for a store
     * without it. Removed in the next major version.
     */
    set(nonce: string, dateRequested: Date): Promise<void>;

    /**
     * Claims a nonce for the rest of its request's validity window, if it has not been
     * claimed already. The only method verification calls on a store that has it, and
     * required in the next major version.
     *
     * An implementation must check and record in one atomic step — `SET key value NX` on
     * Redis — so two concurrent copies of a request cannot both be accepted; keep the
     * entry until at least `dateRequested + maxAgeInSeconds`, because the signature stays
     * valid until then; and return `false` rather than store an entry whose expiry has
     * already passed.
     *
     * @param nonce The nonce to claim.
     * @param dateRequested When the request carrying it was signed.
     * @param maxAgeInSeconds The validity window of the policy the request was verified
     * for. One store serves every policy, so this is per call rather than per store.
     * @returns Whether the nonce was unclaimed and is now recorded.
     */
    tryAdd?(nonce: string, dateRequested: Date, maxAgeInSeconds: number): Promise<boolean>;
}

/**
 * The outcome of claiming a nonce.
 */
export type NonceClaim = "claimed" | "replayed" | "expired";

/**
 * Claims a nonce for the rest of its request's window.
 *
 * Mirrors `INonceCache.TryAddAsync` on the .NET side. A nonce whose window has already
 * closed is reported as expired before the store is touched — the request expired while
 * its signature was computed, and some stores reject an expiry in the past. That is the
 * only expiry decision: whatever the store answers afterwards, however long it takes, a
 * refusal is a replay. A store with `tryAdd` is asked to claim the nonce atomically; one
 * with only the deprecated `has` and `set` falls back to check-then-set, where two
 * concurrent replays of the same request can both observe the nonce as unused before
 * either records it.
 */
export const claimNonce = async (
    store: NonceStore,
    nonce: string,
    dateRequested: Date,
    maxAgeInSeconds: number
): Promise<NonceClaim> => {
    if (dateRequested.getTime() + maxAgeInSeconds * 1000 <= Date.now()) {
        return "expired";
    }

    if (store.tryAdd) {
        return await store.tryAdd(nonce, dateRequested, maxAgeInSeconds) ? "claimed" : "replayed";
    }

    if (await store.has(nonce)) {
        return "replayed";
    }

    await store.set(nonce, dateRequested);
    return "claimed";
};
