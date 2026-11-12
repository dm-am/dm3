<template>
  <nav aria-label="Разделы: игры, блоги, форум">
    <ul class="blocks">
      <!-- One context panel at most, above the global blocks -->
      <GamePanel v-if="routeGameId" :key="routeGameId" :game-id="routeGameId" />
      <BlogPanel
        v-else-if="routeBlogId"
        :key="routeBlogId"
        :blog-id="routeBlogId"
      />
      <ModerationPanel v-else-if="showModerationPanel" />
      <OwnedGames v-if="userStore.user" />
      <OwnedBlogs v-if="userStore.user" />
      <RecruitingGames />
      <ActiveGames />
      <FinishedGames />
      <ActiveBlogs />
      <ForumBoards />
    </ul>
  </nav>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent, defineComponent, h } from "vue";
import { useRoute } from "vue-router";
import ContextPanelSkeleton from "./ContextPanelSkeleton.vue";
import OwnedGames from "./OwnedGames.vue";
import OwnedBlogs from "./OwnedBlogs.vue";
import RecruitingGames from "./RecruitingGames.vue";
import ActiveGames from "./ActiveGames.vue";
import { useAuthStore, userIsModerator } from "@/entities/user";

// Below the fold: loaded after the page.
const FinishedGames = defineAsyncComponent(() => import("./FinishedGames.vue"));
const ActiveBlogs = defineAsyncComponent(() => import("./ActiveBlogs.vue"));
const ForumBoards = defineAsyncComponent(() => import("./ForumBoards.vue"));

// The context panels, loaded only where they mount. They head the column, so
// their skeleton holds the place at once (no delay) while the chunk comes.
const panelSkeleton = (token: string, title: string) =>
  defineComponent(() => () => h(ContextPanelSkeleton, { token, title }));
const GamePanel = defineAsyncComponent({
  loader: () => import("./GamePanel.vue"),
  loadingComponent: panelSkeleton("GamePanel", "Меню игры"),
  delay: 0,
});
const BlogPanel = defineAsyncComponent({
  loader: () => import("./BlogPanel.vue"),
  loadingComponent: panelSkeleton("BlogPanel", "Меню блога"),
  delay: 0,
});
// prettier-ignore
const ModerationPanel = defineAsyncComponent(() => import("./ModerationPanel.vue"));

const userStore = useAuthStore();
const route = useRoute();

const isGameRoute = computed(() => route.meta.gameZone === true);
const routeGameId = computed(() =>
  isGameRoute.value ? (route.params.id as string) : "",
);

const isBlogRoute = computed(() => route.meta.blogZone === true);
const routeBlogId = computed(() =>
  isBlogRoute.value ? (route.params.id as string) : "",
);

// Hidden below Moderator: none of the panel's links would open for him.
const isModerationRoute = computed(() => route.meta.moderationZone === true);
const showModerationPanel = computed(
  () => isModerationRoute.value && userIsModerator(userStore.user),
);
</script>

<style scoped lang="sass">
.blocks
  list-style: none
</style>
