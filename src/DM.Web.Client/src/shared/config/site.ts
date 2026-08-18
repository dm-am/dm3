/**
 * Site-wide facts. SSOT for values that appear in multiple places.
 */
import type { IconName } from "@/shared/lib/utils/icons";

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
  /**
   * The mark drawn before the name in the sidebar block: an icon of the set,
   * or a short label where no honest icon exists.
   *
   * Not an emoji, which is what stood here first (the pair the old settings
   * bubble used): an emoji is a glyph of whatever font the visitor's system
   * ships, so the mark looked different on every platform, and the copy gate
   * (copy-rules.spec.ts) rightly refuses emoji in interface copy. The globe
   * icon is that same Windows glyph extracted into the registry, so on the
   * owner's reference platform nothing changed visually; a country has no
   * icon and no monochrome flag worth drawing, so it is named by its letters,
   * which is what Windows rendered in place of the flag emoji anyway.
   */
  mark: { icon: IconName } | { label: string };
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
  { name: "Основной сервер", host: "dm.am", mark: { icon: "globe" } },
  { name: "Прокси в России", host: "ru.l.dm.am", mark: { label: "RU" } },
];

/**
 * The address being read.
 *
 * A host the list does not know counts as the first address, and every
 * development and preview stand is such a host. The lines are then drawn there
 * too, in the words a visitor reads on the site itself, which is the only way
 * the developer sees the thing he is changing. The two real addresses answer
 * exactly as before: each recognises itself.
 *
 * One rule in one place: both the block that lists every address and the
 * failure toast that names the other one ask here which is which.
 */
export function currentSiteAddress(
  host: string = window.location.host,
): SiteAddress {
  return (
    SITE_ADDRESSES.find((address) => address.host === host) ?? SITE_ADDRESSES[0]
  );
}

/** The addresses other than the one being read. */
export function otherSiteAddresses(
  host: string = window.location.host,
): SiteAddress[] {
  const here = currentSiteAddress(host);
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
