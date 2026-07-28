<script setup lang="ts">
/**
 * StatBoard — one leaderboard card. Single source of truth for every
 * leaderboard consumer (the statistics page and the period-digest topics),
 * so they always render identical cards.
 *
 * Renders the dashed content card (poll/topic idiom), a plain-bold title, and
 * a ranked list of "N. <entity link> [+score]" rows (plain ordinal 1..N).
 * Scores are shown as "[+N]" with muted brackets and a green value; the
 * server guarantees only positive entries. Large scores (text volume runs
 * to millions of characters) render with locale digit grouping.
 *
 * On player boards the signed-in viewer's own name renders bold — only the
 * name, nothing else.
 *
 * `kind` maps each entry to its route: player -> profile (by username),
 * game/blog -> the entity page (by publicId, falling back to entityId).
 */
import type { RouteLocationRaw } from "vue-router";
import type { LeaderboardEntry } from "@/shared/api/models/community";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useAuthStore } from "@/shared/stores/auth";

const props = withDefaults(
  defineProps<{
    title: string;
    /** Positive-score entries, server-ranked, capped by the caller. */
    entries: LeaderboardEntry[];
    kind: "player" | "game" | "blog";
    /** Show shimmer rows instead of content while the period loads. */
    loading?: boolean;
    /** Message shown when the period has no positive entries. */
    emptyText?: string;
    /** Shimmer rows rendered while loading. */
    skeletonRows?: number;
  }>(),
  {
    loading: false,
    emptyText: "Данных за период пока нет",
    skeletonRows: 5,
  },
);

const authStore = useAuthStore();

function entryLink(entry: LeaderboardEntry): RouteLocationRaw {
  if (props.kind === "player")
    return { name: "profile", params: { username: entry.name } };
  return {
    name: props.kind === "game" ? "game" : "blog",
    params: { id: entry.publicId ?? entry.entityId },
  };
}

/** The signed-in viewer's own row (player boards only). */
function isOwn(entry: LeaderboardEntry): boolean {
  return props.kind === "player" && entry.name === authStore.user?.username;
}

/** Locale digit grouping ("1 234 567", non-breaking spaces). */
function formatScore(score: number): string {
  return score.toLocaleString("ru-RU");
}
</script>

<template>
  <section class="stat-board">
    <h3 class="stat-board-title">{{ title }}</h3>

    <ol v-if="loading" class="stat-board-list" aria-hidden="true">
      <li v-for="i in skeletonRows" :key="i" class="stat-board-skeleton-row">
        <span
          class="skeleton-bar"
          :class="`skeleton-bar--w${((i - 1) % 3) + 1}`"
        />
      </li>
    </ol>

    <SecondaryText v-else-if="!entries.length">{{ emptyText }}</SecondaryText>

    <ol v-else class="stat-board-list">
      <li
        v-for="(entry, idx) in entries"
        :key="entry.entityId"
        :class="{ own: isOwn(entry) }"
      >
        {{ entry.rank || idx + 1 }}.
        <router-link :to="entryLink(entry)">{{ entry.name }}</router-link
        ><span class="score">
          [<span class="score-pos">+{{ formatScore(entry.score) }}</span
          >]</span
        >
      </li>
    </ol>
  </section>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

// Dashed content card with the site element background (poll/topic idiom).
.stat-board
  border: 1px dashed $border
  background-color: $bg-element
  padding: $medium
  box-sizing: border-box

// Plain bold body-text heading (no heading colour/size, never brown).
.stat-board-title
  margin: 0 0 $small
  font-size: $font-size
  font-weight: bold
  color: $text

.stat-board-list
  list-style: none
  margin: 0
  padding: 0

  li
    line-height: 1.6

  // The viewer's own row (player boards): only the name is bold.
  li.own a
    font-weight: bold

// Score "[+N]" — muted brackets, green value (positive only).
.score
  color: $text-muted

.score-pos
  color: $accent-green

.stat-board-skeleton-row
  display: flex
  align-items: center
  height: 1.6em

.skeleton-bar
  display: inline-block
  height: 0.85em
  +skeleton-shimmer

  &--w1
    width: 220px
    max-width: 85%

  &--w2
    width: 160px
    max-width: 65%

  &--w3
    width: 190px
    max-width: 75%
</style>
