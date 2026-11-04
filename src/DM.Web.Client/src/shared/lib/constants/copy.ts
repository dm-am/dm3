/**
 * Printed wherever a value is missing, one token for every kind of nothing: a
 * dash reads as a zero in a column of numbers.
 */
export const VALUE_UNAVAILABLE = "n/a";

/** Printed instead of a table that has no rows, hand-rolled tables included. */
export const NOTHING_TO_SHOW = "Нет данных";

/**
 * Printed when a search or a filter typed by the reader matched nothing; a
 * table with no rows and no query says NOTHING_TO_SHOW instead.
 */
export const NOTHING_FOUND = "Ничего не найдено";
