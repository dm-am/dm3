/**
 * Printed wherever a value is missing: a rating its owner keeps hidden, a board
 * without a description, a date that never happened, a counter the server did
 * not return, a column that does not apply to this row.
 *
 * One token for all of them rather than one per shade of nothing. The dash it
 * replaced reads as a zero in a column of numbers, it was spelled out in
 * nineteen places, and four more screens wrote this token by hand; a reader who
 * has learned the sign once should not have to learn it again per table.
 */
export const VALUE_UNAVAILABLE = "n/a";
