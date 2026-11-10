/**
 * @vitest-environment jsdom
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent } from "vue";
import { flushPromises } from "@vue/test-utils";
import type { Router } from "vue-router";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";

const { signOutMock, signOutAllMock } = vi.hoisted(() => ({
  signOutMock: vi.fn(),
  signOutAllMock: vi.fn(),
}));

vi.mock("@/entities/user", () => ({
  signOut: signOutMock,
  signOutAll: signOutAllMock,
}));

import { useSessionExit } from "./useSessionExit";

const routes: Address[] = [
  ["home", "/"],
  ["notifications", "/notifications", { meta: { requiresAuth: true } }],
  ["rules", "/rules"],
];

/** The exits of a page mounted at the address, and the router it stands on. */
async function exitsAt(address: string) {
  let exits!: ReturnType<typeof useSessionExit>;
  const Shell = defineComponent({
    setup() {
      exits = useSessionExit();
      return () => null;
    },
  });
  const { router } = await mountPage(Shell, address, { routes });
  return { exits, router };
}

const address = (router: Router) => router.currentRoute.value.fullPath;

describe("useSessionExit", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("leaves a page that required the session", async () => {
    signOutMock.mockResolvedValue(true);
    const { exits, router } = await exitsAt("/notifications");

    await exits.signOut();
    await flushPromises();

    expect(address(router)).toBe("/");
  });

  it("stays put on a page a guest may read", async () => {
    signOutMock.mockResolvedValue(true);
    const { exits, router } = await exitsAt("/rules");

    await exits.signOut();
    await flushPromises();

    expect(address(router)).toBe("/rules");
  });

  it("stays put when the server refused to end the session", async () => {
    signOutAllMock.mockResolvedValue(false);
    const { exits, router } = await exitsAt("/notifications");

    await exits.signOutAll();
    await flushPromises();

    expect(address(router)).toBe("/notifications");
  });
});
