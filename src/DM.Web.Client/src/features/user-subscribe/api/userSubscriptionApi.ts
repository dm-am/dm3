import type { ListEnvelope } from "@/shared/api/models/common";
import type { UserSubscriptionCategory } from "@/shared/api/models/subscriptions";
import { Api } from "@/shared/api";

export default new (class UserSubscriptionApi {
  /** The categories of a subscription to a user, with what each sends. */
  public getCategories() {
    return Api.get<ListEnvelope<UserSubscriptionCategory>>(
      "users/subscription-categories",
    );
  }
})();
