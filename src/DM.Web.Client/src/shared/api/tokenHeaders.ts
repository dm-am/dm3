/**
 * The token of a token-gated endpoint goes in a header, never in the URL: a
 * path is written into the proxy's access log and the trace.
 */

/** Activation, password reset, email change confirmation, username change approval. */
export const X_DM_ACCOUNT_TOKEN = "X-Dm-Account-Token";

/** Guest ticket tracking. */
export const X_DM_TICKET_TOKEN = "X-Dm-Ticket-Token";
