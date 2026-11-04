/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { RouteLocationNormalized } from "vue-router";

const { replace, currentRoute, setSessionExpiredHandler } = vi.hoisted(() => ({
  replace: vi.fn(),
  currentRoute: {
    value: { meta: {}, fullPath: "/" } as Pick<
      RouteLocationNormalized,
      "meta" | "fullPath"
    >,
  },
  setSessionExpiredHandler: vi.fn(),
}));

// A real push would pull every lazy route component into the test.
vi.mock("./router", () => ({
  default: { replace, currentRoute },
}));

vi.mock("@/shared/api", () => ({
  setSessionExpiredHandler,
}));

import { effectScope, ref } from "vue";
import { useAuthStore } from "@/shared/stores";
import {
  openWindowCount,
  useOpenWindow,
} from "@/shared/lib/composables/useOpenWindows";
import {
  endExpiredSession,
  installSessionExpiredHandler,
} from "./sessionExpiry";

describe("endExpiredSession", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  function signedIn() {
    localStorage.setItem(
      "user",
      JSON.stringify({ id: "user-1", username: "SolohinLex" }),
    );
    const auth = useAuthStore();
    expect(auth.isAuthenticated).toBe(true);
    return auth;
  }

  it("drops the viewer from the store the interface reads", () => {
    currentRoute.value = { meta: {}, fullPath: "/forum/flood/12" };
    const auth = signedIn();

    endExpiredSession();

    expect(auth.isAuthenticated).toBe(false);
    // One writer: updateUser clears the persisted copy as well, which is what
    // logs the other tabs out through the `storage` event.
    expect(localStorage.getItem("user")).toBeNull();
  });

  it("leaves a reader of a public page exactly where they were", () => {
    // Any 401 from any request lands here, including a background one. Moving
    // the reader took whatever they had typed in a field without a draft-key.
    currentRoute.value = { meta: {}, fullPath: "/forum/flood/12" };
    signedIn();

    endExpiredSession();

    expect(replace).not.toHaveBeenCalled();
  });

  it("leaves a protected page carrying the address back", () => {
    currentRoute.value = {
      meta: { requiresAuth: true },
      fullPath: "/messenger/c/42",
    };
    signedIn();

    endExpiredSession();

    expect(replace).toHaveBeenCalledWith({
      name: "home",
      query: { action: "login", redirect: "/messenger/c/42" },
    });
  });

  it("is what the HTTP client is told to call", () => {
    installSessionExpiredHandler();

    expect(setSessionExpiredHandler).toHaveBeenCalledWith(endExpiredSession);
  });
});

/**
 * The windows over the page close with the session, each its own way
 * (Dialog.vue, useDialogShell); the guest windows do not.
 */
describe("the windows at the end of a session", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("closes every open window, each its own way", () => {
    currentRoute.value = { meta: {}, fullPath: "/forum/flood/12" };
    const scope = effectScope();
    const closed: string[] = [];
    const shown = { confirm: ref(true), form: ref(true), gone: ref(false) };
    scope.run(() => {
      useOpenWindow(shown.confirm, () => closed.push("confirm"));
      useOpenWindow(shown.form, () => closed.push("form"));
      useOpenWindow(shown.gone, () => closed.push("gone"));
    });

    endExpiredSession();

    expect(closed.sort()).toEqual(["confirm", "form"]);
    expect(openWindowCount()).toBe(0);
    scope.stop();
  });

  it("asks a window once, however many requests the session ends on", () => {
    currentRoute.value = { meta: {}, fullPath: "/" };
    const scope = effectScope();
    let asked = 0;
    scope.run(() => useOpenWindow(ref(true), () => asked++));

    endExpiredSession();
    endExpiredSession();

    expect(asked).toBe(1);
    scope.stop();
  });
});
