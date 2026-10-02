import NonceStore from "./nonce-store";

/**
 * An in-process {@link NonceStore}, holding each nonce for as long as a signature made
 * with it could still verify.
 *
 * The counterpart to `MemoryNonceCache` on the .NET side, and the default when nothing
 * else is supplied.
 *
 * **Per-process.** Two replicas do not share what they have seen, so a replay that
 * lands on a different instance than the original is not detected. Anything running
 * more than one verifier for a policy wants a shared store behind the same interface.
 */
export default class MemoryNonceStore implements NonceStore {
    /**
     * Nonce to the moment it stops being worth remembering.
     *
     * A `Map` and not a plain object: insertion order is specified for `Map`, which is
     * what makes the sweep below cheap, and nonce strings would otherwise collide with
     * `Object.prototype` keys.
     */
    private readonly entries = new Map<string, number>();

    /**
     * How long an entry recorded through the deprecated {@link set} is kept, in
     * milliseconds. {@link tryAdd} is told each policy's window per call instead.
     */
    private readonly maxAgeInMilliseconds: number;

    /**
     * @param maxAgeInSeconds How long an entry recorded through the deprecated
     * {@link set} is kept, dated from when the request was signed. Defaults to 30
     * seconds, matching `Nonce.MaxAgeInSeconds` on the .NET side. Entries claimed with
     * {@link tryAdd} use the window passed with them.
     */
    constructor(maxAgeInSeconds: number = 30) {
        this.maxAgeInMilliseconds = maxAgeInSeconds * 1000;
    }

    /**
     * @deprecated Use {@link tryAdd}. Removed in the next major version.
     */
    has = async (nonce: string): Promise<boolean> => {
        const expiresAt = this.entries.get(nonce);
        if (expiresAt === undefined) {
            return false;
        }

        // Checked on read as well as swept on write, so an expired entry is never
        // reported as a hit even if nothing has been written since it lapsed.
        if (expiresAt <= Date.now()) {
            this.entries.delete(nonce);
            return false;
        }

        return true;
    };

    /**
     * @deprecated Use {@link tryAdd}. Removed in the next major version.
     */
    set = async (nonce: string, dateRequested: Date): Promise<void> => {
        this.record(nonce, dateRequested.getTime() + this.maxAgeInMilliseconds);
    };

    /**
     * Checks and records the nonce with no `await` in between, so no other call can
     * interleave on the event loop.
     *
     * @param maxAgeInSeconds The window of the policy the request was verified for.
     * Optional only so a caller written against the two-argument form keeps compiling;
     * without it, the constructor's window applies.
     */
    tryAdd = async (nonce: string, dateRequested: Date, maxAgeInSeconds?: number): Promise<boolean> => {
        const now = Date.now();
        const expiresAt = dateRequested.getTime() +
            (maxAgeInSeconds === undefined ? this.maxAgeInMilliseconds : maxAgeInSeconds * 1000);
        if (expiresAt <= now) {
            return false;
        }

        const existing = this.entries.get(nonce);
        if (existing !== undefined && existing > now) {
            return false;
        }

        this.record(nonce, expiresAt);
        return true;
    };

    /**
     * Records an entry at the end of the map. Deleted first because `Map.set` on a key
     * already present keeps its original position, and an entry the sweep has not reached
     * yet would otherwise sit early in the order with a late expiry, stopping every sweep.
     */
    private record(nonce: string, expiresAt: number): void {
        this.evictExpired();
        this.entries.delete(nonce);
        this.entries.set(nonce, expiresAt);
    }

    /**
     * Drops the entries that have lapsed.
     *
     * `Map` iterates in insertion order, which roughly tracks expiry, so the sweep
     * stops at the first live entry instead of walking the whole map — O(expired) rather
     * than O(size).
     *
     * Deliberately no timer. A `setInterval` here would keep a Node process alive for as
     * long as the store existed, which is not a decision a library gets to make for its
     * host; sweeping on write costs nothing on an idle store because an idle store is
     * not being written to.
     *
     * Only roughly, though. A request signed earlier but received later sorts out of
     * place, and so does an entry from a policy with a shorter window than its
     * neighbours. Either way the sweep stops early and the entry waits for a later pass,
     * held no longer than the longest window in use — and it is never reported as a hit
     * meanwhile, because every read re-checks the expiry.
     */
    private evictExpired(): void {
        const now = Date.now();
        for (const [nonce, expiresAt] of this.entries) {
            if (expiresAt > now) {
                break;
            }

            this.entries.delete(nonce);
        }
    }

    /**
     * How many nonces are currently held. Intended for tests and diagnostics.
     */
    get size(): number {
        return this.entries.size;
    }
}
