import { SubscriptionSettings } from "@/shared/api/models/subscriptions";
import { useApiList } from "@/shared/lib/composables/useApiResource";
import userSubscriptionApi from "../api/userSubscriptionApi";

/** A box of the panel: the category's bit and its name. */
interface PanelCategory {
  bit: SubscriptionSettings;
  label: string;
}

export const USER_SUBSCRIPTION_CATEGORIES: readonly PanelCategory[] = [
  { bit: SubscriptionSettings.AuthorGameEvents, label: "Игры" },
  { bit: SubscriptionSettings.AuthorBlogEvents, label: "Блоги" },
  { bit: SubscriptionSettings.AuthorTopicEvents, label: "Топики" },
];

export const CATEGORY_BITS = USER_SUBSCRIPTION_CATEGORIES.reduce(
  (bits, category) => bits | category.bit,
  0,
);

export function describeUserSubscription(settings: number): string {
  return USER_SUBSCRIPTION_CATEGORIES.filter(
    (category) => (settings & category.bit) !== 0,
  )
    .map((category) => category.label.toLowerCase())
    .join(", ");
}

// The server replaces the whole number: channels carry over, and a subscription with none gets InApp.
export function withCategories(settings: number, categories: number): number {
  const channels = settings & ~CATEGORY_BITS || SubscriptionSettings.InApp;
  return channels | (categories & CATEGORY_BITS);
}

// The words are the server's, which knows what it sends under each bit: asked once a session.
const categoryEvents = useApiList(() => userSubscriptionApi.getCategories(), {
  cacheMs: Infinity,
});

/**
 * The words under a box, which say what the category sends, so "Игры" is not
 * read as every post: asked for unless known or on their way, and absent until
 * they come.
 */
export function useCategoryEvents(): (bit: number) => string | undefined {
  void categoryEvents.fetch();
  return (bit) =>
    categoryEvents.data.value?.find((category) => category.category === bit)
      ?.events;
}
