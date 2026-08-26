<template>
  <SidebarEntityList
    token="PopularBlogs"
    title="Популярные блоги"
    :lines="10"
    :items="store.popularBlogs"
    :errored="!!store.popularBlogsError"
    empty="Популярных блогов пока нет"
    :retry="() => store.fetchPopularBlogs(true)"
    :forward-to="{
      name: 'blogs',
      query: { sortBy: 'popularity', sortOrder: 'desc' },
    }"
    forward-label="Все популярные блоги"
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

useSidebarRefresh(store.fetchPopularBlogs);
</script>
