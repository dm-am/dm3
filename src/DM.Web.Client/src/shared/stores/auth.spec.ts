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
import { Theme } from "@/shared/api/models/personal";
import { useAuthStore } from "./auth";
import { useUiStore } from "./ui";

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

/**
 * The theme belongs to the device, and this store is where that was lost: it
 * answered every appearance of a viewer with the theme saved on the account —
 * at boot, at every `fetchUser`, on the sign-in of another tab — and the ui
 * store persists what it is handed. A switch made in the settings panel
 * therefore survived no refresh of the viewer.
 */
describe("useAuthStore and the theme", () => {
  const THEME_KEY = "dm_theme";
  const registered: ReturnType<typeof useAuthStore>[] = [];

  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
  });

  afterEach(() => {
    // Disposed rather than abandoned, for the reason at the top of this file:
    // these cases answer `storage` events, and a listener left on the window
    // would answer them again in every case that follows.
    while (registered.length) registered.pop()?.$dispose();
    localStorage.clear();
  });

  /** An auth store that gives its storage listener back after the case. */
  function authStore() {
    const store = useAuthStore();
    registered.push(store);
    return store;
  }

  function readerWithTheme(theme: Theme): User {
    return { username: "reader", settings: { theme } } as unknown as User;
  }

  it("takes the account theme on a device that has never chosen", () => {
    const auth = authStore();

    auth.updateUser(readerWithTheme(Theme.Dark));

    expect(useUiStore().theme).toBe(Theme.Dark);
    expect(localStorage.getItem(THEME_KEY)).toBe(Theme.Dark);
  });

  it("starts from the account theme of the viewer it boots with", () => {
    localStorage.setItem("user", JSON.stringify(readerWithTheme(Theme.Dark)));

    authStore();

    expect(useUiStore().theme).toBe(Theme.Dark);
  });

  it("leaves the device choice alone when the viewer is refreshed", () => {
    localStorage.setItem(THEME_KEY, Theme.Dark);
    const auth = authStore();

    auth.updateUser(readerWithTheme(Theme.Light));

    expect(useUiStore().theme).toBe(Theme.Dark);
    expect(localStorage.getItem(THEME_KEY)).toBe(Theme.Dark);
  });

  it("keeps the choice of the device when another tab signs in", () => {
    localStorage.setItem(THEME_KEY, Theme.Dark);
    const auth = authStore();
    const signedIn = JSON.stringify(readerWithTheme(Theme.Light));

    localStorage.setItem("user", signedIn);
    window.dispatchEvent(
      new StorageEvent("storage", { key: "user", newValue: signedIn }),
    );

    expect(auth.user).not.toBeNull();
    expect(useUiStore().theme).toBe(Theme.Dark);
    expect(localStorage.getItem(THEME_KEY)).toBe(Theme.Dark);
  });

  it("does not touch the theme when the viewer signs out", () => {
    localStorage.setItem(THEME_KEY, Theme.Dark);
    const auth = authStore();
    auth.updateUser(readerWithTheme(Theme.Light));

    auth.updateUser(null);

    expect(auth.user).toBeNull();
    expect(useUiStore().theme).toBe(Theme.Dark);
    expect(localStorage.getItem(THEME_KEY)).toBe(Theme.Dark);
  });

  it("does not touch the theme when another tab signs out", () => {
    localStorage.setItem(THEME_KEY, Theme.Dark);
    const auth = authStore();
    auth.updateUser(readerWithTheme(Theme.Light));

    localStorage.removeItem("user");
    window.dispatchEvent(
      new StorageEvent("storage", { key: "user", newValue: null }),
    );

    expect(auth.user).toBeNull();
    expect(useUiStore().theme).toBe(Theme.Dark);
    expect(localStorage.getItem(THEME_KEY)).toBe(Theme.Dark);
  });
});
