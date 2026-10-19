import { vi } from "vitest";

export const routed = { push: vi.fn(), replace: vi.fn() };

export const vueRouter = async (
  importOriginal: () => Promise<typeof import("vue-router")>,
) => ({
  ...(await importOriginal()),
  useRoute: () => ({
    params: { id: "the-game", characterId: "char-1" },
    query: {},
  }),
  useRouter: () => ({ push: routed.push, replace: routed.replace }),
});
