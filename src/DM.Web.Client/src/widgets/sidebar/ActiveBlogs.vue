<template>
  <SidebarEntityList
    token="ActiveBlogs"
    title="Активные блоги"
    :lines="5"
    :items="store.activeBlogs"
    :errored="!!store.activeBlogsError"
    empty="Активных блогов пока нет"
    :retry="() => store.fetchActiveBlogs(true)"
    :forward-to="{ name: 'blogs' }"
    forward-label="Все блоги"
  >
    <template #item="{ item }">
      <BlogLink
        :blog="item"
        :counters="true"
        :always-show-counters="!userStore.user"
      />
    </template>
  </SidebarEntityList>
</template>

<script setup lang="ts">
import SidebarEntityList from "./SidebarEntityList.vue";
import BlogLink from "./BlogLink.vue";
import { useBlogsStore } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { useSidebarRefresh } from "./useSidebarRefresh";

const store = useBlogsStore();
const userStore = useAuthStore();

useSidebarRefresh(store.fetchActiveBlogs);
</script>
