<script setup lang="ts">
/**
 * UserEndorsementsList — общий список рекомендаций с фильтром, поиском,
 * сортировкой и пагинацией. Используется обеими страницами:
 * «Полученные рекомендации» и «Написанные рекомендации».
 *
 * Различие между режимами — только в endpoint'е (`getUserEndorsements`
 * vs `getWrittenUserEndorsements`) и в имени route'а для Paging-ссылок.
 * Все остальное — отображение, фильтр, состояние URL — общее.
 *
 * Архитектура:
 *  - `useReviewsFilter` управляет URL-state'ом (поиск + sort).
 *  - `useFetchData` слушает route.query и перевыполняет fetch.
 *  - Серверная пагинация — paging-инфо приходит из ListEnvelope.
 *  - Рендерим через `<Testimonial>` (тот же компонент, что и на
 *    /about/testimonials и на табе рекомендаций — единая визуальная
 *    единица для всех «отзывных» сущностей).
 */
import { ref, computed, type Ref } from "vue";
import { useRoute } from "vue-router";
import { communityApi } from "@/shared/api";
import type {
  UserEndorsement,
  Username,
  WebsiteTestimonial,
} from "@/shared/api/models/community";
import type { ListEnvelope } from "@/shared/api/models/common";
import { ReviewsFilter, useReviewsFilter } from "@/features/review-filter";
import { Testimonial } from "@/entities/testimonial";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { SecondaryText } from "@/shared/ui/Layout";
import { useFetchData } from "@/shared/lib/composables/useFetchData";

const props = defineProps<{
  username: string;
  /**
   * "received" — рекомендации, полученные этим пользователем (он — recipient).
   * "written"  — рекомендации, написанные этим пользователем (он — author).
   * Определяет endpoint и через RouteContext выбирает имя route'а для
   * Paging-ссылок (чтобы кнопки страниц не теряли контекст).
   */
  mode: "received" | "written";
  /** Имя route'а текущей страницы — для Paging.to. */
  routeName: "received-endorsements" | "given-endorsements";
}>();

const route = useRoute();

const { filterState, searchParams, hasActiveFilters } = useReviewsFilter();

const envelope: Ref<ListEnvelope<UserEndorsement> | null> = ref(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

async function fetch() {
  loading.value = true;
  try {
    // Не разрушаем метод — `getUserEndorsements`/`getWrittenUserEndorsements`
    // дергают `this.buildEndorsementParams(q)`, а отсоединенный `const fn =
    // communityApi.getX` теряет `this` и валится TypeError'ом, который тихо
    // съедается catch-блоком ниже.
    const params = {
      search: searchParams.value.search,
      sortBy: searchParams.value.sortBy,
      sortOrder: searchParams.value.sortOrder,
      number: searchParams.value.number,
      take: searchParams.value.size,
    };
    const { data, error } =
      props.mode === "received"
        ? await communityApi.getUserEndorsements(
            props.username as Username,
            params,
          )
        : await communityApi.getWrittenUserEndorsements(
            props.username as Username,
            params,
          );
    if (error) {
      // Keep any already-shown items (stale-while-revalidate); the
      // template surfaces the error line only when there's nothing to show.
      loadError.value = "Не удалось загрузить рекомендации";
    } else {
      loadError.value = null;
      envelope.value = data ?? null;
    }
  } finally {
    loading.value = false;
  }
}

useFetchData(
  () => fetch(),
  [
    {
      // URL — единственный источник правды для filter/paging:
      // изменился route.query → перезапрашиваем. Username входит через
      // замыкание props, поэтому также покрыт.
      param: () => JSON.stringify({ q: route.query, u: props.username }),
      callback: () => fetch(),
    },
  ],
);

const items = computed(() => envelope.value?.resources ?? []);
const paging = computed(() => envelope.value?.paging ?? null);
const isEmpty = computed(
  () => envelope.value !== null && items.value.length === 0,
);

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Рекомендаций по заданным фильтрам не найдено"
    : props.mode === "received"
      ? "У пользователя пока нет рекомендаций"
      : "Пользователь пока не писал рекомендаций",
);

/**
 * UserEndorsement и WebsiteTestimonial структурно совместимы
 * (id / author / text / createdUtc / modifiedUtc). `<Testimonial>`
 * читает только эти поля — проецируем на границе вместо дублирования
 * визуала. То же приведение делает существующий ProfileEndorsements.vue.
 */
function asTestimonial(e: UserEndorsement): WebsiteTestimonial {
  return e as unknown as WebsiteTestimonial;
}

const pagingTo = computed(() => ({
  name: props.routeName,
  params: { username: props.username },
}));
</script>

<template>
  <div class="user-endorsements-list">
    <ReviewsFilter />

    <SecondaryText v-if="loading && !envelope">Загрузка…</SecondaryText>

    <div v-else-if="loadError && !envelope" class="error-message">
      {{ loadError }}
    </div>

    <SecondaryText v-else-if="isEmpty">
      {{ emptyText }}
    </SecondaryText>

    <div v-else class="list">
      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
      />

      <template v-for="(item, idx) in items" :key="item.id">
        <Testimonial
          :testimonial="asTestimonial(item)"
          :search-query="filterState.search"
        />
        <div v-if="idx < items.length - 1" class="separator">
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - -
        </div>
      </template>

      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.user-endorsements-list
  display: flex
  flex-direction: column
  gap: $small

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius

.list
  display: flex
  flex-direction: column
  gap: $tiny
  margin-top: $medium

.separator
  margin: $tiny 0
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: 100%
  width: 0
  min-width: 100%
  user-select: none
</style>
