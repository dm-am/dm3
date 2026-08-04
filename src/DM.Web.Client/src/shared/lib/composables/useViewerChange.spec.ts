/**
 * @vitest-environment jsdom
 */

/**
 * The case the six hand-written conditions all missed: a second tab signs a
 * different account in, and the username goes from A to B without passing
 * through undefined.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { useViewerChange } from "./useViewerChange";
import type { User } from "@/shared/api/models/common/user";

const viewer = (username: string) => ({ id: username, username }) as User;

describe("useViewerChange", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
  });

  it("runs when one account replaces another without a sign-out", async () => {
    const auth = useAuthStore();
    auth.updateUser(viewer("a"));

    const handler = vi.fn();
    useViewerChange(handler);

    auth.updateUser(viewer("b"));
    await nextTick();

    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler).toHaveBeenCalledWith("b");
  });

  it("runs on sign-in and on sign-out", async () => {
    const auth = useAuthStore();
    const handler = vi.fn();
    useViewerChange(handler);

    auth.updateUser(viewer("a"));
    await nextTick();
    auth.updateUser(null);
    await nextTick();

    expect(handler.mock.calls).toEqual([["a"], [undefined]]);
  });

  it("stays quiet while the same viewer is updated", async () => {
    const auth = useAuthStore();
    auth.updateUser(viewer("a"));

    const handler = vi.fn();
    useViewerChange(handler);

    auth.updateUser({ ...viewer("a"), status: "у камина" });
    await nextTick();

    expect(handler).not.toHaveBeenCalled();
  });
});
