import type { Envelope } from "@/shared/api/models/common";
import { Api } from "@/shared/api";

/** Sitewide fundraising goal state (single editable resource) */
export interface Fundraising {
  goalAmount: number;
  collectedAmount: number;
  modifiedUtc: string;
}

/** Update request for the fundraising goal */
export interface UpdateFundraisingRequest {
  goalAmount: number;
  collectedAmount: number;
}

export default new (class FundraisingApi {
  /**
   * Get the current fundraising goal and collected amount
   * (anonymous endpoint)
   */
  public getFundraising() {
    return Api.get<Envelope<Fundraising>>("fundraising");
  }

  /**
   * Update the fundraising goal and collected amount (admin only)
   * @param request New goal and collected amounts
   */
  public updateFundraising(request: UpdateFundraisingRequest) {
    return Api.put<Envelope<Fundraising>>("fundraising", request);
  }
})();
