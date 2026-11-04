import type { GeneralError } from "@/shared/api/models/common";

/**
 * The failure of a request that was never made: a store guard with nothing to
 * mutate. The title is empty, as for no response, so the call site's own
 * sentence wins.
 */
export const requestNotSent: GeneralError = {
  type: "Unknown",
  title: "",
  status: 0,
  traceId: "",
};
