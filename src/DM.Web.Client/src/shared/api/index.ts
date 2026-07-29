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

// Envelope unwrap helper (single-resource responses)
export { unwrapResource } from "./envelope";

// API services. Every module exports a singleton instance, so the name is
// camelCase — one name per thing. A parallel PascalCase set used to be exported
// "for backward compatibility" with nothing, and both halves accumulated real
// consumers, so the codebase had two names for every client.
export { default as accountApi } from "./accountApi";
export { default as achievementApi } from "./achievementApi";
export { default as blacklistApi } from "./blacklistApi";
export { default as communityApi } from "./communityApi";
export { default as moderationApi } from "./moderationApi";
export { default as notepadApi } from "./notepadApi";
export { default as notificationApi } from "./notificationApi";
export {
  default as personalApi,
  type UpdateProfilePayload,
} from "./personalApi";
export { default as subscriptionApi } from "./subscriptionApi";
export {
  default as supportApi,
  type TicketSubtype,
  type CreateTicketIntake,
  type Ticket,
  type TicketStatus,
} from "./supportApi";
export { default as uploadApi } from "./uploadApi";

// Re-export models
export * from "./models";
