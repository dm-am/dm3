<script setup lang="ts">
// Thin blog layout (mirrors GamePage): the blog title (H1) and a
// <router-view> for the active sub-page. Status, author, assistants and the
// subscriber count live in the info table (BlogDetails), not duplicated in a
// header strip. All per-blog navigation and actions live in the left-sidebar
// BlogPanel; role flags are lifted into the shared useBlogDetailsStore (SSOT).
import { computed, onUnmounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { PageTitleSkeleton } from "@/shared/ui/Skeleton";

const route = useRoute();
const blogStore = useBlogDetailsStore();
const { blog, blogError } = storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);

// Same rule as the game shell: the blog name first, the section of the active
// sub-route (meta.section) second.
useDocumentTitle(() =>
  joinTitleSegments(blog.value?.title, route.meta.section),
);

useFetchData(
  () => blogStore.loadBlog(blogId.value),
  [
    {
      param: (p) => p.id,
      callback: (id) => {
        // Same as the game shell: the detail store is not keyed by id and the
        // route record is shared, so without wiping first the previous blog's
        // publications and comments stay under the new title.
        blogStore.reset();
        return blogStore.loadBlog(id as string);
      },
    },
  ],
);

onUnmounted(() => {
  blogStore.reset();
});
</script>

<template>
  <template v-if="blog">
    <div class="blog-header">
      <page-title>{{ blog.title }}</page-title>
    </div>

    <router-view />
  </template>

  <div v-else-if="blogError" class="blog-error">
    <p>{{ blogError }}</p>
    <router-link :to="{ name: 'blogs' }">Вернуться к списку блогов</router-link>
  </div>

  <!-- Loading: twin of the loaded header (skeleton-parity). Reuses
       .blog-header so the margins match; the twin itself owns its geometry. -->
  <div v-else class="blog-header">
    <PageTitleSkeleton />
  </div>
</template>

<style scoped lang="sass">
.blog-header
  margin-bottom: $medium

.blog-error
  padding: $big
  color: $accent-red

  a
    color: $link
    margin-top: $small
    display: inline-block
</style>
