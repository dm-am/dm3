/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const { push, setSessionExpiredHandler } = vi.hoisted(() => ({
  push: vi.fn(),
  setSessionExpiredHandler: vi.fn(),
}));

// Navigation is not what this asserts, and a real push would pull every lazy
// route component into the test.
vi.mock("./router", () => ({
  default: { push },
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

  it("drops the viewer from the store the interface reads", () => {
    localStorage.setItem(
      "user",
      JSON.stringify({ id: "user-1", username: "SolohinLex" }),
    );
    const auth = useAuthStore();
    expect(auth.isAuthenticated).toBe(true);

    endExpiredSession();

    expect(auth.isAuthenticated).toBe(false);
    // One writer: updateUser clears the persisted copy as well, which is what
    // logs the other tabs out through the `storage` event.
    expect(localStorage.getItem("user")).toBeNull();
    expect(push).toHaveBeenCalledWith({ name: "home" });
  });

  it("is what the HTTP client is told to call", () => {
    installSessionExpiredHandler();

    expect(setSessionExpiredHandler).toHaveBeenCalledWith(endExpiredSession);
  });
});
