import { readonly, ref } from "vue";

/**
 * The zone of this device, of the account and the zones the browser knows. The
 * account's zone is held here for the page; siteTime in datetime.ts reads it.
 */

/** No zone on the account is UTC, and this is how the pair of them is spelled. */
export const UTC = "UTC";

/** Where the last known zone of the account waits for the next visit. */
const ACCOUNT_ZONE_KEY = "dm3-account-time-zone";

const accountZone = ref<string | null>(null);

/** The zone of the signed-in account, or null for a guest; reactive. */
export const accountTimeZone = readonly(accountZone);

/**
 * An empty zone is UTC, as the letters read it. Kept on the device too, so the
 * next visit draws its first frame in it.
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

/** A zone's offset as a reader writes it ("UTC+3", "UTC-3:30", "UTC"), for today. */
function offsetOf(timeZone: string): string {
  try {
    const parts = new Intl.DateTimeFormat("en-US", {
      timeZone,
      timeZoneName: "longOffset",
    }).formatToParts(new Date());
    const name =
      parts.find((part) => part.type === "timeZoneName")?.value ?? "";
    // "GMT+02:00" written as a reader writes it: "UTC+2".
    const offset = name
      .replace("GMT", "")
      .replace(/:00$/, "")
      .replace(/^([+-])0(\d)/, "$1$2");
    return offset ? `UTC${offset}` : UTC;
  } catch {
    // An unknown zone name can come from another browser's account.
    return UTC;
  }
}

/** "Europe/Moscow (UTC+3)": the place the account stores and the offset a clock shows. */
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

/** The method of ES2022, past the ES2020 the app is compiled for: an older browser lacks it. */
interface ListsTimeZones {
  supportedValuesOf(key: "timeZone"): string[];
}

const listsTimeZones = (intl: object): intl is ListsTimeZones =>
  "supportedValuesOf" in intl && typeof intl.supportedValuesOf === "function";

/**
 * Asked of the browser, since the zone database moves; empty where the method
 * is missing, and the field then offers UTC alone.
 */
export function knownTimeZones(): string[] {
  const intl: object = Intl;
  try {
    return listsTimeZones(intl) ? intl.supportedValuesOf("timeZone") : [];
  } catch {
    return [];
  }
}
