export const ONLINE_THRESHOLD_MINUTES = 5;
export const ONLINE_THRESHOLD_MS = ONLINE_THRESHOLD_MINUTES * 60 * 1000;

/**
 * Printed in place of a rating (and of the counters that come inside the
 * rating object) when there is no value: the user has no rating yet, keeps it
 * hidden, or the object is absent altogether. Four screens spelled this token
 * themselves and one of them printed a dash instead, which reads as "zero" in
 * a column of numbers; the rule is one token everywhere, and this is it.
 */
export const RATING_UNAVAILABLE = "n/a";
