import { h, render } from "vue";
import AnchoredTooltipHost from "./AnchoredTooltipHost.vue";

/**
 * A tooltip for text that has no component to wrap it in <Tooltip>: server
 * HTML shown through v-html, where bbcodeInteractive.ts hears the pointer. It
 * is the site's own bubble (TooltipBubble), one per page, pointed at whatever
 * rectangle the caller names: the line of a hidden block under the pointer
 * rather than the whole block, whose top may be off the screen.
 *
 * The text is shown as text, through Vue's text binding. It is never read as
 * markup: the recipients of a hidden block are names the players wrote.
 *
 * The bubble is drawn into a host of its own at the end of <body>, mounted on
 * the first request. The code asking for it is plain DOM code that runs
 * wherever server HTML is shown, the reference dialog included, and a host
 * that every page had to remember to mount would be missing from the first
 * page that forgot.
 */
export interface AnchoredTooltipRequest {
  /** Id of the bubble element, new with every request. */
  id: string;
  /** Plain words; drawn as text. */
  text: string;
  /** The rectangle the bubble points at. */
  anchor: () => DOMRect | null;
  /** Whose bubble it is: another owner's hide leaves it alone. */
  owner: object;
}

let current: AnchoredTooltipRequest | null = null;
let host: HTMLElement | null = null;
let serial = 0;

function paint(): void {
  if (!host) {
    if (!current) return;
    host = document.createElement("div");
    document.body.appendChild(host);
  }
  render(h(AnchoredTooltipHost, { request: current }), host);
}

/** Shows `text` against `anchor`, replacing whatever bubble was up. */
export function showAnchoredTooltip(
  request: Omit<AnchoredTooltipRequest, "id">,
): void {
  serial += 1;
  current = { ...request, id: `anchored-tooltip-${serial}` };
  paint();
}

/** Hides the bubble; with an owner, only when it is that owner's. */
export function hideAnchoredTooltip(owner?: object): void {
  if (!current || (owner !== undefined && current.owner !== owner)) return;
  current = null;
  paint();
}

/** Whose bubble is up, or null. */
export function anchoredTooltipOwner(): object | null {
  return current?.owner ?? null;
}

/** The bubble on screen, for "was this press inside it", or null. */
export function anchoredTooltipElement(): HTMLElement | null {
  if (!current || !host) return null;
  return host.querySelector<HTMLElement>(`#${current.id}`);
}
