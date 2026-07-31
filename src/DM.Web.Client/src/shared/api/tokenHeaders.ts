/**
 * Headers carrying the token a token-gated endpoint treats as the caller's
 * credential: the one-time token from a mailed link, and the tracking token a
 * guest receives when filing a ticket.
 *
 * The token never goes into the URL. A path is written verbatim into the reverse
 * proxy's access log and into the request trace, so a token placed there is
 * readable by everyone who can read either, for as long as the token lives — and
 * rotating it means purging logs rather than changing a value.
 */

/** Activation, password reset, email change confirmation, username change approval. */
export const X_DM_ACCOUNT_TOKEN = "X-Dm-Account-Token";

/** Guest ticket tracking. */
export const X_DM_TICKET_TOKEN = "X-Dm-Ticket-Token";
