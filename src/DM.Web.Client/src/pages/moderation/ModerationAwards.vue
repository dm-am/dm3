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
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import ContestSeriesCreateDialog from "./dialogs/ContestSeriesCreateDialog.vue";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess, deniedText } = useRoleGate("SeniorModerator");
const { series, loading, load, reload } = useContestSeries();

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
  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <section v-else class="awards-admin">
    <header class="awards-admin__header">
      <BlockTitle>Серии конкурсов</BlockTitle>
      <div class="awards-admin__actions">
        <router-link
          :to="{ name: 'moderation-award-types' }"
          class="link-button"
        >
          Каталог типов наград
        </router-link>
        <button type="button" class="primary-button" @click="openCreate">
          + Новая серия
        </button>
      </div>
    </header>

    <SecondaryText v-if="loading && !series">Загрузка...</SecondaryText>
    <SecondaryText v-else-if="sortedSeries.length === 0">
      Нет серий конкурсов
    </SecondaryText>

    <table v-else class="series-table">
      <thead>
        <tr>
          <th>Конкурс</th>
          <th>Год</th>
          <th>Топик</th>
          <th>Статус</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="s in sortedSeries"
          :key="s.id"
          :class="{ 'is-inactive': !s.isActive }"
        >
          <td>
            <router-link
              :to="{ name: 'moderation-awards-series', params: { id: s.id } }"
            >
              {{ formatContestSeriesTitle(s.contestType, s.number) }}
            </router-link>
          </td>
          <td>{{ s.year }}</td>
          <td>
            <a
              v-if="s.topicUrl"
              :href="s.topicUrl"
              target="_blank"
              rel="noopener"
            >
              открыть →
            </a>
            <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
          </td>
          <td>{{ s.isActive ? "Активна" : "Скрыта" }}</td>
          <td>
            <button
              type="button"
              class="text-button"
              @click="toggleActive(s.id, s.isActive)"
            >
              {{ s.isActive ? "Скрыть" : "Вернуть" }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/Tables"

.awards-admin
  display: flex
  flex-direction: column
  gap: $medium

  &__header
    display: flex
    align-items: center
    justify-content: space-between
    gap: $medium

  &__actions
    display: flex
    align-items: center
    gap: $small

// Site table idiom: 1px gaps painted by the $border background
// (same visual language as DataTable / +table)
.series-table
  width: 100%
  border-collapse: separate
  border-spacing: 1px
  background-color: $border
  font-size: $secondary-font-size

  th
    text-align: left
    +table-header

  td
    text-align: left
    vertical-align: middle
    +table-row

  tbody tr
    +table-row-hover

  .is-inactive
    opacity: 0.5

  .muted
    color: $text-muted

.primary-button
  +button

.link-button
  text-decoration: none
  +button

.text-button
  +inline-link-button
</style>
