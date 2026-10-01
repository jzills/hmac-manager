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

    /**
     * Records a nonce as used only if it has not been seen before, as one atomic step.
     *
     * @param nonce The nonce to record.
     * @param dateRequested When the request carrying it was signed, as for {@link set}.
     * @returns Whether the nonce was unused and is now recorded.
     */
    tryAdd?(nonce: string, dateRequested: Date): Promise<boolean>;
}

/**
 * Checks a nonce and claims it in one step, returning whether it was unused.
 *
 * Mirrors `INonceCacheExtensions.IsValidNonceAsync`. Uses the store's `tryAdd` when it
 * has one, which is atomic: {@link MemoryNonceStore} implements it, and a shared store
 * should implement it with its own atomic primitive — `SET key value NX` on Redis. A
 * store with only `has` and `set` falls back to check-then-set, where two concurrent
 * replays of the same request can both observe the nonce as unused before either
 * records it.
 */
export const isValidNonce = async (
    store: NonceStore,
    nonce: string,
    dateRequested: Date
): Promise<boolean> => {
    if (store.tryAdd) {
        return store.tryAdd(nonce, dateRequested);
    }

    if (await store.has(nonce)) {
        return false;
    }

    await store.set(nonce, dateRequested);
    return true;
};
