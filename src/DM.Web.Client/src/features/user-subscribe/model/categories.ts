import { SubscriptionSettings } from "@/shared/api/models/subscriptions";

/**
 * What a subscription to a person can be about: the three author-category bits
 * of SubscriptionSettings, each with the words the site says it in.
 *
 * `events` names what the server actually sends under the bit
 * (DM.Workers.NotificationDispatcher): a new game, a game started and a game
 * recruiting under "Игры"; a new blog and a blog opened under "Блоги"; a new
 * topic under "Топики". The bare word alone left a reader guessing whether
 * "Игры" meant every post in every game of that person.
 */
export interface UserSubscriptionCategory {
  bit: SubscriptionSettings;
  /** The checkbox caption. */
  label: string;
  /** What arrives under it, as the line under the checkbox says. */
  events: string;
  /** The same category inside a sentence: "игры, блоги". */
  word: string;
}

export const USER_SUBSCRIPTION_CATEGORIES: readonly UserSubscriptionCategory[] =
  [
    {
      bit: SubscriptionSettings.AuthorGameEvents,
      label: "Игры",
      events: "Новая игра, начало игры, набор игроков",
      word: "игры",
    },
    {
      bit: SubscriptionSettings.AuthorBlogEvents,
      label: "Блоги",
      events: "Новый блог, открытие блога",
      word: "блоги",
    },
    {
      bit: SubscriptionSettings.AuthorTopicEvents,
      label: "Топики",
      events: "Новый топик на форуме",
      word: "топики",
    },
  ];

/** The three category bits together. */
export const CATEGORY_BITS = USER_SUBSCRIPTION_CATEGORIES.reduce(
  (bits, category) => bits | category.bit,
  0,
);

/** "игры, блоги, топики": the categories a subscription holds, in order. */
export function describeUserSubscription(settings: number): string {
  return USER_SUBSCRIPTION_CATEGORIES.filter(
    (category) => (settings & category.bit) !== 0,
  )
    .map((category) => category.word)
    .join(", ");
}

/**
 * The number to send when the categories become `categories` on a
 * subscription that holds `settings`.
 *
 * The server replaces the whole number, so the bits outside the categories
 * (the channels, InApp and Email) are carried over from what the subscription
 * holds. A subscription that somehow holds no channel gets InApp, the one the
 * site delivers through, or it would be a subscription that never says a word.
 */
export function withCategories(settings: number, categories: number): number {
  const channels = settings & ~CATEGORY_BITS || SubscriptionSettings.InApp;
  return channels | (categories & CATEGORY_BITS);
}
