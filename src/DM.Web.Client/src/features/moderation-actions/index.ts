// Moderation actions feature — warning and ban creation dialogs
// (product doc 4.2.4.1 / 4.2.4.2). The api module behind them is addressed as
// "../api" by the two dialogs and has no consumer outside the feature, so it
// stays off the public surface.
export { default as WarningDialog } from "./ui/WarningDialog.vue";
export { default as BanDialog } from "./ui/BanDialog.vue";
