import type { GeneralError } from "@/shared/api/models/common";

/**
 * The failure of a request that was never made.
 *
 * A store mutation guards on having something to mutate — no blog loaded, no
 * game loaded — and that guard is not success. It used to return the same
 * `false` as a rejected request, which was at least honest; returning `null` from
 * a mutation that reports its problem document would claim the opposite.
 *
 * The shape matches what the client produces when a request gets no response:
 * status 0, empty title. Empty on purpose — call sites read the title first and
 * fall back to their own sentence, and any placeholder here would win that
 * fallback and replace the page's wording with a generic one.
 */
export const requestNotSent: GeneralError = {
  type: "Unknown",
  title: "",
  status: 0,
  traceId: "",
};
