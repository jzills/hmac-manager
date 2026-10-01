import { assert, test, vi } from "vitest";
import MemoryNonceStore from "../src/caching/memory-nonce-store";
import NonceStore, { isValidNonce } from "../src/caching/nonce-store";

const nonce = (suffix: string) => `00000000-0000-0000-0000-${suffix.padStart(12, "0")}`;

test("MemoryNonceStore_Has_Is_False_For_An_Unseen_Nonce", async () => {
    const store = new MemoryNonceStore();

    assert.isFalse(await store.has(nonce("1")));
});

test("MemoryNonceStore_Has_Is_True_For_A_Recorded_Nonce", async () => {
    const store = new MemoryNonceStore();
    await store.set(nonce("1"), new Date());

    assert.isTrue(await store.has(nonce("1")));
});

test("MemoryNonceStore_Forgets_A_Nonce_Once_Its_Window_Has_Passed", async () => {
    const store = new MemoryNonceStore(30);

    // Dated from when the request was signed, not from now: an entry that outlived its
    // signature's window would guard nothing, since the request is already rejected as
    // expired before the nonce is ever consulted.
    await store.set(nonce("1"), new Date(Date.now() - 31_000));

    assert.isFalse(await store.has(nonce("1")));
});

test("MemoryNonceStore_Expires_On_Read_Without_Waiting_For_A_Write", async () => {
    const store = new MemoryNonceStore(30);
    await store.set(nonce("1"), new Date(Date.now() - 31_000));

    await store.has(nonce("1"));

    assert.equal(store.size, 0);
});

test("MemoryNonceStore_Evicts_Lapsed_Entries_On_Write", async () => {
    const store = new MemoryNonceStore(30);

    for (let index = 0; index < 5; index++) {
        await store.set(nonce(index.toString()), new Date(Date.now() - 31_000));
    }

    assert.isAbove(store.size, 0);

    await store.set(nonce("live"), new Date());

    // The lapsed entries are gone and only the live one remains — without this the store
    // grows for as long as the process runs.
    assert.equal(store.size, 1);
});

test("MemoryNonceStore_Keeps_Entries_That_Are_Still_Live", async () => {
    const store = new MemoryNonceStore(30);
    await store.set(nonce("1"), new Date());
    await store.set(nonce("2"), new Date());

    assert.equal(store.size, 2);
    assert.isTrue(await store.has(nonce("1")));
    assert.isTrue(await store.has(nonce("2")));
});

test("MemoryNonceStore_TryAdd_Is_True_Then_False", async () => {
    const store = new MemoryNonceStore();

    assert.isTrue(await store.tryAdd(nonce("1"), new Date()));
    assert.isFalse(await store.tryAdd(nonce("1"), new Date()));
});

test("MemoryNonceStore_TryAdd_Accepts_A_Nonce_Whose_Entry_Has_Expired", async () => {
    const store = new MemoryNonceStore(30);
    await store.set(nonce("1"), new Date(Date.now() - 31_000));

    assert.isTrue(await store.tryAdd(nonce("1"), new Date()));
    assert.isTrue(await store.has(nonce("1")));
});

test("IsValidNonce_Claims_A_Nonce_On_First_Use_And_Rejects_The_Second", async () => {
    const store = new MemoryNonceStore();

    assert.isTrue(await isValidNonce(store, nonce("1"), new Date(), 30));
    assert.isFalse(await isValidNonce(store, nonce("1"), new Date(), 30));
});

test("IsValidNonce_Treats_Distinct_Nonces_Independently", async () => {
    const store = new MemoryNonceStore();

    assert.isTrue(await isValidNonce(store, nonce("1"), new Date(), 30));
    assert.isTrue(await isValidNonce(store, nonce("2"), new Date(), 30));
});

test("IsValidNonce_Works_With_A_Store_That_Has_Only_Has_And_Set", async () => {
    const entries = new Set<string>();
    const store: NonceStore = {
        has: async (value) => entries.has(value),
        set: async (value) => { entries.add(value); }
    };

    assert.isTrue(await isValidNonce(store, nonce("1"), new Date(), 30));
    assert.isFalse(await isValidNonce(store, nonce("1"), new Date(), 30));
});

test("MemoryNonceStore_TryAdd_Holds_A_Nonce_For_The_Window_It_Is_Given", async () => {
    // The constructor's 30 seconds would have expired this entry already; the policy's 60 have not.
    const store = new MemoryNonceStore(30);
    const signed = new Date(Date.now() - 40_000);

    assert.isTrue(await store.tryAdd(nonce("1"), signed, 60));
    assert.isFalse(await store.tryAdd(nonce("1"), signed, 60));
});

test("MemoryNonceStore_TryAdd_Refuses_A_Nonce_Whose_Window_Has_Closed", async () => {
    const store = new MemoryNonceStore();

    assert.isFalse(await store.tryAdd(nonce("1"), new Date(Date.now() - 31_000), 30));
    assert.equal(store.size, 0);
});

test("MemoryNonceStore_TryAdd_Moves_A_Reclaimed_Nonce_To_The_End", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    try {
        const start = new Date("2026-01-01T00:00:00Z");
        vi.setSystemTime(start);
        const store = new MemoryNonceStore();
        const keys = () => [...(store as unknown as { entries: Map<string, number> }).entries.keys()];

        await store.tryAdd(nonce("a"), start, 60);
        await store.tryAdd(nonce("lapsed"), start, 10);
        await store.tryAdd(nonce("b"), start, 60);

        // "lapsed" has expired but sits behind a live entry, so no sweep has reached it.
        vi.setSystemTime(start.getTime() + 20_000);
        assert.isTrue(await store.tryAdd(nonce("lapsed"), new Date(), 10));

        // Left in place, its new, later expiry would sit ahead of "b" in the order.
        assert.deepEqual(keys(), [nonce("a"), nonce("b"), nonce("lapsed")]);
    } finally {
        vi.useRealTimers();
    }
});

test("IsValidNonce_Passes_The_Policy_Window_To_TryAdd", async () => {
    const tryAdd = vi.fn(async () => true);
    const store: NonceStore = { has: async () => false, set: async () => {}, tryAdd };
    const signed = new Date();

    assert.isTrue(await isValidNonce(store, nonce("1"), signed, 42));
    assert.deepEqual(tryAdd.mock.calls, [[nonce("1"), signed, 42]]);
});

test("IsValidNonce_Refuses_A_Lapsed_Nonce_Without_Touching_The_Store", async () => {
    const store = {
        has: vi.fn(async () => false),
        set: vi.fn(async () => {}),
        tryAdd: vi.fn(async () => true)
    };
    const legacy = { has: vi.fn(async () => false), set: vi.fn(async () => {}) };
    const lapsed = new Date(Date.now() - 31_000);

    assert.isFalse(await isValidNonce(store, nonce("1"), lapsed, 30));
    assert.isFalse(await isValidNonce(legacy, nonce("1"), lapsed, 30));
    assert.equal(store.tryAdd.mock.calls.length + store.has.mock.calls.length + store.set.mock.calls.length, 0);
    assert.equal(legacy.has.mock.calls.length + legacy.set.mock.calls.length, 0);
});
