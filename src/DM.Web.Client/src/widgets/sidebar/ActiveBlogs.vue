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
import { onMounted, watch } from "vue";
import { useRoute } from "vue-router";

const store = useBlogsStore();
const userStore = useAuthStore();
const route = useRoute();

onMounted(() => store.fetchActiveBlogs());

// Refetch only on actual login/logout to keep unread counters accurate
// (force=true because a plain fetch() no-ops inside the cache TTL).
watch(
  () => userStore.user?.username,
  (newUsername, oldUsername) => {
    if ((newUsername && !oldUsername) || (!newUsername && oldUsername)) {
      store.fetchActiveBlogs(true);
    }
  },
);

// Re-trigger on navigation so a failed fetch gets another chance once the
// TTL cache considers it stale.
watch(
  () => route.fullPath,
  () => store.fetchActiveBlogs(),
);
</script>
