/**
 * Rendering audience — semantic intent for BBCode output from the server.
 * Mirrors the backend RenderAudience enum. The server renders HTML
 * appropriate for the requested audience; clients never render BBCode
 * themselves.
 */
export const RENDER_AUDIENCE = {
  /** Standard read surface. Privacy tags filtered per viewer permissions. */
  Display: "display",
  /** Author loading own content into the editor. Round-trip via data-bb-*. */
  AuthorEdit: "author_edit",
  /** Plain text (emails, notifications). Privacy tags unconditionally stripped. */
  PlainText: "plain_text",
  /** Link preview / embed. NSFW-safe tag set, privacy tags stripped. */
  EmbedSafe: "embed_safe",
} as const;

export type RenderAudience =
  (typeof RENDER_AUDIENCE)[keyof typeof RENDER_AUDIENCE];

/** HTTP header name carrying the audience to the server. */
export const X_DM_AUDIENCE = "X-Dm-Audience";
