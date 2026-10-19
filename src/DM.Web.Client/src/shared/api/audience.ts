/**
 * What the server renders BBCode for (RenderAudience on the backend); the
 * client never renders BBCode itself, and the author is given it unrendered.
 */
export const RENDER_AUDIENCE = {
  /** Standard read surface. Privacy tags filtered per viewer permissions. */
  Display: "display",
  /** Author loading own content into the editor: the BBCode as typed. */
  AuthorEdit: "author_edit",
} as const;

export type RenderAudience =
  (typeof RENDER_AUDIENCE)[keyof typeof RENDER_AUDIENCE];

/** HTTP header name carrying the audience to the server. */
export const X_DM_AUDIENCE = "X-Dm-Audience";
