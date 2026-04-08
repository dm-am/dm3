import type { ListEnvelope } from "./models/common";
import type {
  Subscription,
  SubscribeRequest,
  UpdateSubscriptionRequest,
  SubscriptionTargetType,
} from "./models/subscriptions";
import Api from "./client";

export default new (class SubscriptionApi {
  private basePath = "users/me/subscriptions";

  /**
   * Get all subscriptions for the current user
   * @param type Optional filter by target type
   */
  public getMySubscriptions(type?: SubscriptionTargetType) {
    const params = type !== undefined ? `?type=${type}` : "";
    return Api.get<ListEnvelope<Subscription>>(`${this.basePath}${params}`);
  }

  /**
   * Get subscription by ID
   */
  public getById(id: string) {
    return Api.get<Subscription>(`${this.basePath}/${id}`);
  }

  /**
   * Check subscription status for a specific target
   */
  public checkSubscription(type: SubscriptionTargetType, targetId: string) {
    return Api.get<Subscription>(
      `${this.basePath}/check?type=${type}&targetId=${targetId}`,
    );
  }

  /**
   * Subscribe to a target
   */
  public subscribe(request: SubscribeRequest) {
    return Api.post<Subscription>(this.basePath, request);
  }

  /**
   * Update subscription settings
   */
  public updateSettings(id: string, request: UpdateSubscriptionRequest) {
    return Api.patch<Subscription>(`${this.basePath}/${id}`, request);
  }

  /**
   * Unsubscribe
   */
  public unsubscribe(id: string) {
    return Api.delete(`${this.basePath}/${id}`);
  }
})();
