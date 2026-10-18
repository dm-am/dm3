/**
 * Live tail: how close to the end of a list still counts as being at the end.
 *
 * A list that appends to its own bottom has to decide, every time something
 * arrives, whether to follow it down. A reader standing at the tail expects to
 * stay there; a reader who has scrolled up is reading, and moving him is taking
 * the page away from him. The distance below is the width of "at the end":
 * browsers report fractional scroll positions, and a reader who was just
 * scrolled to the bottom is routinely a pixel or two short of it, so an exact
 * comparison answers "no" for a reader who is plainly at the end.
 *
 * The number is here rather than in a component because more than one list
 * appends to its own bottom. The global chat page and the chat room of a game
 * both do, and the room's copy of the number was a literal under a comment
 * naming the page it was copied from. A comment is not a link: the day the
 * number is tuned, one list keeps holding the reader in place and the other
 * starts jumping under him, and nothing reports it.
 */
export const AT_BOTTOM_THRESHOLD_PX = 40;

/**
 * Is the reader within the tail of this scrolling element?
 *
 * Measured off the element every time it is asked rather than kept in a flag
 * updated by scroll events: appending content grows scrollHeight without firing
 * a scroll event, so a stored flag says "at the bottom" for a reader the new
 * content has just pushed above it.
 */
export function isNearBottom(
  el: HTMLElement,
  thresholdPx: number = AT_BOTTOM_THRESHOLD_PX,
): boolean {
  return el.scrollHeight - el.scrollTop - el.clientHeight < thresholdPx;
}
