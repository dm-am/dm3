/**
 * How long after their last visit a user is still shown as online.
 *
 * The server keeps the same rule in ActivityPolicy.cs, as OnlinePeriod, and
 * orders user lists and the "active" filters by it. This copy exists because
 * the browser recomputes the dot on its own clock and has nobody to ask. The
 * spec next to this file holds the two numbers equal: raising one alone is how
 * the server comes to sort by ten minutes while the dot goes out after five.
 */
export const ONLINE_THRESHOLD_MINUTES = 5;
export const ONLINE_THRESHOLD_MS = ONLINE_THRESHOLD_MINUTES * 60 * 1000;
