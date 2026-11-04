/** The router and the filing of the support pages' specs, handed to vi.mock. */
import { vi } from "vitest";

/** The address the pages are open at; nothing else on them touches the router. */
export const route = { query: {} as Record<string, string> };

/** The module vue-router stands for. */
export const vueRouter = () => ({
  useRoute: () => route,
  useRouter: () => ({
    push: vi.fn(),
    resolve: (to: { hash: string }) => ({
      href: `/support/track${to.hash}`,
    }),
  }),
});

// A picker's DOM value can be written from the outside; the submitted payload
// cannot, so that is where what the form sends is checked.
export const createTicket = vi.fn();

/** The module @/entities/ticket stands for: itself, but for the filing. */
export const ticketEntity = async (
  importOriginal: () => Promise<typeof import("@/entities/ticket")>,
) => ({
  ...(await importOriginal()),
  ticketApi: { createTicket },
});
