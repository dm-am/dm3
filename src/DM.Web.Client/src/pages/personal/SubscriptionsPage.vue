<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import type { RouteLocationRaw } from "vue-router";
import { useSubscriptionsStore } from "@/entities/subscription";
import {
  SubscriptionTargetType,
  type Subscription,
} from "@/shared/api/models/subscriptions";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { notifyFailure } from "@/shared/lib/errors";

const store = useSubscriptionsStore();
const activeTab = ref<"all" | "games" | "blogs" | "topics" | "users">("all");

onMounted(() => store.fetchSubscriptions());

const filteredSubscriptions = computed(() => {
  if (!store.subscriptions) return [];
  switch (activeTab.value) {
    case "games":
      return store.gameSubscriptions;
    case "blogs":
      return store.blogSubscriptions;
    case "topics":
      return store.topicSubscriptions;
    case "users":
      return store.userSubscriptions;
    default:
      return store.subscriptions;
  }
});

const getTargetTypeLabel = (type: SubscriptionTargetType): string => {
  switch (type) {
    case SubscriptionTargetType.Game:
      return "Игра";
    case SubscriptionTargetType.Blog:
      return "Блог";
    case SubscriptionTargetType.Topic:
      return "Топик";
    case SubscriptionTargetType.User:
      return "Пользователь";
    default:
      return "Неизвестно";
  }
};

/**
 * Named routes, one per target type. The hand-written paths were four guesses
 * and three of them were wrong: a game lives at /game/:id (not /games/), a
 * topic addressed by id goes through the resolver route (/forum/topics/<guid>
 * was parsed as board "topics" and topic number NaN), and a profile is
 * addressed by username, which the subscription row does not hold — hence
 * targetUsername on the wire.
 *
 * An empty targetTitle means the server found no row to name: the game, blog or
 * topic is gone. Such a subscription is still listed, because unsubscribing is
 * the only thing left to do with it, but it gets no link: every one of these
 * routes would have opened an error page.
 */
const targetLocation = (
  subscription: Subscription,
): RouteLocationRaw | null => {
  if (!subscription.targetTitle) return null;

  switch (subscription.targetType) {
    case SubscriptionTargetType.Game:
      return { name: "game", params: { id: subscription.targetId } };
    case SubscriptionTargetType.Blog:
      return { name: "blog", params: { id: subscription.targetId } };
    case SubscriptionTargetType.Topic:
      return {
        name: "forum-topic-redirect",
        params: { topicId: subscription.targetId },
      };
    case SubscriptionTargetType.User:
      return subscription.targetUsername
        ? { name: "profile", params: { username: subscription.targetUsername } }
        : null;
    default:
      return null;
  }
};

// One row per subscription, with the link resolved once: the template asked
// for it twice, and a target that no longer exists has no link at all.
const rows = computed(() =>
  filteredSubscriptions.value.map((subscription) => ({
    subscription,
    label: subscription.targetTitle || VALUE_UNAVAILABLE,
    to: targetLocation(subscription),
  })),
);

const handleUnsubscribe = async (subscription: Subscription) => {
  const error = await store.unsubscribe(subscription.id);
  if (error) notifyFailure(error, "Не удалось отписаться");
};
</script>

<template>
  <div class="subscriptions-page">
    <page-title>Подписки</page-title>

    <div class="tabs">
      <button
        :class="{ active: activeTab === 'all' }"
        @click="activeTab = 'all'"
      >
        Все ({{ store.totalCount }})
      </button>
      <button
        :class="{ active: activeTab === 'games' }"
        @click="activeTab = 'games'"
      >
        Игры ({{ store.gameCount }})
      </button>
      <button
        :class="{ active: activeTab === 'blogs' }"
        @click="activeTab = 'blogs'"
      >
        Блоги ({{ store.blogCount }})
      </button>
      <button
        :class="{ active: activeTab === 'topics' }"
        @click="activeTab = 'topics'"
      >
        Топики ({{ store.topicCount }})
      </button>
      <button
        :class="{ active: activeTab === 'users' }"
        @click="activeTab = 'users'"
      >
        Пользователи ({{ store.userCount }})
      </button>
    </div>

    <secondary-text v-if="store.subscriptionsLoading"
      >Загрузка...</secondary-text
    >

    <template v-else-if="filteredSubscriptions.length === 0">
      <secondary-text>Нет подписок</secondary-text>
    </template>

    <ul v-else class="subscription-list">
      <li
        v-for="row in rows"
        :key="row.subscription.id"
        class="subscription-item"
      >
        <span class="type-badge">{{
          getTargetTypeLabel(row.subscription.targetType)
        }}</span>
        <router-link v-if="row.to" :to="row.to" class="target-link">
          {{ row.label }}
        </router-link>
        <span v-else class="target-link target-link--gone">{{
          row.label
        }}</span>
        <button
          class="unsubscribe-btn"
          @click="handleUnsubscribe(row.subscription)"
        >
          Отписаться
        </button>
      </li>
    </ul>
  </div>
</template>

<style scoped lang="sass">
// Variables are injected globally via vite.config.ts additionalData
@use "@/assets/styles/Inputs" as *

.subscriptions-page
  padding: $medium

  h1
    margin-bottom: $medium

.tabs
  display: flex
  gap: $small
  margin-bottom: $medium
  flex-wrap: wrap

  button
    padding: $minor $small
    border: 1px solid $border
    border-radius: $border-radius
    background: transparent
    cursor: pointer

    &:hover
      background: $hover-overlay

    &.active
      background: $accent-green
      color: $text-on-fill
      border-color: $accent-green

.subscription-list
  list-style: none
  padding: 0
  margin: 0

.subscription-item
  display: flex
  align-items: center
  gap: $small
  padding: $small 0
  border-bottom: 1px solid $border

  &:last-child
    border-bottom: none

.type-badge
  font-size: 0.75rem
  padding: 2px 6px
  background: $bg-element-overlay
  border-radius: $border-radius
  color: $text-muted

.target-link
  flex: 1
  color: $link
  text-decoration: none

  &:hover
    text-decoration: underline

  // A target that is gone: the name is still worth reading, the link is not
  // worth offering.
  &--gone
    color: $text-muted

    &:hover
      text-decoration: none

.unsubscribe-btn
  +button-outline($accent-red)
</style>
