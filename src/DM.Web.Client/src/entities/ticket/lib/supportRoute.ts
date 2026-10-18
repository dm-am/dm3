import type { TicketSubtype } from "../api";

/**
 * The support form with the reason already chosen.
 *
 * A card or a page that runs out of ways on sends the reader to support, and
 * the reason is always known by the one that sends him: the sign-in card knows
 * the account has to be brought back, the recovery card knows the mailbox is
 * out of reach, the activation page knows the registration did not finish.
 * Naming it in the address spares the reader a picker he would open on the
 * answer the previous screen already had.
 *
 * The value is the member of TicketSubtype and not a word of its own, so the
 * form reads it without a second vocabulary in between, and a renamed category
 * fails to compile here instead of quietly selecting nothing. It lives with the
 * ticket entity because both a feature (the guest cards) and pages (the pages a
 * letter opens) send readers here, and neither may import the other.
 */
export const supportFor = (subtype: TicketSubtype) =>
  `/support?subtype=${subtype}`;
