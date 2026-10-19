<script lang="ts">
/**
 * The tabs of a profile and the panel of the open one. useProfileTabs is the
 * page's: the page redraws the tabs on every load, and the open tab outlives it.
 */
import { computed, ref, watch, type Ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQueryRaw } from "vue-router";
import { useModeratedProfile } from "@/entities/moderation";
import { userIsModerator } from "@/entities/user";
import type { User } from "@/shared/api/models/community";
import type { ModeratedProfile } from "@/shared/api/models/moderation";
import type { TabItem } from "@/shared/ui/Tabs";

// Each content tab holds its list, the best of its category and the subscribers to it.
export type ProfileTab =
  | "about"
  | "games"
  | "blogs"
  | "topics"
  | "achievements"
  | "moderation";

const DEFAULT_TAB: ProfileTab = "about";

/** Every tab, so a name arriving from outside can be checked against them. */
const TABS: readonly ProfileTab[] = [
  "about",
  "games",
  "blogs",
  "topics",
  "achievements",
  "moderation",
];

// The keys of the topics filter: a switch keeps only the open tab's own, and every tab starts at page 1.
const TOPICS_QUERY_KEYS: readonly string[] = [
  "search",
  "authors",
  "createdFromUtc",
  "createdToUtc",
  "sortBy",
  "sortOrder",
];

/** Whoever has the profile open, signed in or not. */
type Viewer = Parameters<typeof userIsModerator>[0];

/**
 * The tab the profile opens on. Switching tabs writes nothing to the address;
 * `?tab=` lets a page that sends the reader here name the list that answers
 * him (a deleted blog lands on "Блоги"), and the first switch drops it.
 */
function openOn(
  asked: unknown,
  isRemovedAccount: boolean,
  viewer: Viewer,
): ProfileTab {
  const tab =
    typeof asked === "string" && TABS.includes(asked as ProfileTab)
      ? (asked as ProfileTab)
      : DEFAULT_TAB;
  // "Аккаунт удален" has no "Зал славы" to open on.
  if (tab === "achievements" && isRemovedAccount) return DEFAULT_TAB;
  // Nor has anybody but a moderator a "Модерация", which waits for the moderated profile.
  if (tab === "moderation" && (isRemovedAccount || !userIsModerator(viewer))) {
    return DEFAULT_TAB;
  }
  return tab;
}

// Games and blogs hide when there are none; topics always stand, the table says it is empty.
function tabItems(
  hasGames: boolean,
  hasBlogs: boolean,
  isRemovedAccount: boolean,
  moderatedProfile: ModeratedProfile | null,
): TabItem<ProfileTab>[] {
  return [
    { value: "about", label: "О себе" },
    { value: "games", label: "Игры", hidden: !hasGames },
    { value: "blogs", label: "Блоги", hidden: !hasBlogs },
    { value: "topics", label: "Топики" },
    // Awards and achievements are a person's, and "Аккаунт удален" is nobody.
    {
      value: "achievements",
      label: "Зал славы",
      hidden: isRemovedAccount,
    },
    // Last, and only for the viewer the server serves the moderated profile to.
    { value: "moderation", label: "Модерация", hidden: !moderatedProfile },
  ];
}

/**
 * The tabs of a profile: which of them stand, which one is open, and what the
 * address keeps of the open one.
 */
export function useProfileTabs(profile: {
  user: Ref<User | null>;
  currentUser: Ref<Viewer>;
  usernameParam: Ref<string>;
  isRemovedAccount: Ref<boolean>;
}) {
  const route = useRoute();
  const router = useRouter();
  const { user, usernameParam, isRemovedAccount } = profile;

  // An empty name asks nothing; the server's answer draws the "Модерация" tab.
  const {
    moderatedProfile,
    loading: moderationLoading,
    refresh: refreshModeration,
  } = useModeratedProfile(() =>
    isRemovedAccount.value ? "" : usernameParam.value,
  );

  const activeTab = ref<ProfileTab>(
    openOn(route.query.tab, isRemovedAccount.value, profile.currentUser.value),
  );

  function onTabChange(value: ProfileTab) {
    activeTab.value = value;
    // Only the query is cleaned, so another tab's filter does not leak into this one.
    const allowedKeys = value === "topics" ? TOPICS_QUERY_KEYS : [];
    const query: LocationQueryRaw = {};
    for (const key of allowedKeys) {
      if (route.query[key] !== undefined) query[key] = route.query[key];
    }
    router.replace({ query });
  }

  const hasGames = computed(
    () => (user.value?.gamesHosting ?? 0) + (user.value?.gamesPlaying ?? 0) > 0,
  );
  const hasBlogs = computed(() => (user.value?.blogsHosting ?? 0) > 0);

  const tabs = computed(() =>
    tabItems(
      hasGames.value,
      hasBlogs.value,
      isRemovedAccount.value,
      moderatedProfile.value,
    ),
  );

  // Opened on "Модерация", the page waits for the answer that decides whether the tab exists.
  watch([tabs, moderationLoading], ([next]) => {
    if (activeTab.value === "moderation" && moderationLoading.value) return;
    const current = next.find((t) => t.value === activeTab.value);
    if (current?.hidden) onTabChange(DEFAULT_TAB);
  });

  watch(usernameParam, () => {
    // Another profile opens on the default tab.
    activeTab.value = DEFAULT_TAB;
  });

  return {
    activeTab,
    tabs,
    onTabChange,
    moderatedProfile,
    refreshModeration,
  };
}

type ProfileTabState = ReturnType<typeof useProfileTabs>;
</script>

