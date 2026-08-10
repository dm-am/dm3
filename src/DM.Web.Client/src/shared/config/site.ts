/**
 * Site-wide facts. SSOT for values that appear in multiple places.
 */

/**
 * The year Dungeon Master was founded. Lower bound for period pickers
 * (site statistics) and the footer copyright range; also referenced in
 * the About / Rules prose.
 */
export const SITE_FOUNDED_YEAR = 2007;

/** One of the addresses the site answers on. */
export interface SiteAddress {
  /** What the address is called in the interface. */
  name: string;
  /** Host, exactly as a visitor would type it. */
  host: string;
}

/**
 * Every address this site answers on.
 *
 * Not fetched. The list is here rather than behind a request because the one
 * moment a visitor needs the other address is the moment the site stopped
 * answering, and a request cannot be served then. It is also the only way the
 * line can be drawn on the first frame instead of appearing a beat later.
 *
 * The server keeps the same list for its own use (letters are built from it).
 * Two copies of four strings that change once in a decade is the cheaper half
 * of that trade.
 */
export const SITE_ADDRESSES: SiteAddress[] = [
  { name: "Основной адрес сайта", host: "dm.am" },
  { name: "Зеркало сайта в России", host: "ru.l.dm.am" },
];

/**
 * The addresses other than the one being read.
 *
 * A host the list does not know counts as the first address, and every
 * development and preview stand is such a host. The line is then drawn there
 * too, in the words a visitor reads on the site itself, which is the only way
 * the developer sees the thing he is changing. The two real addresses answer
 * exactly as before: each names the other.
 */
export function otherSiteAddresses(
  host: string = window.location.host,
): SiteAddress[] {
  const here =
    SITE_ADDRESSES.find((address) => address.host === host) ??
    SITE_ADDRESSES[0];
  return SITE_ADDRESSES.filter((address) => address !== here);
}

/**
 * The origin of a link built to leave this page: a permalink a reader copies,
 * the address of a post pasted into a moderator's warning.
 *
 * It returns the address being read, which is what all eight call sites did on
 * their own. The change is that they no longer know it: naming the canonical
 * address for shared links is one decision, and this is the one place it can be
 * taken. A link built here can be opened from either door of the site.
 */
export function permalinkOrigin(): string {
  return window.location.origin;
}
