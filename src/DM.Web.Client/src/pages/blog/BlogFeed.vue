<script setup lang="ts">
// Publication feed sub-page (dev doc 4.2.3.6.2 "Рубрика"): the latest
// publications in reverse chronological order, optionally filtered by
// rubric via the ?rubric= query (URL_STRUCTURE: /blogs/{id}/feed?rubric=).
// The rubric page IS the feed page with a filter — one component serves
// both. Publications render through PublicationCard (the topic-copy card;
// its internals are the publication entity's concern, not this page's).
//
// States per UI_STANDARDS: skeleton -> error(retry) -> empty -> content.
// GamePostSkeleton is the established stand-in for a publication card
// (see ProfileBestPublication) — no new skeleton needed.
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

// Page heading: the rubric title when filtered, the generic feed otherwise.
const rubric = computed(() =>
  rubricId.value ? rubrics.value.find((r) => r.id === rubricId.value) : null,
);
const heading = computed(() => rubric.value?.title ?? "Лента публикаций");

// The shared Paging widget writes the page as ?number= (codebase-wide
// query-key convention) — read the same key back.
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

// Paging scrolls the feed back into view (not the page top)
const feedRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return feedRef.value;
}

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

    <!-- 1. loading -->
    <template v-if="publicationsLoading && !publications.length">
      <GamePostSkeleton v-for="i in 3" :key="i" />
    </template>

    <!-- 2. error -->
    <ErrorState
      v-else-if="publicationsError"
      :message="publicationsError"
      :retry="load"
    />

    <!-- 3. empty -->
    <SecondaryText v-else-if="!publications.length" class="feed-empty">
      Публикаций пока нет
    </SecondaryText>

    <!-- 4. content -->
    <div v-else ref="feedRef" class="feed-list">
      <div
        v-for="publication in publications"
        :key="publication.id"
        class="feed-item"
      >
        <PublicationCard
          :publication="publication"
          :blog-id="blogId"
          truncatable
        />
        <div v-if="canManage" class="feed-item-actions">
          <router-link
            :to="{
              name: 'blog-publication-edit',
              params: { id: blogId, pubId: publication.id },
            }"
          >
            Редактировать
          </router-link>
        </div>
      </div>
    </div>

    <!-- Paging -->
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
      :scroll-anchor="pagingAnchor"
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
  color: $link
  font-size: $secondary-font-size

  &:hover
    color: $link-hover
    text-decoration: underline

.feed-empty
  padding: $big

.feed-list
  display: flex
  flex-direction: column
  gap: $medium

.feed-item-actions
  margin-top: $tiny
  text-align: right
  font-size: $secondary-font-size

  a
    color: $link

    &:hover
      color: $link-hover
      text-decoration: underline
</style>
