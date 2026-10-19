<script setup lang="ts">
// The blog's layout: its title and the router-view of the sub-page; the
// navigation and the actions live in the sidebar's BlogPanel.
import { computed, onUnmounted } from "vue";
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
const { blog, blogError, blogErrorStatus } = storeToRefs(blogStore);

// The failure is a page, derived from the store: any reload of the blog that
// is refused nulls `blog`. Zero is a status too (a request that never arrived),
// so the status is not tested for truthiness.
const errorCode = computed(() =>
  blogError.value
    ? errorCodeForStatus(blogErrorStatus.value ?? undefined)
    : null,
);

const blogId = computed(() => route.params.id as string);

// The heading and the tab are one sentence: the blog name, then the section;
// the error owns it while the error page shows.
const announced = provideZoneSection();
const heading = computed(() =>
  joinTitleSegments(blog.value?.title, announced.value ?? route.meta.section),
);

useDocumentTitle(() =>
  errorCode.value ? getErrorConfig(errorCode.value).title : heading.value,
);

// The refusal is already in the store, which errorCode reads.
async function load(id: string) {
  await blogStore.loadBlog(id);
}

useFetchData(
  () => load(blogId.value),
  [
    {
      param: (p) => p.id,
      callback: (id) => {
        // The store is not keyed by id and the route record is shared, so the previous
        // blog is wiped first.
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

  <!-- The twin of the loaded header, in its margins. -->
  <div v-else class="blog-header">
    <PageTitleSkeleton />
  </div>
</template>

<style scoped lang="sass">
.blog-header
  margin-bottom: $medium
</style>
