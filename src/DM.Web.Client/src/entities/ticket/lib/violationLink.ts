import { isSafeAddress } from "@/shared/lib/bbcode/bbcodeAddresses";
import { toInternalPath } from "@/shared/lib/utils/internalUrl";

// The server writes the address on a line of its own, rendered with no tag for a bare
// address; only that line becomes a link, and only where a [link] of the text may lead.

/** TicketService.LinkCaption, one per kind of complaint. */
const TICKET_LINK_CAPTIONS = [
  "Ссылка на нарушение",
  "Ссылка на решение",
] as const;

const LINK_LINE = new RegExp(
  `^(\\s*)(${TICKET_LINK_CAPTIONS.join("|")}): (\\S+)(\\s*)$`,
);

/** The site's own address gives the path, so it works on whichever address the reader came by. */
export function ticketLinkHref(address: string): string | null {
  const value = address.trim();
  if (value.startsWith("/")) {
    // As the server reads a path of the site, BbAddress.IsSitePath.
    if (value.startsWith("//") || /[\s\\]/.test(value)) return null;
    return toInternalPath(value);
  }
  if (!/^https?:\/\//i.test(value)) return null;
  return toInternalPath(value) ?? (isSafeAddress(value) ? value : null);
}

function standsOnItsOwnLine(node: ChildNode): boolean {
  const isBreak = (n: ChildNode | null) => n === null || n.nodeName === "BR";
  return isBreak(node.previousSibling) && isBreak(node.nextSibling);
}

/** In a tab of its own, as every [link] of a text is, so the ticket stays where it was. */
export function linkTicketAddress(html: string): string {
  if (!html || !TICKET_LINK_CAPTIONS.some((caption) => html.includes(caption)))
    return html;

  // Detached, so the caller stays a pure computed.
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
