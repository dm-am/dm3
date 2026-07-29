<template>
  <nav aria-label="Разделы: игры, блоги, форум">
    <ul class="blocks">
      <!-- Context panels (doc 4.2.1.3-4.2.1.5) — exactly one can match, the
           zones are route-exclusive (game / blog / moderation). Rendered
           above the global blocks so the current context leads. -->
      <GamePanel
        v-if="isGameRoute && routeGameId"
        :key="routeGameId"
        :game-id="routeGameId"
      />
      <BlogPanel
        v-else-if="isBlogRoute && routeBlogId"
        :key="routeBlogId"
        :blog-id="routeBlogId"
      />
      <ModerationPanel v-else-if="isModerationRoute" />
      <!-- Mentor panel (doc 4.2.1.6) — site-wide for Mentor+ users; the
           panel itself decides visibility (hidden until data confirms a
           non-empty curated list), so it mounts for any authed user. -->
      <MentorPanel v-if="userStore.user" />
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
import { computed, defineAsyncComponent } from "vue";
import { useRoute } from "vue-router";
import OwnedGames from "./OwnedGames.vue";
import OwnedBlogs from "./OwnedBlogs.vue";
import RecruitingGames from "./RecruitingGames.vue";
import ActiveGames from "./ActiveGames.vue";
import GamePanel from "./GamePanel.vue";
import { useAuthStore } from "@/entities/user";

// Below-fold: lazy-loaded to reduce initial bundle
const FinishedGames = defineAsyncComponent(() => import("./FinishedGames.vue"));
const ActiveBlogs = defineAsyncComponent(() => import("./ActiveBlogs.vue"));
const ForumBoards = defineAsyncComponent(() => import("./ForumBoards.vue"));

// Conditional context panels: lazy-loaded so their entity stores/features
// stay out of the main bundle (LeftSidebar itself is statically imported by
// the router). Each mounts only on its own zone or user role.
const BlogPanel = defineAsyncComponent(() => import("./BlogPanel.vue"));
const ModerationPanel = defineAsyncComponent(
  () => import("./ModerationPanel.vue"),
);
const MentorPanel = defineAsyncComponent(() => import("./MentorPanel.vue"));

const userStore = useAuthStore();
const route = useRoute();

const isGameRoute = computed(() => route.meta.gameZone === true);
const routeGameId = computed(() =>
  isGameRoute.value ? (route.params.id as string) : "",
);

// Blog zone (any /blogs/:id sub-route) — mirrors the gameZone gate.
const isBlogRoute = computed(() => route.meta.blogZone === true);
const routeBlogId = computed(() =>
  isBlogRoute.value ? (route.params.id as string) : "",
);

// Moderation zone (/moderation/*) — pure navigation panel, no params.
const isModerationRoute = computed(() => route.meta.moderationZone === true);
</script>

<style scoped lang="sass">
.blocks
  list-style: none
</style>
