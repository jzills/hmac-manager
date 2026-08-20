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
     */
    set(nonce: string, dateRequested: Date): Promise<void>;
}

/**
 * Checks a nonce and claims it in one step, returning whether it was unused.
 *
 * Mirrors `INonceCacheExtensions.IsValidNonceAsync`. Note that this is check-then-set
 * and not atomic: two concurrent replays of the same request can both observe the nonce
 * as unused before either records it. Closing that needs an atomic primitive from the
 * underlying store — `SET key value NX` on Redis — which is why the operation lives
 * here as a default rather than in the interface. An implementation that can do better
 * should, and the .NET library has the same gap in the same place.
 */
export const isValidNonce = async (
    store: NonceStore,
    nonce: string,
    dateRequested: Date
): Promise<boolean> => {
    if (await store.has(nonce)) {
        return false;
    }

    await store.set(nonce, dateRequested);
    return true;
};
