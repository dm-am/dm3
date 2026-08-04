// API Client
import Api from "./client";

export { Api };
export default Api;

// Installed by the app layer: the client reports an expired session, the app
// decides where that takes the user.
export { setSessionExpiredHandler } from "./client";

// BBCode render audience — semantic intent for server-rendered content
export {
  RENDER_AUDIENCE,
  X_DM_AUDIENCE,
  type RenderAudience,
} from "./audience";

// Token-gated endpoints: the credential travels in a header, never in the URL
export { X_DM_ACCOUNT_TOKEN, X_DM_TICKET_TOKEN } from "./tokenHeaders";

// Envelope unwrap helper (single-resource responses)
export { unwrapResource } from "./envelope";

// Mirror list — deployment topology, not a domain concept
export {
  default as mirrorApi,
  type Mirror,
  type MirrorList,
} from "./mirrorApi";

// Uploading a binary with progress and an idempotency key is transport, and the
// two consumers belong to different domains — so this one client stays here
// while every domain client lives in its entity slice
// (docs/conventions/PATTERNS.md). The singleton is exported under a camelCase
// name: a parallel PascalCase set used to exist "for backward compatibility"
// with nothing, and both halves accumulated real consumers.
export { default as uploadApi } from "./uploadApi";

// Comments query — one shape and one wire conversion for every discussion
export { toCommentsQueryParams, type CommentsQuery } from "./commentsQuery";

// Re-export models
export * from "./models";
