<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useSubscriptionsStore } from "@/entities/subscription";
import {
  SubscriptionTargetType,
  type Subscription,
} from "@/shared/api/models/subscriptions";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
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

const getTargetLink = (subscription: Subscription): string => {
  switch (subscription.targetType) {
    case SubscriptionTargetType.Game:
      return `/games/${subscription.targetId}`;
    case SubscriptionTargetType.Blog:
      return `/blogs/${subscription.targetId}`;
    case SubscriptionTargetType.Topic:
      return `/forum/topics/${subscription.targetId}`;
    case SubscriptionTargetType.User:
      return `/users/${subscription.targetId}`;
    default:
      return "#";
  }
};

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
        v-for="subscription in filteredSubscriptions"
        :key="subscription.id"
        class="subscription-item"
      >
        <span class="type-badge">{{
          getTargetTypeLabel(subscription.targetType)
        }}</span>
        <router-link :to="getTargetLink(subscription)" class="target-link">
          {{ subscription.targetId }}
        </router-link>
        <button
          class="unsubscribe-btn"
          @click="handleUnsubscribe(subscription)"
        >
          Отписаться
        </button>
      </li>
    </ul>
  </div>
</template>

<style scoped lang="sass">
// Variables are injected globally via vite.config.ts additionalData

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
      color: $text-on-green
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

.unsubscribe-btn
  padding: $minor $small
  border: 1px solid $accent-red
  border-radius: $border-radius
  background: transparent
  color: $accent-red
  cursor: pointer
  font-size: 0.85rem

  // Тинт вместо сплошной заливки: $text-on-red рассчитан на светлую
  // подложку, на $accent-red его контраст 1.5.
  &:hover
    +tint($accent-red, 15%)
</style>
