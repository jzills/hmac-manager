import { assert, test } from "vitest";
import MemoryNonceStore from "../src/caching/memory-nonce-store";
import { isValidNonce } from "../src/caching/nonce-store";

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

test("IsValidNonce_Claims_A_Nonce_On_First_Use_And_Rejects_The_Second", async () => {
    const store = new MemoryNonceStore();

    assert.isTrue(await isValidNonce(store, nonce("1"), new Date()));
    assert.isFalse(await isValidNonce(store, nonce("1"), new Date()));
});

test("IsValidNonce_Treats_Distinct_Nonces_Independently", async () => {
    const store = new MemoryNonceStore();

    assert.isTrue(await isValidNonce(store, nonce("1"), new Date()));
    assert.isTrue(await isValidNonce(store, nonce("2"), new Date()));
});
