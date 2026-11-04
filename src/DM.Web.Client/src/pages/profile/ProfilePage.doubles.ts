import { vi } from "vitest";

/** The address the profile is open at; a test names the profile in it. */
export const route = {
  params: { username: "Other" },
  query: {} as Record<string, string>,
};

/** vue-router with the address above, and a router that goes nowhere. */
export const vueRouter = async (
  importOriginal: () => Promise<typeof import("vue-router")>,
) => ({
  ...(await importOriginal()),
  useRoute: () => route,
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
});
