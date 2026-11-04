<script setup lang="ts">
// The publication feed, filtered by rubric through ?rubric=: the rubric page
// is the feed with a filter.
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { PublicationCard } from "@/features/publication";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { ErrorState } from "@/shared/ui/ErrorState";
import Paging from "@/shared/ui/Paging/Paging.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";

const route = useRoute();
const blogStore = useBlogDetailsStore();
const {
  rubrics,
  publications,
  publicationsPaging,
  publicationsLoading,
  publicationsError,
  canManage,
} = storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);
const rubricId = computed(() => (route.query.rubric as string) || undefined);

const rubric = computed(() =>
  rubricId.value ? rubrics.value.find((r) => r.id === rubricId.value) : null,
);
const heading = computed(() => rubric.value?.title ?? "Лента публикаций");

// The shared Paging writes the page as ?number=.
function getPage(): number {
  const page = route.query.number;
  return page ? parseInt(page as string) || 1 : 1;
}

function load() {
  return blogStore.loadPublications(blogId.value, {
    rubricId: rubricId.value,
    page: getPage(),
  });
}

function editRoute(pubId: string) {
  return {
    name: "blog-publication-edit",
    params: { id: blogId.value, pubId },
  };
}

// Paging scrolls back to the feed, not to the page top.
const feedRef = ref<HTMLElement | null>(null);

useFetchData(
  load,
  [{ param: (p) => p.id, callback: () => load() }],
  [
    { query: (q) => q.rubric, callback: () => load() },
    { query: (q) => q.number, callback: () => load() },
  ],
);
</script>

<template>
  <div class="blog-feed">
    <div class="feed-header">
      <block-title>{{ heading }}</block-title>
      <router-link
        v-if="canManage"
        class="create-link"
        :to="{ name: 'blog-publication-create', params: { id: blogId } }"
      >
        Создать публикацию
      </router-link>
    </div>

    <template v-if="publicationsLoading && !publications.length">
      <GamePostSkeleton v-for="i in 3" :key="i" />
    </template>

    <ErrorState
      v-else-if="publicationsError"
      :message="publicationsError"
      :retry="load"
    />

    <SecondaryText v-else-if="!publications.length" class="feed-empty">
      Публикаций пока нет
    </SecondaryText>

    <div v-else ref="feedRef" class="feed-list">
      <div
        v-for="publication in publications"
        :key="publication.id"
        class="feed-item"
      >
        <!-- "Редактировать" is an action of the card's caption line. -->
        <PublicationCard
          :publication="publication"
          :blog-id="blogId"
          :edit-to="canManage ? editRoute(publication.id) : null"
          truncatable
        />
      </div>
    </div>

    <Paging
      v-if="publicationsPaging"
      :paging="publicationsPaging"
      :to="{
        name: 'blog-feed',
        params: { id: blogId },
        query: rubricId ? { rubric: rubricId } : {},
      }"
      :use-query="true"
      query-key="number"
      :scroll-anchor="() => feedRef"
    />
  </div>
</template>

<style scoped lang="sass">
.blog-feed
  display: flex
  flex-direction: column
  gap: $medium
  min-height: $grid-step * 50

.feed-header
  display: flex
  justify-content: space-between
  align-items: baseline
  gap: $medium
  flex-wrap: wrap

.create-link
  font-size: $secondary-font-size

.feed-empty
  padding: $big

.feed-list
  display: flex
  flex-direction: column
  gap: $medium
</style>
