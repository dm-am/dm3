import { defineStore } from "pinia";
import { computed } from "vue";
import type { GeneralError } from "@/shared/api/models/common";
import type { Subscription } from "@/shared/api/models/subscriptions";
import { SubscriptionTargetType } from "@/shared/api/models/subscriptions";
import { subscriptionApi } from "../api";
import { useApiList } from "@/shared/lib/composables/useApiResource";

export const useSubscriptionsStore = defineStore("subscriptions", () => {
  const all = useApiList<Subscription>(() =>
    subscriptionApi.getMySubscriptions(),
  );

  const userSubscriptions = computed(
    () =>
      all.data.value?.filter(
        (s) => s.targetType === SubscriptionTargetType.User,
      ) ?? [],
  );

  const isSubscribed = (targetType: SubscriptionTargetType, targetId: string) =>
    all.data.value?.some(
      (s) => s.targetType === targetType && s.targetId === targetId,
    ) ?? false;

  const getSubscription = (
    targetType: SubscriptionTargetType,
    targetId: string,
  ) =>
    all.data.value?.find(
      (s) => s.targetType === targetType && s.targetId === targetId,
    );

  // The refetch is forced: an unforced one would answer from the minute of cache.
  const reloadAfter = async (
    request: Promise<{ error: GeneralError | null }>,
  ): Promise<GeneralError | null> => {
    const { error } = await request;
    if (error) return error;
    await all.fetch(true);
    return null;
  };

  const subscribe = (
    targetType: SubscriptionTargetType,
    targetId: string,
    settings?: number,
  ) =>
    reloadAfter(subscriptionApi.subscribe({ targetType, targetId, settings }));

  const unsubscribe = (subscriptionId: string) =>
    reloadAfter(subscriptionApi.unsubscribe(subscriptionId));

  const updateSettings = (subscriptionId: string, settings: number) =>
    reloadAfter(subscriptionApi.updateSettings(subscriptionId, { settings }));

  return {
    subscriptions: all.data,
    subscriptionsLoading: all.loading,
    subscriptionsError: all.error,
    fetchSubscriptions: all.fetch,

    userSubscriptions,

    isSubscribed,
    getSubscription,
    subscribe,
    unsubscribe,
    updateSettings,
  };
});
