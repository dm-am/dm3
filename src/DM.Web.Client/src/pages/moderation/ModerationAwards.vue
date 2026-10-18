<script setup lang="ts">
/**
 * ModerationAwards — the contest series list + create form + a link to
 * the award-type catalog. The main admin page for the "Награды" block.
 *
 * Reuses `useContestSeries()` (singleton cache) — after create/edit we
 * call reload() so every consumer (profile, other admin views) sees the
 * fresh list.
 */
import { computed, onMounted, reactive } from "vue";
import { useModal } from "vue-final-modal";
import {
  achievementApi,
  useContestSeries,
  formatContestSeriesTitle,
} from "@/entities/achievement";
import type { ContestSeries } from "@/shared/api/models/achievements";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import Button from "@/shared/ui/Button/Button.vue";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import ContestSeriesCreateDialog from "./dialogs/ContestSeriesCreateDialog.vue";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

const { hasAccess, deniedText } = useRoleGate(sectionRole("moderation-awards"));
const {
  adminSeries: series,
  loading,
  loadAdmin: load,
  reload,
} = useContestSeries();

onMounted(() => load());

const sortedSeries = computed(() =>
  [...(series.value ?? [])].sort((a, b) => {
    if (a.year !== b.year) return b.year - a.year;
    if (a.contestType !== b.contestType) {
      return a.contestType.localeCompare(b.contestType);
    }
    return b.number - a.number;
  }),
);

// The site's table, as a table whose rows are added in place is drawn
// (AttributeSchemaEditor): "+ Новая серия" right under it.
const columns: Column[] = [
  { key: "title", label: "Конкурс", align: "left" },
  { key: "year", label: "Год", align: "left" },
  { key: "topic", label: "Топик", align: "left" },
  { key: "status", label: "Статус", align: "left" },
  { key: "actions", label: "", align: "left" },
];

/** A hidden series reads dimmed; its action brings it back. */
function seriesRowClass(s: ContestSeries): string | undefined {
  return s.isActive ? undefined : "is-inactive";
}

// --- Create dialog (shared Dialog idiom) ---
const seriesList = computed(() => series.value ?? []);

const { open: openCreate, close: closeCreate } = useModal({
  component: ContestSeriesCreateDialog,
  attrs: reactive({
    series: seriesList,
    onSuccess: async () => {
      closeCreate();
      await reload();
    },
    onCancel: () => closeCreate(),
  }),
});

async function toggleActive(id: string, isActive: boolean) {
  await achievementApi.updateContestSeries(id, { isActive: !isActive });
  await reload();
}
</script>

<template>
  <page-title>Награды</page-title>

  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <section v-else class="awards-admin">
    <!-- The heading, and the way to the catalogue of the award types right
         under it. -->
    <header class="awards-admin__header">
      <BlockTitle>Серии конкурсов</BlockTitle>
      <router-link :to="{ name: 'moderation-award-types' }" class="link-button">
        Каталог типов наград
      </router-link>
    </header>

    <SecondaryText v-if="loading && !series">Загрузка...</SecondaryText>
    <SecondaryText v-else-if="sortedSeries.length === 0">
      Нет серий конкурсов
    </SecondaryText>

    <DataTable
      v-else
      class="series-table"
      :columns="columns"
      :data="sortedSeries"
      :row-class="seriesRowClass"
      aria-label="Серии конкурсов"
    >
      <template #cell-title="{ row }">
        <router-link
          :to="{ name: 'moderation-awards-series', params: { id: row.id } }"
        >
          {{ formatContestSeriesTitle(row.contestType, row.number) }}
        </router-link>
      </template>
      <template #cell-topic="{ row }">
        <a
          v-if="row.topicUrl"
          :href="row.topicUrl"
          target="_blank"
          rel="noopener"
        >
          открыть →
        </a>
        <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
      </template>
      <template #cell-status="{ row }">
        {{ row.isActive ? "Активна" : "Скрыта" }}
      </template>
      <template #cell-actions="{ row }">
        <button
          type="button"
          class="text-button"
          @click="toggleActive(row.id, row.isActive)"
        >
          {{ row.isActive ? "Скрыть" : "Вернуть" }}
        </button>
      </template>
    </DataTable>

    <!-- Opens the window that creates a series, where the main button is. -->
    <Button type="button" class="new-series" @click="openCreate">
      + Новая серия
    </Button>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.awards-admin
  display: flex
  flex-direction: column
  gap: $medium

// The heading over its row of buttons, the heading's own step under it.
.awards-admin__header
  display: block

// The site's table in the size this list has always had, its headers on the
// left as its cells are (Column.align).
.series-table
  font-size: $secondary-font-size

  :deep(.is-inactive)
    opacity: 0.5

  .muted
    color: $text-muted

// The shared secondary button, under the table it adds a row to, at its left
// edge.
.new-series
  align-self: flex-start

.link-button
  text-decoration: none
  +button

.text-button
  +inline-link-button
</style>
