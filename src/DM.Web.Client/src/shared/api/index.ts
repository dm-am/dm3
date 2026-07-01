// API Client
import Api from "./client";

export { Api };
export default Api;

// BBCode render audience — semantic intent for server-rendered content
export {
  RENDER_AUDIENCE,
  X_DM_AUDIENCE,
  type RenderAudience,
} from "./audience";

// API Services (PascalCase)
export { default as AccountApi } from "./accountApi";
export { default as AchievementApi } from "./achievementApi";
export { default as BlacklistApi } from "./blacklistApi";
export { default as BotApi } from "./botApi";
export { default as CommunityApi } from "./communityApi";
export { default as ModerationApi } from "./moderationApi";
export { default as NotepadApi } from "./notepadApi";
export { default as NotificationApi } from "./notificationApi";
export {
  default as PersonalApi,
  type UpdateProfilePayload,
} from "./personalApi";
export { default as SubscriptionApi } from "./subscriptionApi";
export { default as UploadApi } from "./uploadApi";

// API Services (camelCase aliases for backward compatibility)
export { default as accountApi } from "./accountApi";
export { default as achievementApi } from "./achievementApi";
export { default as blacklistApi } from "./blacklistApi";
export { default as botApi } from "./botApi";
export { default as communityApi } from "./communityApi";
export { default as moderationApi } from "./moderationApi";
export { default as notepadApi } from "./notepadApi";
export { default as notificationApi } from "./notificationApi";
export { default as personalApi } from "./personalApi";
export { default as subscriptionApi } from "./subscriptionApi";
export { default as uploadApi } from "./uploadApi";

// Re-export models
export * from "./models";
