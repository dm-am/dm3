import type { RouteLocationRaw } from "vue-router";

/**
 * "Аккаунт удален": the one name everything removed accounts wrote is signed
 * with, and the address of the profile that gathers all of it.
 *
 * Not an account. The server keeps what a removed account wrote and nests its
 * author, master or owner as null, and every surface that meets that null
 * writes this name there, leading to the profile. The profile itself is an
 * ordinary /users/:username page: the server answers the name with the sum of
 * every removed account (RemovedAccount on the server), and the tabs are the
 * usual lists asked by this name.
 */
export const REMOVED_ACCOUNT_NAME = "Аккаунт удален";

/** The lower-case "е" with two dots, U+0451, which the sources never spell. */
const DOTTED_E = /\u0451/g;

const fold = (value: string) => value.toLowerCase().replace(DOTTED_E, "е");

const FOLDED_NAME = fold(REMOVED_ACCOUNT_NAME);

/**
 * Whether a name, the way a reader types it into an address, is this profile.
 *
 * The same question the server asks (RemovedAccount.Is): surrounding spaces,
 * the case and the dots over the "е" do not matter, so the DM2 address
 * "Cabinet/?user=Аккаунт Удален" and the one the site writes open one page.
 * Lookalike Latin letters are not folded: the server answers such a name as a
 * name nobody holds, and so does the page.
 */
export function isRemovedAccountName(
  username: string | null | undefined,
): boolean {
  return !!username && fold(username.trim()) === FOLDED_NAME;
}

/** Where every "Аккаунт удален" signature leads: the profile of the name. */
export const removedAccountRoute = {
  name: "profile",
  params: { username: REMOVED_ACCOUNT_NAME },
} as const satisfies RouteLocationRaw;
