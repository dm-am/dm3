// Moderation actions feature — warning and ban creation dialogs
// (product doc 4.2.4.1 / 4.2.4.2). The api module behind them is addressed as
// "../api" by the two dialogs and has no consumer outside the feature, so it
// stays off the public surface.
export { default as WarningDialog } from "./ui/WarningDialog.vue";
export { default as BanDialog } from "./ui/BanDialog.vue";
// The correction of points is an action too, so it lives beside the two dialogs
// that issue a warning and a ban rather than in the entity that draws the card.
export { default as ChangeWarningPointsDialog } from "./ui/ChangeWarningPointsDialog.vue";
// The lift of a ban is the other correction a moderation list can make, and it
// asks for its reason the way the change of points does.
export { default as LiftBanDialog } from "./ui/LiftBanDialog.vue";
