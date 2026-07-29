import { defineStore } from "pinia";
import { computed } from "vue";
import type { Subscription } from "@/shared/api/models/subscriptions";
import { SubscriptionTargetType } from "@/shared/api/models/subscriptions";
import { subscriptionApi } from "@/shared/api";
import { useApiList } from "@/shared/lib/composables/useApiResource";

export const useSubscriptionsStore = defineStore("subscriptions", () => {
  const all = useApiList<Subscription>(() =>
    subscriptionApi.getMySubscriptions(),
  );

  // Filtered subscriptions by type
  const gameSubscriptions = computed(
    () =>
      all.data.value?.filter(
        (s) => s.targetType === SubscriptionTargetType.Game,
      ) ?? [],
  );

  const blogSubscriptions = computed(
    () =>
      all.data.value?.filter(
        (s) => s.targetType === SubscriptionTargetType.Blog,
      ) ?? [],
  );

  const topicSubscriptions = computed(
    () =>
      all.data.value?.filter(
        (s) => s.targetType === SubscriptionTargetType.Topic,
      ) ?? [],
  );

  const userSubscriptions = computed(
    () =>
      all.data.value?.filter(
        (s) => s.targetType === SubscriptionTargetType.User,
      ) ?? [],
  );

  // Subscription counts
  const gameCount = computed(() => gameSubscriptions.value.length);
  const blogCount = computed(() => blogSubscriptions.value.length);
  const topicCount = computed(() => topicSubscriptions.value.length);
  const userCount = computed(() => userSubscriptions.value.length);
  const totalCount = computed(() => all.data.value?.length ?? 0);

  // Check if subscribed to a target
  const isSubscribed = (
    targetType: SubscriptionTargetType,
    targetId: string,
  ) => {
    return (
      all.data.value?.some(
        (s) => s.targetType === targetType && s.targetId === targetId,
      ) ?? false
    );
  };

  // Get subscription for a target
  const getSubscription = (
    targetType: SubscriptionTargetType,
    targetId: string,
  ) => {
    return all.data.value?.find(
      (s) => s.targetType === targetType && s.targetId === targetId,
    );
  };

  // Subscribe to a target
  const subscribe = async (
    targetType: SubscriptionTargetType,
    targetId: string,
    settings?: number,
  ) => {
    await subscriptionApi.subscribe({ targetType, targetId, settings });
    await all.fetch();
  };

  // Unsubscribe
  const unsubscribe = async (subscriptionId: string) => {
    await subscriptionApi.unsubscribe(subscriptionId);
    await all.fetch();
  };

  // Update settings on an existing subscription
  const updateSettings = async (subscriptionId: string, settings: number) => {
    await subscriptionApi.updateSettings(subscriptionId, { settings });
    await all.fetch();
  };

  // Unsubscribe by target
  const unsubscribeByTarget = async (
    targetType: SubscriptionTargetType,
    targetId: string,
  ) => {
    const subscription = getSubscription(targetType, targetId);
    if (subscription) {
      await unsubscribe(subscription.id);
    }
  };

  return {
    // All subscriptions
    subscriptions: all.data,
    subscriptionsLoading: all.loading,
    subscriptionsError: all.error,
    fetchSubscriptions: all.fetch,

    // Filtered by type
    gameSubscriptions,
    blogSubscriptions,
    topicSubscriptions,
    userSubscriptions,

    // Counts
    gameCount,
    blogCount,
    topicCount,
    userCount,
    totalCount,

    // Helpers
    isSubscribed,
    getSubscription,
    subscribe,
    unsubscribe,
    unsubscribeByTarget,
    updateSettings,
  };
});
