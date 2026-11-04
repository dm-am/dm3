import type { GeneralError } from "@/shared/api/models/common";

/** A problem document with this status and title. */
export const problem = (
  status: number,
  title = "",
  extra: Partial<GeneralError> = {},
): GeneralError => ({ type: "", title, status, traceId: "t", ...extra });
