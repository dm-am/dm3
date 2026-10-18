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

  // The three mutators return the problem document instead of swallowing it.
  // They used to return void, so a rejected subscribe (already subscribed,
  // blacklisted by the author) was indistinguishable from a successful one, and
  // the caller reported it as done either way. A refusal now stops the caller
  // before it reports anything, and the list is only refetched when something
  // actually changed.
  //
  // That refetch is forced, and the argument is not decoration. The list is read
  // through useApiList, which keeps it fresh for a minute, so an unforced
  // fetch() right after a mutation returns at the freshness check without
  // asking the server: the row this call had just deleted stayed in the list,
  // the subscribe button went on reading "Подписан", and the next click sent
  // the id of a subscription the server no longer had — a refusal for an
  // unsubscribe that had in fact gone through. The minute of caching is there
  // for readers; a caller that has just changed the data on the server is not
  // one of them.
  const subscribe = async (
    targetType: SubscriptionTargetType,
    targetId: string,
    settings?: number,
  ): Promise<GeneralError | null> => {
    const { error } = await subscriptionApi.subscribe({
      targetType,
      targetId,
      settings,
    });
    if (error) return error;
    await all.fetch(true);
    return null;
  };

  const unsubscribe = async (
    subscriptionId: string,
  ): Promise<GeneralError | null> => {
    const { error } = await subscriptionApi.unsubscribe(subscriptionId);
    if (error) return error;
    await all.fetch(true);
    return null;
  };

  const updateSettings = async (
    subscriptionId: string,
    settings: number,
  ): Promise<GeneralError | null> => {
    const { error } = await subscriptionApi.updateSettings(subscriptionId, {
      settings,
    });
    if (error) return error;
    await all.fetch(true);
    return null;
  };

  const unsubscribeByTarget = async (
    targetType: SubscriptionTargetType,
    targetId: string,
  ): Promise<GeneralError | null> => {
    const subscription = getSubscription(targetType, targetId);
    return subscription ? await unsubscribe(subscription.id) : null;
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