<script setup lang="ts">
import type { Username } from "@/entities/user";
import { SubscriptionSettings } from "@/shared/api/models/subscriptions";
import { Tabs } from "@/shared/ui/Tabs";
import { BlockTitle } from "@/shared/ui/Layout";
import ProfileAbout from "./ProfileAbout.vue";
import ProfileRemovedAccountAbout from "./ProfileRemovedAccountAbout.vue";
import ProfileGamesTable from "./ProfileGamesTable.vue";
import ProfileBlogsTable from "./ProfileBlogsTable.vue";
import ProfileBestPostSection from "./ProfileBestPostSection.vue";
import ProfileBestPublicationSection from "./ProfileBestPublicationSection.vue";
import ProfileTopicsList from "./ProfileTopicsList.vue";
import ProfileSubscribersSection from "./ProfileSubscribersSection.vue";
import ProfileAchievements from "./ProfileAchievements.vue";
import ProfileModerationTab from "./moderation/ProfileModerationTab.vue";

const props = defineProps<{
  /** The page's: the open tab outlives the strip, redrawn on every load. */
  model: ProfileTabState;
  user: User;
  /** The name in the address, which the lists of the tabs are asked by. */
  usernameParam: string;
  isEditMode: boolean;
  /** What the editor of "О себе" holds: its BBCode, or what was typed since. */
  aboutSource: string;
  isRemovedAccount: boolean;
}>();

const emit = defineEmits<{
  "update-field": [field: string, value: string];
}>();

const { activeTab, tabs, onTabChange, moderatedProfile, refreshModeration } =
  props.model;

defineSlots<{
  /** What opens the "О себе" tab before the words of the owner. */
  note?: () => unknown;
}>();

// Typed by hand: a template ref does not carry the generic of <Tabs>.
const tabsRef = ref<{
  tabId(v: ProfileTab): string;
  panelId(v: ProfileTab): string;
} | null>(null);

// The best post is drawn only when the user has rated posts.
const hasBestPost = computed(() => (props.user.reviewsReceived ?? 0) > 0);

// Raw: ProfileSubscribersSection filters by category, in the server's order.
const subscribers = computed(() => props.user.subscribers ?? []);

// The real totals: the preview is capped before the categories are counted.
const subscriberCounts = computed(
  () => props.user.subscribersByCategory ?? { games: 0, blogs: 0, topics: 0 },
);
</script>

<template>
  <!-- The tabs are the section's heading; the strip wraps after a bar. -->
  <Tabs
    ref="tabsRef"
    class="info-section"
    :model-value="activeTab"
    :tabs="tabs"
    variant="headings"
    aria-label="Разделы профиля"
    @update:model-value="onTabChange"
  />

  <div
    class="tab-content"
    role="tabpanel"
    :id="tabsRef?.panelId(activeTab)"
    :aria-labelledby="tabsRef?.tabId(activeTab)"
  >
    <template v-if="activeTab === 'about'">
      <slot name="note" />

      <ProfileRemovedAccountAbout v-if="isRemovedAccount" />
      <ProfileAbout
        v-else
        :user="user"
        :is-edit-mode="isEditMode"
        :source="aboutSource"
        @update-field="
          (field: string, value: string) => emit('update-field', field, value)
        "
      />
    </template>
    <template v-else-if="activeTab === 'games'">
      <ProfileGamesTable :username="usernameParam" />
      <section v-if="hasBestPost" class="featured-section">
        <BlockTitle>Лучший игровой пост</BlockTitle>
        <ProfileBestPostSection :username="usernameParam as Username" />
      </section>
      <ProfileSubscribersSection
        v-if="!isRemovedAccount"
        :subscribers="subscribers"
        label="Подписаны на игры"
        :flag="SubscriptionSettings.AuthorGameEvents"
        :total="subscriberCounts.games"
      />
    </template>

    <template v-else-if="activeTab === 'blogs'">
      <ProfileBlogsTable :username="usernameParam" />
      <section class="featured-section">
        <BlockTitle>Самая популярная публикация</BlockTitle>
        <ProfileBestPublicationSection :username="usernameParam" />
      </section>
      <ProfileSubscribersSection
        v-if="!isRemovedAccount"
        :subscribers="subscribers"
        label="Подписаны на блоги"
        :flag="SubscriptionSettings.AuthorBlogEvents"
        :total="subscriberCounts.blogs"
      />
    </template>

    <template v-else-if="activeTab === 'topics'">
      <ProfileTopicsList :username="usernameParam" />
      <ProfileSubscribersSection
        v-if="!isRemovedAccount"
        :subscribers="subscribers"
        label="Подписаны на топики"
        :flag="SubscriptionSettings.AuthorTopicEvents"
        :total="subscriberCounts.topics"
      />
    </template>

    <ProfileAchievements
      v-else-if="activeTab === 'achievements'"
      :username="usernameParam"
      :user="user"
    />

    <ProfileModerationTab
      v-else-if="activeTab === 'moderation' && moderatedProfile"
      :profile="moderatedProfile"
      :target-username="usernameParam"
      @updated="refreshModeration"
    />
  </div>
</template>

<style scoped lang="sass">
$panel-min-height: 100px

// $medium over the strip, so the last contact and "Информация" stand as the last stat line and "Контакты".
.info-section
  // Flush with the column.
  margin-top: $medium

  // The heading's own margin-bottom matches "Контакты" to its first line.
  :deep(h2)
    margin-top: 0

.tab-content
  margin-top: $small
  display: flex
  flex-direction: column
  gap: $medium
  min-height: $panel-min-height

// The subscribers caption belongs to the block above: $small from it, not the panel's $medium.
.tab-content > :deep(.subscribers-line)
  margin-top: -$small

// A "best of" block under its own BlockTitle; the lists are named by the open tab.
.featured-section
  display: flex
  flex-direction: column
  gap: $small

  :deep(h2)
    margin: 0
</style>
