import { isSafeAddress } from "@/shared/lib/utils/bbcode";
import { toInternalPath } from "@/shared/lib/utils/internalUrl";

/**
 * The address a complaint carries, made a link where it can be one.
 *
 * The forms send the address of the violation (or of the decision complained
 * about) as a field of its own, and the server writes it into the text of the
 * ticket on a line of its own, under the words of that field
 * (TicketService.LinkCaption): "Ссылка на нарушение: /games/example". The text
 * is rendered with the tag set of profile texts, which has no tag for a bare
 * address, so the line reached every reader as plain text, and the moderator
 * the complaint was written for had to copy it into the address bar.
 *
 * Only that line, and only an address that leads somewhere a link of the site
 * may lead: a path of this site ("/users/Name"), a full address of the page's
 * own origin, or an http or https address outside it that a [link] of the text
 * would take (isSafeAddress: never the reader's own machine or network).
 * Anything else, "javascript:" among it, stays the text it was. "//host" and
 * "/\host" are not paths of this site, a browser reads both as another host.
 */

/**
 * The words the server writes the address under, one per kind of complaint:
 * a complaint about a decision links the decision, every other the violation.
 */
export const TICKET_LINK_CAPTIONS = [
  "Ссылка на нарушение",
  "Ссылка на решение",
] as const;

/** "Caption: address", the line as the server composes it, nothing else. */
const LINK_LINE = new RegExp(
  `^(\\s*)(${TICKET_LINK_CAPTIONS.join("|")}): (\\S+)(\\s*)$`,
);

/**
 * Where the address of a ticket leads, or null when it stays text.
 *
 * A path of this site and a full address of the page's own origin give the
 * path, so the link works on whichever address of the site the reader is on.
 * Another host gives the address as written.
 */
export function ticketLinkHref(address: string): string | null {
  const value = address.trim();
  if (value.startsWith("/")) {
    // One slash, then no second one and no backslash anywhere, as the server
    // reads a path of the site (BbAddress.IsSitePath).
    if (value.startsWith("//") || /[\s\\]/.test(value)) return null;
    return toInternalPath(value);
  }
  if (!/^https?:\/\//i.test(value)) return null;
  return toInternalPath(value) ?? (isSafeAddress(value) ? value : null);
}

/** A line of the text: the start or a <br> before it, the end or a <br> after. */
function standsOnItsOwnLine(node: ChildNode): boolean {
  const isBreak = (n: ChildNode | null) => n === null || n.nodeName === "BR";
  return isBreak(node.previousSibling) && isBreak(node.nextSibling);
}

/**
 * The rendered text of a ticket with the address on its link line made a link.
 *
 * Opened in a tab of its own, as every [link] of a text of the site is
 * (LinkTag on the server), so the ticket stays where the reader left it. The
 * markup is handed back untouched when there is nothing to make a link of.
 */
export function linkTicketAddress(html: string): string {
  if (!html || !TICKET_LINK_CAPTIONS.some((caption) => html.includes(caption)))
    return html;

  // A detached <template>, as postAttachments.ts does it: the live page is
  // never touched and the caller stays a pure computed.
  const template = document.createElement("template");
  template.innerHTML = html;

  let changed = false;
  for (const node of Array.from(template.content.childNodes)) {
    if (node.nodeType !== Node.TEXT_NODE || !standsOnItsOwnLine(node)) continue;
    const match = LINK_LINE.exec(node.textContent ?? "");
    if (!match) continue;
    const [, lead, caption, address, trail] = match;
    const href = ticketLinkHref(address);
    if (!href) continue;

    const link = document.createElement("a");
    link.setAttribute("href", href);
    link.setAttribute("target", "_blank");
    link.setAttribute("rel", "noopener noreferrer");
    link.textContent = address;
    node.replaceWith(
      ...[`${lead}${caption}: `, link, trail].filter((part) => part !== ""),
    );
    changed = true;
  }

  return changed ? template.innerHTML : html;
}
