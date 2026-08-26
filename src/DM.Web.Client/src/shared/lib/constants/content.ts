/**
 * The longest body a post, a comment or a message may be saved with.
 *
 * The server holds the same number in BodyTextLimits.cs and every validator on
 * those three surfaces refuses a longer one. The browser cannot read it, so the
 * composers keep this copy and show the count against it; an architecture test
 * compares the two numbers.
 *
 * It is shown rather than enforced. Nothing here cuts a body short or blocks the
 * send: the refusal belongs to the save, and a composer that silently truncated
 * would lose text the author wrote. What the counter buys is the moment of
 * finding out — before the answer is written rather than after — and that moment
 * is what the Quote action needed, because a quotation of a long message arrives
 * all at once and can take a draft over the limit in a single press.
 */
export const BODY_TEXT_MAX_LENGTH = 50000;
