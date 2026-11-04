<script setup lang="ts">
/** The contest series; after a write reload() refreshes the shared cache of useContestSeries. */
import { computed, onMounted, reactive, ref } from "vue";
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
import { refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ErrorState } from "@/shared/ui/ErrorState";
import ContestSeriesCreateDialog from "./dialogs/ContestSeriesCreateDialog.vue";

const {
  adminSeries: series,
  loading,
  adminFailed: failed,
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

// "+ Новая серия" under the table, as in AttributeSchemaEditor.
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

/** A refused "Скрыть" or "Вернуть", on a line under the row it was pressed in. */
const toggleRefusal = ref<{ id: string; text: string } | null>(null);

async function toggleActive(id: string, isActive: boolean) {
  toggleRefusal.value = null;
  const { error } = await achievementApi.updateContestSeries(id, {
    isActive: !isActive,
  });
  if (error) {
    const text = refusalHere(
      error,
      isActive ? "Не удалось скрыть серию" : "Не удалось вернуть серию",
    );
    if (text) toggleRefusal.value = { id, text };
    return;
  }
  await reload();
}
</script>

<template>
  <page-title>Награды</page-title>

  <section class="awards-admin">
    <!-- The heading, and the way to the catalogue of award types under it. -->
    <header class="awards-admin__header">
      <BlockTitle>Серии конкурсов</BlockTitle>
      <router-link :to="{ name: 'moderation-award-types' }" class="link-button">
        Каталог типов наград
      </router-link>
    </header>

    <ErrorState
      v-if="failed"
      message="Не удалось загрузить серии конкурсов"
      :retry="() => load(true)"
    />

    <SecondaryText
      v-else-if="series !== null && !loading && !sortedSeries.length"
    >
      Нет серий конкурсов
    </SecondaryText>

    <!-- Unread, the table draws its skeleton rows. -->
    <DataTable
      v-else
      class="series-table"
      :columns="columns"
      :data="sortedSeries"
      :loading="loading || series === null"
      :row-class="seriesRowClass"
      :details-for="toggleRefusal?.id ?? null"
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
      <template #details>
        <ActionResult :text="toggleRefusal?.text" class="series-refusal" />
      </template>
    </DataTable>

    <Button type="button" class="new-series" @click="openCreate">
      + Новая серия
    </Button>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *
@use "@/assets/styles/Links" as *

.awards-admin
  display: flex
  flex-direction: column
  gap: $medium

.awards-admin__header
  display: block

// Headers on the left, as the cells are (Column.align).
.series-table
  font-size: $secondary-font-size

  :deep(.is-inactive)
    opacity: $muted-opacity

  .muted
    color: $text-muted

// Under the table it adds a row to, at its left edge.
.new-series
  align-self: flex-start

// The line is the cell's only content: the cell's padding stands around it.
.series-refusal
  --action-result-margin: 0

.link-button
  text-decoration: none
  +button

.text-button
  +inline-link-button
</style>
