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
import { onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";

const store = useBlogsStore();
const userStore = useAuthStore();
const route = useRoute();

onMounted(() => store.fetchPopularBlogs());

// Same as PopularGames: the block is mounted once for the life of the app, so
// nothing but this refreshes the unread counters when the viewer changes
// (force=true because a plain fetch() no-ops inside the cache TTL).
useViewerChange(() => store.fetchPopularBlogs(true));

// Re-trigger on navigation so a failed fetch gets another chance once the
// TTL cache considers it stale.
watch(
  () => route.fullPath,
  () => store.fetchPopularBlogs(),
);
</script>
