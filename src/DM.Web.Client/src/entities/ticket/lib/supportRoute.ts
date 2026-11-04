import type { TicketSubtype } from "../api";

/** The support form with the reason the sending screen already knows. */
export const supportFor = (subtype: TicketSubtype) =>
  `/support?subtype=${subtype}`;
