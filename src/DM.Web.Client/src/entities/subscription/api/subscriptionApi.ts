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

/**
 * Every answer that carries a subscription is read through
 * subscriptionFromWire: the server names the enums, the model numbers them.
 */
async function one(
  request: Promise<ApiResult<WireSubscription>>,
): Promise<ApiResult<Subscription>> {
  const { data, error } = await request;
  return { data: data ? subscriptionFromWire(data) : null, error };
}

/** Where the subscriptions of the signed-in reader live on the API. */
const BASE_PATH = "users/me/subscriptions";

export default new (class SubscriptionApi {
  /**
   * Get all subscriptions for the current user
   * @param type Optional filter by target type
   */
  public async getMySubscriptions(
    type?: SubscriptionTargetType,
  ): Promise<ApiResult<ListEnvelope<Subscription>>> {
    const { data, error } = await Api.get<ListEnvelope<WireSubscription>>(
      BASE_PATH,
      type !== undefined ? { type } : undefined,
    );
    return {
      data: data
        ? { ...data, resources: data.resources.map(subscriptionFromWire) }
        : null,
      error,
    };
  }

  /**
   * Get subscription by ID
   */
  public getById(id: string) {
    return one(Api.get<WireSubscription>(`${BASE_PATH}/${id}`));
  }

  /**
   * Check subscription status for a specific target
   */
  public checkSubscription(type: SubscriptionTargetType, targetId: string) {
    return one(
      Api.get<WireSubscription>(`${BASE_PATH}/check`, {
        type,
        targetId,
      }),
    );
  }

  /**
   * Subscribe to a target
   */
  public subscribe(request: SubscribeRequest) {
    return one(Api.post<WireSubscription>(BASE_PATH, request));
  }

  /**
   * Update subscription settings
   */
  public updateSettings(id: string, request: UpdateSubscriptionRequest) {
    return one(Api.patch<WireSubscription>(`${BASE_PATH}/${id}`, request));
  }

  /**
   * Unsubscribe
   */
  public unsubscribe(id: string) {
    return Api.delete(`${BASE_PATH}/${id}`);
  }
})();
