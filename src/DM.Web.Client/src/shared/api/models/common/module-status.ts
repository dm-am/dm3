/**
 * Lifecycle status of games and blogs, the backend ModuleStatus. An object and
 * a union rather than an enum: the union is what arrives on the wire.
 */
export const ModuleStatus = {
  Draft: "Draft",
  Active: "Active",
  Closed: "Closed",
} as const;

export type ModuleStatus = (typeof ModuleStatus)[keyof typeof ModuleStatus];

/** Why a closed game or blog closed, the backend ClosedReason. */
export const ClosedReason = {
  None: "None",
  Finished: "Finished",
  Frozen: "Frozen",
} as const;

export type ClosedReason = (typeof ClosedReason)[keyof typeof ClosedReason];

/** A newbie's game or blog on premoderation, the backend PremoderationStatus; "Approved" is not shown. */
export type PremoderationStatus =
  | "Approved"
  | "AwaitingApproval"
  | "AwaitingEdits";
