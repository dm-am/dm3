import { readonly, ref } from "vue";

/**
 * The zone this device keeps time in, the zone of the account, and the zones
 * the browser knows about.
 *
 * The account carries a zone for the letters, which have no browser to ask,
 * and the site shows time in it too: the owner's word is that a signed-in
 * reader sees every date and time by the zone of the account, and a guest by
 * the clock of the device. So the account's zone is held here for the page
 * (`accountTimeZone`), and `siteTime` in datetime.ts reads it.
 */

/** No zone on the account is UTC, and this is how the pair of them is spelled. */
export const UTC = "UTC";

/** Where the last known zone of the account waits for the next visit. */
const ACCOUNT_ZONE_KEY = "dm3-account-time-zone";

const accountZone = ref<string | null>(null);

/**
 * The zone of the signed-in account, or null for a guest, whose time is the
 * device's. Reactive: a date drawn by a computed or a template is drawn again
 * when the zone arrives or changes.
 */
export const accountTimeZone = readonly(accountZone);

/**
 * Sets the zone of the signed-in account, or null when nobody is signed in.
 *
 * An empty zone is UTC, as the letters read it. The zone is kept on the device
 * as well, so the next visit draws its first frame in it instead of in the
 * device's clock while the preferences are on their way.
 */
export function setAccountTimeZone(zone: string | null): void {
  accountZone.value = zone === null ? null : zone || UTC;
  try {
    if (accountZone.value === null) localStorage.removeItem(ACCOUNT_ZONE_KEY);
    else localStorage.setItem(ACCOUNT_ZONE_KEY, accountZone.value);
  } catch {
    // A browser that refuses storage asks the server on every visit; the
    // first frame is then drawn in the device's zone, which is all it costs.
  }
}

/** The zone of the account as the last visit left it, or null. */
export function storedAccountTimeZone(): string | null {
  try {
    return localStorage.getItem(ACCOUNT_ZONE_KEY);
  } catch {
    return null;
  }
}

/**
 * The offset of a zone from UTC, spelled the way a reader writes it: "UTC+3",
 * "UTC-3:30", "UTC".
 *
 * Taken for today, so a zone on summer time names the offset it is in now and
 * not the one it was in last winter.
 */
export function offsetOf(timeZone: string): string {
  try {
    const parts = new Intl.DateTimeFormat("en-US", {
      timeZone,
      timeZoneName: "longOffset",
    }).formatToParts(new Date());
    const name =
      parts.find((part) => part.type === "timeZoneName")?.value ?? "";
    // "GMT+02:00" arrives with a leading zero and whole minutes. A reader
    // writes "UTC+2", and the zero in the hours reads to them as a slip rather
    // than as precision.
    const offset = name
      .replace("GMT", "")
      .replace(/:00$/, "")
      .replace(/^([+-])0(\d)/, "$1$2");
    return offset ? `UTC${offset}` : UTC;
  } catch {
    // A zone name this browser does not know. It can arrive from the account,
    // written by another browser or an older zone database, and a notice that
    // threw here would take the page with it.
    return UTC;
  }
}

/**
 * A zone as it is shown to a reader: "Europe/Moscow (UTC+3)".
 *
 * The database name and the offset both, because neither is enough alone. The
 * name is what the account stores and what a reader recognises as a place; the
 * offset is what they can compare against a clock.
 */
export function labelOf(timeZone: string): string {
  return timeZone === UTC ? UTC : `${timeZone} (${offsetOf(timeZone)})`;
}

/**
 * The zone this device is in, or an empty string when the browser will not say.
 */
export function deviceTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone ?? "";
  } catch {
    return "";
  }
}

/**
 * The zones this browser knows.
 *
 * Asked of the browser rather than written out here: the zone database moves
 * several times a year, and a list copied into the source goes stale without
 * saying so. Empty where the method is missing, and the field that uses it then
 * offers UTC alone — honester than a list made up here.
 */
export function knownTimeZones(): string[] {
  const supported = (
    Intl as unknown as { supportedValuesOf?: (key: string) => string[] }
  ).supportedValuesOf;
  try {
    return typeof supported === "function" ? supported("timeZone") : [];
  } catch {
    return [];
  }
}
