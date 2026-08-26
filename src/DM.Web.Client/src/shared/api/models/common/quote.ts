/**
 * The markup of a quotation, as the server composes it.
 *
 * The whole tag, opening through closing, with the author already in place. The
 * client never builds this from what is on the page: the rendered HTML is a
 * lossy image of the source, and the source of somebody else's message is not
 * something the browser has or should have. Asking the server also means the
 * text arrives already filtered for whoever is asking, so a block this reader
 * cannot see is absent by construction rather than by a check somebody has to
 * remember to write.
 */
export type QuoteSource = {
  /** Whole quotation, opening tag through closing tag, in BBCode */
  text: string;
  /**
   * Whether a private block this reader can see on the page was left out.
   *
   * Always false for a reader the page does not show one to, so the flag never
   * tells its holder that a block they were not meant to see exists.
   */
  privateTextStripped: boolean;
};
