import type { ApiResult, ListEnvelope } from "@/shared/api/models/common";
import type {
  Subscription,
  SubscribeRequest,
  UpdateSubscriptionRequest,
  SubscriptionTargetType,
} from "@/shared/api/models/subscriptions";
import { Api } from "@/shared/api";
import {
  subscriptionFromWire,
  type WireSubscription,
} from "./subscriptionWire";

// The server names the enums, the model numbers them.
async function one(
  request: Promise<ApiResult<WireSubscription>>,
): Promise<ApiResult<Subscription>> {
  const { data, error } = await request;
  return { data: data ? subscriptionFromWire(data) : null, error };
}

const BASE_PATH = "users/me/subscriptions";

export default new (class SubscriptionApi {
  public async getMySubscriptions(
    type?: SubscriptionTargetType,
  ): Promise<ApiResult<ListEnvelope<Subscription>>> {
    const { data, error } = await Api.get<ListEnvelope<WireSubscription>>(
      BASE_PATH,
      type !== undefined ? { type } : undefined,
    );
    return {
      data: data
        ? {
            ...data,
            resources: data.resources
              .map(subscriptionFromWire)
              .filter((subscription) => subscription !== null),
          }
        : null,
      error,
    };
  }

  public subscribe(request: SubscribeRequest) {
    return one(Api.post<WireSubscription>(BASE_PATH, request));
  }

  public updateSettings(id: string, request: UpdateSubscriptionRequest) {
    return one(Api.patch<WireSubscription>(`${BASE_PATH}/${id}`, request));
  }

  public unsubscribe(id: string) {
    return Api.delete(`${BASE_PATH}/${id}`);
  }
})();
