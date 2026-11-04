import type { Page } from "@playwright/test";

/**
 * Reading in the page: several functions run in the page as one, at one
 * moment, each on the same argument.
 *
 * A function given to page.evaluate sees no outside names: Playwright sends
 * its text, so a long reader cannot be split into functions that call each
 * other. Separate calls would read the page at separate moments, and a reader
 * that changes the page for a moment (fixtures/tables.ts sets a table at no
 * width and puts it back) has to do it inside one synchronous read. So the
 * readers are sent as text the same way, and run in order in one expression.
 */

/** A function run in the page: no outside names, its argument plain JSON. */
export type InPageReader<A, R> = (arg: A) => R;

/** The answers of the readers, in their order, read at one moment. */
export function readInPage<A, R extends unknown[] | []>(
  page: Page,
  readers: { [K in keyof R]: InPageReader<A, R[K]> },
  arg: A,
): Promise<R> {
  const calls = (readers as InPageReader<A, unknown>[])
    .map((reader) => `(${reader.toString()})(arg)`)
    .join(",\n");
  return page.evaluate<R>(`((arg) => [${calls}])(${JSON.stringify(arg)})`);
}
