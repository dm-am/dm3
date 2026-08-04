/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const { replace, currentRoute, setSessionExpiredHandler } = vi.hoisted(() => ({
  replace: vi.fn(),
  currentRoute: { value: { meta: {}, fullPath: "/" } as Record<string, any> },
  setSessionExpiredHandler: vi.fn(),
}));

// Navigation is not what this asserts, and a real push would pull every lazy
// route component into the test.
vi.mock("./router", () => ({
  default: { replace, currentRoute },
  extractNumberParam: vi.fn(),
}));

vi.mock("@/shared/api", () => ({
  setSessionExpiredHandler,
}));

import { useAuthStore } from "@/shared/stores";
import {
  endExpiredSession,
  installSessionExpiredHandler,
} from "./sessionExpiry";

/**
 * The 401 interceptor used to clear only the persisted copy of the viewer. The
 * store kept its own, so the header, the sidebar blocks and every action button
 * went on rendering as signed in while the route guard bounced the same viewer
 * to the login modal — and it stayed that way until a full page reload.
 */
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
