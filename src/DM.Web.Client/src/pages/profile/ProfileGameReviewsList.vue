<script setup lang="ts">
/**
 * The game reviews of a profile on ProfileTestimonialsFrame, received or
 * written. In "written" every review is the owner's, so the "Автор" sort is
 * relabelled "Игра"; the server sorts that scope by the other side and the
 * value sent stays "author".
 */
import { computed } from "vue";
import { userApi, type GameReviewsQuery } from "@/entities/user";
import { GameReviewCard } from "@/entities/game";
import type { Username } from "@/shared/api/models/community";
import ProfileTestimonialsFrame from "./ProfileTestimonialsFrame.vue";
import type {
  GameReviewsSubpageName,
  TestimonialSide,
} from "./profileSubpages";

const props = defineProps<{
  username: string;
  /** Received: reviews of the games this user masters; written: the reviews he wrote. */
  mode: TestimonialSide;
  /** The route of the page, for the paging links. */
  routeName: GameReviewsSubpageName;
}>();

function fetchPage(q: GameReviewsQuery) {
  return props.mode === "received"
    ? userApi.getUserGameReviews(props.username as Username, q)
    : userApi.getWrittenUserGameReviews(props.username as Username, q);
}

const emptyText = computed(() =>
  props.mode === "received"
    ? "На игры пользователя пока не писали рецензий"
    : "Пользователь пока не писал рецензий на игры",
);

const authorSortOverride = computed(() =>
  props.mode === "written"
    ? { label: "Игра", hint: "По названию игры" }
    : undefined,
);

const searchPlaceholder = computed(() =>
  props.mode === "written"
    ? "Поиск по тексту или игре"
    : "Поиск по тексту, автору или игре",
);
</script>

<template>
  <ProfileTestimonialsFrame
    :username="username"
    :mode="mode"
    :route-name="routeName"
    error-message="Не удалось загрузить рецензии"
    :empty-text="emptyText"
    empty-filtered-text="Рецензий по заданным фильтрам не найдено"
    :author-sort-override="authorSortOverride"
    :search-placeholder="searchPlaceholder"
    :fetch-page="fetchPage"
  >
    <template #item="{ item, searchQuery }">
      <GameReviewCard :review="item" :search-query="searchQuery" />
    </template>
  </ProfileTestimonialsFrame>
</template>
