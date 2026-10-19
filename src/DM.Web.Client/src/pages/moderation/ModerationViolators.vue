<script setup lang="ts">
/**
 * Users with active warning points or an active ban, most points first. The
 * segments make the switch quick, so two answers can be on the wire; the guard
 * keeps the one asked for last.
 */
import { computed, onMounted, ref } from "vue";
import {
  moderationApi,
  type Violator,
  type ViolatorsFilter,
} from "@/entities/moderation";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { ErrorState } from "@/shared/ui/ErrorState";
import {
  SegmentedControl,
  type SegmentedOption,
} from "@/shared/ui/SegmentedControl";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { UserLink } from "@/entities/user";
import { banTermLabel } from "./lib/labels";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const violators = ref<Violator[]>([]);

const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить нарушителей" });

const filter = ref<ViolatorsFilter>("all");

const filterOptions: SegmentedOption<ViolatorsFilter>[] = [
  { value: "all", label: "Все" },
  { value: "banned", label: "С баном" },
  { value: "points-only", label: "Только баллы" },
];

// "Пользователь" takes what the others leave; shares are of the whole table, padding included.
const columns: Column[] = [
  { key: "user", label: "Пользователь" },
  { key: "points", label: "Баллы", width: "17%", align: "center" },
  { key: "lastWarning", label: "Последнее предупреждение", width: "27%" },
  { key: "ban", label: "Активный бан", width: "27%" },
];

function loadViolators() {
  return run(
    () => moderationApi.getViolators(filter.value),
    (data) => {
      violators.value = data?.resources ?? [];
    },
  );
}

function onFilterChange(value: ViolatorsFilter) {
  if (value === filter.value) return;
  filter.value = value;
  loadViolators();
}

onMounted(loadViolators);

// DataTable keys its rows by id: the violator's user id.
const rows = computed(() =>
  violators.value.map((v) => ({ ...v, id: v.user.id })),
);

// Nobody under a segment is not an empty list of violators.
const emptyText = computed(() =>
  filter.value === "all"
    ? "Нарушителей пока нет"
    : "Нарушителей по заданным фильтрам не найдено",
);
</script>

<template>
  <div class="moderation-violators">
    <page-title>Нарушители</page-title>

    <div class="filter-line">
      <SegmentedControl
        :model-value="filter"
        :options="filterOptions"
        ariaLabel="Фильтр нарушителей"
        @update:model-value="onFilterChange"
      />
    </div>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadViolators" />

    <DataTable
      v-else
      :columns="columns"
      :data="rows"
      :loading="loading"
      :empty-text="emptyText"
      aria-label="Нарушители"
    >
      <template #cell-user="{ row }">
        <UserLink :user="row.user" />
      </template>
      <template #cell-points="{ row }">
        <span
          class="points"
          :class="{ 'points-critical': row.points >= row.pointsThreshold }"
        >
          {{ row.points }}/{{ row.pointsThreshold }}
        </span>
      </template>
      <template #cell-lastWarning="{ row }">
        <span v-if="row.lastWarningUtc">
          {{ formatDateFull(row.lastWarningUtc) }}
        </span>
        <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
      </template>
      <template #cell-ban="{ row }">
        <span v-if="row.activeBan" class="ban-info">
          {{ banTermLabel(row.activeBan) }}
          <template v-if="row.activeBan.expiresUtc">
            (до {{ formatDateFull(row.activeBan.expiresUtc) }})
          </template>
        </span>
        <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
.filter-line
  margin-bottom: $medium

.points-critical
  color: $accent-red
</style>
