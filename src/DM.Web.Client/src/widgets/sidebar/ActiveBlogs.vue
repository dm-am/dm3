<template>
  <SidebarEntityList
    token="ActiveBlogs"
    title="Активные блоги"
    :lines="5"
    :items="active"
    :errored="!!store.activeBlogsError"
    empty="Активных блогов пока нет"
    :retry="() => store.fetchActiveBlogs(true)"
    :forward-to="{ name: 'blogs' }"
    forward-label="Все блоги"
  >
    <template #item="{ item }">
      <BlogLink :blog="item" :always-show-counters="!userStore.user" />
    </template>
  </SidebarEntityList>
</template>

<script setup lang="ts">
import SidebarEntityList from "./SidebarEntityList.vue";
import BlogLink from "./BlogLink.vue";
import { SIDEBAR_BLOG_ROWS, useBlogsStore } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { useSidebarRefresh } from "./useSidebarRefresh";
import { useFilledRows, useLeftSidebarBlogs } from "./leftSidebarRows";

const store = useBlogsStore();
const userStore = useAuthStore();

// Without the blogs a block above shows.
const { active, aboveActive } = useLeftSidebarBlogs();
useFilledRows({
  items: () => store.activeBlogs,
  rows: () => active.value,
  above: () => aboveActive.value,
  limit: SIDEBAR_BLOG_ROWS,
  take: () => store.activeBlogsTake,
  widen: store.widenActiveBlogs,
});

useSidebarRefresh(store.fetchActiveBlogs);
</script>
