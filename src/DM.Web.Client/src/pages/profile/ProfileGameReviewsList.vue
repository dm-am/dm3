<script setup lang="ts">
/**
 * ProfileGameReviewsList — the game-reviews flavor of
 * `ProfileTestimonialsFrame`. Used by both pages: "Полученные рецензии на
 * игры" and "Написанные рецензии на игры".
 *
 * What is local here:
 *  - endpoint: `getUserGameReviews` (received) vs `getWrittenUserGameReviews`
 *    (written);
 *  - the card: `GameReviewCard`, which sits in the same speech bubble as a
 *    recommendation — the text is visible at once instead of hiding behind an
 *    accordion row, and the authorship is links rather than a comma-joined
 *    string;
 *  - the texts, including the "written" relabel of the "Автор" sort option to
 *    "Игра": every review there is authored by the profile owner, so sorting
 *    by author is meaningless, and the backend already sorts this scope by the
 *    other side of the pair (GameReviewRepository.ApplySort). Only the label,
 *    the hint and the search placeholder change; the value sent stays
 *    "author".
 * Everything else — filter, fetch, states, paging — is the frame. The footer
 * names BOTH sides of the pair in both modes, the way the recommendation
 * lists do.
 */
import { computed } from "vue";
import { userApi, type GameReviewsQuery } from "@/entities/user";
import { GameReviewCard } from "@/entities/game";
import type { Username } from "@/shared/api/models/community";
import ProfileTestimonialsFrame from "./ProfileTestimonialsFrame.vue";

const props = defineProps<{
  username: string;
  /**
   * "received" — reviews of the games this user masters.
   * "written"  — reviews this user wrote.
   * Determines which of userApi.getUserGameReviews /
   * getWrittenUserGameReviews is called, and (via `routeName` below) where the
   * pagination links lead.
   */
  mode: "received" | "written";
  /** The current page's route name — for Paging.to. */
  routeName: "received-game-reviews" | "given-game-reviews";
}>();

// Do not detach the methods — both calls go through
// `this.buildListParams(q)`, and a detached `const fn = userApi.getX` loses
// `this` and crashes with a TypeError.
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
