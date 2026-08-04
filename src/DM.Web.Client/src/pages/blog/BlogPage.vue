<script setup lang="ts">
// Thin blog layout (mirrors GamePage): the blog title (H1) and a
// <router-view> for the active sub-page. Status, author, assistants and the
// subscriber count live in the info table (BlogDetails), not duplicated in a
// header strip. All per-blog navigation and actions live in the left-sidebar
// BlogPanel; role flags are lifted into the shared useBlogDetailsStore (SSOT).
import { computed, onUnmounted, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import {
  ErrorPage,
  errorCodeForStatus,
  getErrorConfig,
} from "@/shared/ui/ErrorPage";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import { provideZoneSection } from "@/shared/lib/composables/useZoneSection";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { PageTitleSkeleton } from "@/shared/ui/Skeleton";

const route = useRoute();
const blogStore = useBlogDetailsStore();
const { blog } = storeToRefs(blogStore);

// Same rule as the game shell: the failure is a page, not a sentence.
const errorCode = ref<number | null>(null);

const blogId = computed(() => route.params.id as string);

// The mirror of the game shell: the heading on the page and the name of the tab
// are one sentence, composed once — the blog name first, the section of the
// active sub-route second. While the error page is showing, the error owns it:
// there is no blog behind the id, and the section alone names a page the reader
// is not looking at.
const announced = provideZoneSection();
const heading = computed(() =>
  joinTitleSegments(blog.value?.title, announced.value ?? route.meta.section),
);

useDocumentTitle(() =>
  errorCode.value ? getErrorConfig(errorCode.value).title : heading.value,
);

async function load(id: string) {
  errorCode.value = null;
  const { ok, status } = await blogStore.loadBlog(id);
  if (!ok) errorCode.value = errorCodeForStatus(status);
}

useFetchData(
  () => load(blogId.value),
  [
    {
      param: (p) => p.id,
      callback: (id) => {
        // Same as the game shell: the detail store is not keyed by id and the
        // route record is shared, so without wiping first the previous blog's
        // publications and comments stay under the new title.
        blogStore.reset();
        return load(id as string);
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
      <page-title>{{ heading }}</page-title>
    </div>

    <router-view />
  </template>

  <ErrorPage v-else-if="errorCode" :code="errorCode" />

  <!-- Loading: twin of the loaded header (skeleton-parity). Reuses
       .blog-header so the margins match; the twin itself owns its geometry. -->
  <div v-else class="blog-header">
    <PageTitleSkeleton />
  </div>
</template>

<style scoped lang="sass">
.blog-header
  margin-bottom: $medium
</style>
