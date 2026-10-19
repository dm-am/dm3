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

interface UpdateFundraisingRequest {
  title: string;
  goalAmount: number;
  collectedAmount: number;
}

export default new (class FundraisingApi {
  public getFundraising() {
    return Api.get<Envelope<Fundraising>>("fundraising");
  }

  public updateFundraising(request: UpdateFundraisingRequest) {
    return Api.put<Envelope<Fundraising>>("fundraising", request);
  }
})();
