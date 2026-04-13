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
import { defineAsyncComponent } from "vue";
import ContextMenu from "./ContextMenu.vue";
import OwnedGames from "./OwnedGames.vue";
import OwnedBlogs from "./OwnedBlogs.vue";
import RecruitingGames from "./RecruitingGames.vue";
import ActiveGames from "./ActiveGames.vue";
import { useUserStore } from "@/entities/user";

// Below-fold: lazy-loaded to reduce initial bundle
const FinishedGames = defineAsyncComponent(() => import("./FinishedGames.vue"));
const ActiveBlogs = defineAsyncComponent(() => import("./ActiveBlogs.vue"));
const ForumBoards = defineAsyncComponent(() => import("./ForumBoards.vue"));

const userStore = useUserStore();
</script>
