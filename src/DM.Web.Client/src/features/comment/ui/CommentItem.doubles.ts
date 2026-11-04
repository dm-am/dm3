import { vi } from "vitest";

export const route = {
  query: {} as Record<string, string>,
};

export const vueRouter = () => ({
  useRoute: () => route,
  RouterLink: { template: "<a><slot /></a>" },
});

export const submitEdit = vi.fn();
export const submitDelete = vi.fn();
export const submitLike = vi.fn();
export const submitUnlike = vi.fn();
