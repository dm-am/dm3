/**
 * @vitest-environment jsdom
 */

/**
 * "Выйти" on the account settings page left the viewer standing on it: the
 * guard only runs on navigation, and this is not one. What it must NOT do is
 * move a reader off a public page, and it must not move anyone at all when the
 * server refused to end the session.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";

const { push, currentMeta, signOutMock, signOutAllMock } = vi.hoisted(() => ({
  push: vi.fn(),
  currentMeta: { value: {} as Record<string, unknown> },
  signOutMock: vi.fn(),
  signOutAllMock: vi.fn(),
}));

vi.mock("vue-router", () => ({
  useRouter: () => ({ push }),
  useRoute: () => ({
    get meta() {
      return currentMeta.value;
    },
  }),
}));

vi.mock("@/entities/user", () => ({
  signOut: signOutMock,
  signOutAll: signOutAllMock,
}));

import { useSessionExit } from "./useSessionExit";

describe("useSessionExit", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("leaves a page that required the session", async () => {
    currentMeta.value = { requiresAuth: true };
    signOutMock.mockResolvedValue(true);

    await useSessionExit().signOut();

    expect(push).toHaveBeenCalledWith({ name: "home" });
  });

  it("stays put on a page a guest may read", async () => {
    currentMeta.value = {};
    signOutMock.mockResolvedValue(true);

    await useSessionExit().signOut();

    expect(push).not.toHaveBeenCalled();
  });

  it("stays put when the server refused to end the session", async () => {
    currentMeta.value = { requiresAuth: true };
    signOutAllMock.mockResolvedValue(false);

    await useSessionExit().signOutAll();

    expect(push).not.toHaveBeenCalled();
  });
});
