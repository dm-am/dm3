<script setup lang="ts">
/**
 * ModerationViolators — "Нарушители" (doc 4.2.3.8.6).
 * Users with active warning points or an active ban, sorted by points
 * descending (GET v1/moderation/violators). Filter: all / with active
 * ban / points without a ban. Row: name, points "N/6", last warning
 * date, active ban details.
 */
import { computed, onMounted, ref } from "vue";
import ModerationApi, {
  type Violator,
  type ViolatorsFilter,
} from "@/shared/api/moderationApi";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Select, type SelectOption } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { UserLink } from "@/entities/user";
import { useRoleGate } from "./lib/useRoleGate";
import { banTermLabel } from "./lib/labels";

const { hasAccess } = useRoleGate("Moderator");

const violators = ref<Violator[]>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);

const filter = ref<ViolatorsFilter>("all");

const filterOptions: SelectOption[] = [
  { value: "all", label: "Все" },
  { value: "banned", label: "С активным баном" },
  { value: "points-only", label: "С баллами без бана" },
];

const columns: Column[] = [
  { key: "user", label: "Пользователь" },
  { key: "points", label: "Баллы", width: "15%", align: "center" },
  { key: "lastWarning", label: "Последнее предупреждение", width: "25%" },
  { key: "ban", label: "Активный бан", width: "25%" },
];

async function fetch() {
  loading.value = true;
  const { data, error } = await ModerationApi.getViolators(filter.value);
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить нарушителей";
    return;
  }
  loadError.value = null;
  violators.value = data?.resources ?? [];
}

function onFilterChange(value: string) {
  filter.value = value as ViolatorsFilter;
  fetch();
}

onMounted(fetch);

// DataTable rows keyed by the violator's user id
const rows = computed(() =>
  violators.value.map((v) => ({ ...v, id: v.user.id })),
);
</script>

<template>
  <div class="moderation-violators">
    <page-title>Нарушители</page-title>

    <SecondaryText v-if="!hasAccess">
      Страница доступна только модераторам
    </SecondaryText>

    <template v-else>
      <div class="filters">
        <FormField label="Фильтр" name="violators-filter">
          <Select
            :model-value="filter"
            :options="filterOptions"
            @update:model-value="onFilterChange"
          />
        </FormField>
      </div>

      <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

      <DataTable
        v-else
        :columns="columns"
        :data="rows"
        :loading="loading"
        empty-text="Нарушителей пока нет"
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
          <span v-else class="muted">—</span>
        </template>
        <template #cell-ban="{ row }">
          <span v-if="row.activeBan" class="ban-info">
            {{ banTermLabel(row.activeBan) }}
            <template v-if="row.activeBan.expiresUtc">
              (до {{ formatDateFull(row.activeBan.expiresUtc) }})
            </template>
          </span>
          <span v-else class="muted">—</span>
        </template>
      </DataTable>
    </template>
  </div>
</template>

<style scoped lang="sass">
.filters
  max-width: 320px
  margin-bottom: $small

.points-critical
  color: $accent-red

.muted
  color: $text-muted
</style>
