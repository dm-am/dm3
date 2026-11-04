import { vi } from "vitest";

/** Where a guest page sends the reader: every way out of its card is a push. */
export const push = vi.fn();

/** vue-router as the guest pages use it, with the push above. */
export const vueRouter = () => ({ useRouter: () => ({ push }) });
