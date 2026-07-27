// Moderation actions feature — warning and ban creation dialogs
// (product doc 4.2.4.1 / 4.2.4.2) plus their API surface.
export { default as WarningDialog } from "./ui/WarningDialog.vue";
export { default as BanDialog } from "./ui/BanDialog.vue";
export { default as moderationActionsApi, PERMANENT_BAN_HOURS } from "./api";
export type {
  WarningResult,
  CreateWarningPayload,
  UserWarningsSummary,
  PublicWarningInfo,
  BanResult,
  CreateBanPayload,
  BanAccessPolicy,
} from "./api";
