/**
 * @vitest-environment jsdom
 */

/**
 * The cross-tab listener is the only thing this store registers outside itself.
 * A subscription that outlives the store is invisible in the app — one store
 * per tab — and shows up in the suite, where every case builds a fresh pinia on
 * the same jsdom window: each dead listener keeps answering `storage` events
 * through the ui store of a case that is already over.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { User } from "@/shared/api/models/common/user";
import { useAuthStore } from "./auth";

describe("useAuthStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it("takes its storage listener off the window when the store is disposed", () => {
    const added = vi.spyOn(window, "addEventListener");
    const removed = vi.spyOn(window, "removeEventListener");

    const store = useAuthStore();

    const registrations = added.mock.calls.filter(
      ([type]) => type === "storage",
    );
    expect(registrations).toHaveLength(1);

    store.$dispose();

    const removals = removed.mock.calls.filter(([type]) => type === "storage");
    expect(removals).toHaveLength(1);
    // The same function object, or the browser keeps the subscription.
    expect(removals[0][1]).toBe(registrations[0][1]);
  });

  it("stops answering storage events once the store is disposed", () => {
    const store = useAuthStore();
    store.updateUser({ username: "reader" } as unknown as User);
    expect(store.isAuthenticated).toBe(true);

    store.$dispose();
    localStorage.removeItem("user");
    window.dispatchEvent(
      new StorageEvent("storage", { key: "user", newValue: null }),
    );

    expect(store.user).not.toBeNull();
  });
});
