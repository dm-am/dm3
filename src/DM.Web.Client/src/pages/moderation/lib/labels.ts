// Russian labels of moderation enums: the server keeps none.
import type { PremoderationStatus } from "@/shared/api/models/common";

export const PREMODERATION_STATUS_LABELS: Record<
  Exclude<PremoderationStatus, "Approved">,
  string
> = {
  AwaitingApproval: "Ожидает проверки",
  AwaitingEdits: "Требует правок",
};

// The words of a ban term, as the ban record draws them.
export { banTermLabel } from "@/entities/moderation";
