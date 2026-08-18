import type { Envelope } from "@/shared/api/models/common";
import { Api } from "@/shared/api";

/** Sitewide fundraising goal state (single editable resource) */
export interface Fundraising {
  /** What the money is being collected for, shown above the progress bar. */
  title: string;
  goalAmount: number;
  collectedAmount: number;
  modifiedUtc: string;
}

/** Update request for the fundraising goal */
export interface UpdateFundraisingRequest {
  title: string;
  goalAmount: number;
  collectedAmount: number;
}

export default new (class FundraisingApi {
  /**
   * Get the current fundraising goal: what it is for, the target and
   * the collected amount (anonymous endpoint)
   */
  public getFundraising() {
    return Api.get<Envelope<Fundraising>>("fundraising");
  }

  /**
   * Update the fundraising goal (admin only)
   * @param request New title, goal and collected amounts
   */
  public updateFundraising(request: UpdateFundraisingRequest) {
    return Api.put<Envelope<Fundraising>>("fundraising", request);
  }
})();
