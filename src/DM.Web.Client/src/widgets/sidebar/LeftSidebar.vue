<template>
  <ContextMenu />
  <OwnedGames v-if="userStore.user" />
  <OwnedBlogs v-if="userStore.user" />
  <RecruitingGames />
  <ActiveGames />
  <FinishedGames />
  <ActiveBlogs />
  <ForumBoards />
</template>

<script setup lang="ts">
import ContextMenu from "./ContextMenu.vue";
import OwnedGames from "./OwnedGames.vue";
import OwnedBlogs from "./OwnedBlogs.vue";
import RecruitingGames from "./RecruitingGames.vue";
import ActiveGames from "./ActiveGames.vue";
import FinishedGames from "./FinishedGames.vue";
import ActiveBlogs from "./ActiveBlogs.vue";
import ForumBoards from "./ForumBoards.vue";
import { useUserStore } from "@/entities/user";

const userStore = useUserStore();
</script>
