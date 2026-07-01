<script setup lang="ts">
/**
 * UserRatedPostsList — общий список оцененных постов для двух страниц
 * профиля: «Оценки постов пользователя» (received) и «Оценил чужих
 * постов» (given). Зеркало UserEndorsementsList по архитектуре.
 *
 * Разница между режимами — только в том, как scope-параметр уходит на
 * сервер:
 *   - received → posts where this user is the AUTHOR
 *     (authorUsernames=username)
 *   - given    → posts where this user has at least one review
 *     (reviewerUsername=username) — фильтр добавлен в PostsQuery
 *     специально под эту страницу.
 *
 * Архитектура:
 *  - `usePulseFilter` управляет URL-state пользовательских фильтров
 *    (поиск, сортировка, рейтинг, даты). Опция «Авторы» скрывается
 *    через `hide-author-filter`, потому что author-scope гвоздями
 *    прибит маршрутом и должен оставаться вне поля редактирования.
 *  - `useFetchData` слушает route.query / username и перезапрашивает.
 *  - Серверная пагинация через `PagingWithSeparators`.
 *  - Рендер через `<GamePost show-navigation>` — тот же компонент,
 *    что и на /pulse, единая визуальная единица.
 */
import { ref, computed, type Ref } from "vue";
import { useRoute } from "vue-router";
import { gameApi } from "@/entities/game";
import type { Post } from "@/entities/game";
import type { ListEnvelope } from "@/shared/api/models/common";
import { PulseFilter, usePulseFilter } from "@/features/pulse-filter";
import { GamePost } from "@/widgets/game-post";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { SecondaryText } from "@/shared/ui/Layout";
import { useFetchData } from "@/shared/lib/composables/useFetchData";

const props = defineProps<{
  username: string;
  /**
   * "received" — посты, авторованные этим пользователем (он — author).
   * "given"    — посты, в которых этот пользователь оставил отзыв
   *              (он — reviewer).
   * Определяет, какой scope-параметр уходит в gameApi.getRatedPosts.
   */
  mode: "received" | "given";
  /** Имя route'а текущей страницы — для Paging.to. */
  routeName: "received-reviews" | "given-reviews";
}>();

const route = useRoute();
const { filterState, searchParams, hasActiveFilters } = usePulseFilter();

const envelope: Ref<ListEnvelope<Post> | null> = ref(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

async function fetch() {
  loading.value = true;
  try {
    const apiParams: Parameters<typeof gameApi.getRatedPosts>[0] = {
      sortBy: searchParams.value.sortBy ?? "lastreview",
      sortOrder: searchParams.value.sortOrder ?? "desc",
      hasReviews: true,
    };

    // User-facing filters (search/rating/dates/game) — общая логика
    // c /pulse. Author-фильтр сюда сознательно не пробрасываем: scope
    // навязан маршрутом и проставляется ниже.
    if (searchParams.value.search) apiParams.search = searchParams.value.search;
    if (searchParams.value.minRating !== undefined)
      apiParams.minRating = searchParams.value.minRating;
    if (searchParams.value.maxRating !== undefined)
      apiParams.maxRating = searchParams.value.maxRating;
    if (searchParams.value.createdFrom)
      apiParams.createdAfter = new Date(
        `${searchParams.value.createdFrom}T00:00:00Z`,
      ).toISOString();
    if (searchParams.value.createdTo)
      apiParams.createdBefore = new Date(
        `${searchParams.value.createdTo}T23:59:59.999Z`,
      ).toISOString();
    if (searchParams.value.gameId) apiParams.gameId = searchParams.value.gameId;

    // Scope — единственное, чем отличаются режимы.
    if (props.mode === "received") {
      apiParams.authorUsernames = props.username;
    } else {
      apiParams.reviewerUsername = props.username;
    }

    // Пагинация (server-side). `searchParams.size` всегда заполнен
    // `usePulseFilter` из `entitiesPerPage` → fallback не нужен.
    const pageSize = searchParams.value.size!;
    apiParams.take = pageSize;
    if (searchParams.value.number && searchParams.value.number > 1) {
      apiParams.skip = (searchParams.value.number - 1) * pageSize;
    }

    const { data, error } = await gameApi.getRatedPosts(apiParams);
    if (error) {
      // Keep any already-shown posts (stale-while-revalidate); the
      // template surfaces the error line only when there's nothing to show.
      loadError.value = "Не удалось загрузить оцененные посты";
    } else {
      loadError.value = null;
      envelope.value = (data as ListEnvelope<Post>) ?? null;
    }
  } finally {
    loading.value = false;
  }
}

useFetchData(
  () => fetch(),
  [
    {
      // URL + username полностью определяют запрос. Используем JSON
      // строку чтобы `useFetchData` сравнил по значению.
      param: () =>
        JSON.stringify({ q: route.query, u: props.username, m: props.mode }),
      callback: () => fetch(),
    },
  ],
);

const items = computed(() => envelope.value?.resources ?? []);
const paging = computed(() => envelope.value?.paging ?? null);
const isEmpty = computed(
  () => envelope.value !== null && items.value.length === 0,
);

const emptyText = computed(() => {
  if (hasActiveFilters.value) return "Постов по заданным фильтрам не найдено";
  return props.mode === "received"
    ? "У пользователя пока нет оцененных постов"
    : "Пользователь пока никого не оценивал";
});

const pagingTo = computed(() => ({
  name: props.routeName,
  params: { username: props.username },
}));
</script>

<template>
  <div class="user-rated-posts-list">
    <PulseFilter class="filters" hide-author-filter />

    <GamePostSkeleton v-if="loading && !envelope" :count="5" />

    <div v-else-if="loadError && !envelope" class="error-message">
      {{ loadError }}
    </div>

    <SecondaryText v-else-if="isEmpty">
      {{ emptyText }}
    </SecondaryText>

    <template v-else>
      <PagingWithSeparators
        v-if="paging && paging.pages && paging.pages > 1"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
      />

      <div class="posts-list">
        <GamePost
          v-for="post in items"
          :key="post.id"
          :post="post"
          show-navigation
          :search-query="filterState.search"
        />
      </div>

      <PagingWithSeparators
        v-if="paging && paging.pages && paging.pages > 1"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.user-rated-posts-list
  display: flex
  flex-direction: column
  gap: $small

.filters
  margin-bottom: $medium

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius

.posts-list
  display: flex
  flex-direction: column
  gap: $medium
</style>
