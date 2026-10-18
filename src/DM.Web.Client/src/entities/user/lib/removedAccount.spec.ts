/**
 * @vitest-environment node
 */

/**
 * Which names open the profile of "Аккаунт удален" as that profile.
 *
 * The same question the server answers (RemovedAccount.Is), asked by the page
 * to decide what not to request. A spelling the server takes for the name and
 * the page does not would draw the profile of the summary with the actions of
 * an account and ask five endpoints that refuse it; the other way round, a
 * lookalike the server answers as nobody would lose its "не найдено".
 */
import { describe, it, expect } from "vitest";
import {
  REMOVED_ACCOUNT_NAME,
  isRemovedAccountName,
  removedAccountRoute,
} from "./removedAccount";

/** The "е" with two dots, spelled as its code point as the sources require. */
const DOTTED = "\u0451";
const DOTTED_CAPITAL = "\u0401";

describe("the name of a removed account", () => {
  it.each([
    REMOVED_ACCOUNT_NAME,
    "Аккаунт Удален",
    "АККАУНТ УДАЛЕН",
    `Аккаунт удал${DOTTED}н`,
    `АККАУНТ УДАЛ${DOTTED_CAPITAL}Н`,
    "  Аккаунт удален ",
  ])("is %j", (name) => {
    expect(isRemovedAccountName(name)).toBe(true);
  });

  it.each([
    // A Latin "A": reserved on the server, but nobody's profile.
    "Aккаунт удален",
    "Аккаунт_удален",
    "Аккаунтудален",
    "Аккаунт удален2",
    "",
    null,
    undefined,
  ])("is not %j", (name) => {
    expect(isRemovedAccountName(name)).toBe(false);
  });

  it("leads to the profile page of the name", () => {
    expect(removedAccountRoute).toEqual({
      name: "profile",
      params: { username: "Аккаунт удален" },
    });
  });
});
