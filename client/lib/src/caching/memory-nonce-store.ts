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
     * How long an entry is kept, in milliseconds.
     */
    private readonly maxAgeInMilliseconds: number;

    /**
     * @param maxAgeInSeconds How long a signature stays valid. Entries are held for the
     * same span, dated from when the request was signed: a nonce that outlives its
     * signature's window guards nothing, since the request is already rejected as
     * expired. Defaults to 30 seconds, matching `Nonce.MaxAgeInSeconds` on the .NET side.
     */
    constructor(maxAgeInSeconds: number = 30) {
        this.maxAgeInMilliseconds = maxAgeInSeconds * 1000;
    }

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

    set = async (nonce: string, dateRequested: Date): Promise<void> => {
        this.evictExpired();
        this.entries.set(nonce, dateRequested.getTime() + this.maxAgeInMilliseconds);
    };

    /**
     * Drops the entries that have lapsed.
     *
     * Every entry gets the same TTL and `Map` iterates in insertion order, so the
     * expired ones are always a prefix — the sweep can stop at the first live entry
     * instead of walking the whole map, making it O(expired) rather than O(size).
     *
     * Deliberately no timer. A `setInterval` here would keep a Node process alive for as
     * long as the store existed, which is not a decision a library gets to make for its
     * host; sweeping on write costs nothing on an idle store because an idle store is
     * not being written to.
     *
     * Insertion order tracks `dateRequested`, not arrival, so a request signed earlier
     * but received later sorts out of place. It can only be *more* expired than its
     * neighbours, so the sweep stops one entry early and collects it on the next pass —
     * the entry is still never reported as a hit, because `has` re-checks.
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
