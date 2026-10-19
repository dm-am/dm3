<script setup lang="ts">
// One card for the statistics page and the digest topics; scores use locale digit grouping.
import type { RouteLocationRaw } from "vue-router";
import type { LeaderboardEntry } from "@/shared/api/models/community";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useAuthStore } from "@/shared/stores/auth";
import type { LeaderboardBoardDef } from "../model/boards";

const props = withDefaults(
  defineProps<{
    title: string;
    entries: LeaderboardEntry[];
    kind: LeaderboardBoardDef["kind"];
    loading?: boolean;
    emptyText?: string;
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

function isOwn(entry: LeaderboardEntry): boolean {
  return props.kind === "player" && entry.name === authStore.user?.username;
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
          [<span class="score-pos"
            >+{{ entry.score.toLocaleString("ru-RU") }}</span
          >]</span
        >
      </li>
    </ol>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

// As wide as its column and never wider: a long name breaks at the edge, as a login does in text.
.stat-board
  min-width: 0
  border: 1px dashed $border
  background-color: $bg-element
  padding: $medium
  box-sizing: border-box
  overflow-wrap: break-word

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

  li.own a
    font-weight: bold

// The space before "[" is the score's own and unbreakable, so a line never opens with "[+16]".
.score
  color: $text-muted
  white-space: nowrap

.score-pos
  color: $accent-green

.stat-board-skeleton-row
  display: flex
  align-items: center
  height: 1.6em

// min(), not a px width under a percent max-width: a grid sizes columns by the px width.
.skeleton-bar
  display: inline-block
  height: 0.85em
  +skeleton-shimmer

  &--w1
    width: min(220px, 85%)

  &--w2
    width: min(160px, 65%)

  &--w3
    width: min(190px, 75%)
</style>
