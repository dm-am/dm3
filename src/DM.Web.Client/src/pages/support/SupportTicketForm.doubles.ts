/** The filing of the support pages' specs, handed to vi.mock. */
import { vi } from "vitest";

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
