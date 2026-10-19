import type { RouteLocationRaw } from "vue-router";

// Not an account: every null author is signed so, and the profile of the name gathers them all.
export const REMOVED_ACCOUNT_NAME = "Аккаунт удален";

/** The lower-case "е" with two dots, U+0451, which the sources never spell. */
const DOTTED_E = /\u0451/g;

const fold = (value: string) => value.toLowerCase().replace(DOTTED_E, "е");

const FOLDED_NAME = fold(REMOVED_ACCOUNT_NAME);

/** As RemovedAccount.Is: the spaces, the case and the dots over the "е" do not matter. */
export function isRemovedAccountName(
  username: string | null | undefined,
): boolean {
  return !!username && fold(username.trim()) === FOLDED_NAME;
}

export const removedAccountRoute = {
  name: "profile",
  params: { username: REMOVED_ACCOUNT_NAME },
} as const satisfies RouteLocationRaw;
