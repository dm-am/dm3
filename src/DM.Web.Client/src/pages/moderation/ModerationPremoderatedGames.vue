<script setup lang="ts">
/**
 * ModerationPremoderatedGames — "Премодерируемые игры" (doc 4.2.3.8.2).
 * Games with premoderation status != Approved, oldest first. A status
 * filter narrows to a single premoderation bucket; "Все" merges both
 * queues (the games list endpoint takes one status set per request,
 * so "Все" issues the two queries in parallel and merges by createdUtc).
 */
import { computed, onMounted, ref } from "vue";
import {
  moderationApi,
  type PremoderatedGame,
  type PremoderationStatus,
} from "@/entities/moderation";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Select, type SelectOption } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { UserLink } from "@/entities/user";
import { useRoleGate } from "./lib/useRoleGate";
import { PREMODERATION_STATUS_LABELS } from "./lib/labels";

type QueueStatus = Exclude<PremoderationStatus, "Approved">;
type GameRow = PremoderatedGame & { premoderationStatus: QueueStatus };

const { hasAccess, deniedText } = useRoleGate("Moderator");

const rows = ref<GameRow[]>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);

// "" = both premoderation buckets
const statusFilter = ref<"" | QueueStatus>("");

const statusOptions: SelectOption[] = [
  { value: "", label: "Все статусы" },
  {
    value: "AwaitingApproval",
    label: PREMODERATION_STATUS_LABELS.AwaitingApproval,
  },
  { value: "AwaitingEdits", label: PREMODERATION_STATUS_LABELS.AwaitingEdits },
];

const columns: Column[] = [
  { key: "title", label: "Игра" },
  { key: "master", label: "Мастер", width: "20%" },
  { key: "created", label: "Создана", width: "20%", hideOnMobile: true },
  { key: "status", label: "Статус премодерации", width: "20%" },
];

async function fetchQueue(status: QueueStatus): Promise<GameRow[]> {
  const { data, error } = await moderationApi.getPremoderatedGames(status);
  if (error) throw error;
  return (data?.resources ?? []).map((g) => ({
    ...g,
    premoderationStatus: status,
  }));
}

async function fetch() {
  loading.value = true;
  try {
    const buckets: QueueStatus[] = statusFilter.value
      ? [statusFilter.value]
      : ["AwaitingApproval", "AwaitingEdits"];
    const results = await Promise.all(buckets.map(fetchQueue));
    // Oldest first across the merged buckets (doc: "самые старые вверху")
    rows.value = results
      .flat()
      .sort(
        (a, b) =>
          new Date(a.createdUtc).getTime() - new Date(b.createdUtc).getTime(),
      );
    loadError.value = null;
  } catch {
    loadError.value = "Не удалось загрузить премодерируемые игры";
  } finally {
    loading.value = false;
  }
}

function onFilterChange(value: string) {
  statusFilter.value = value as "" | QueueStatus;
  fetch();
}

const isEmpty = computed(() => !loading.value && rows.value.length === 0);

onMounted(fetch);
</script>

<template>
  <div class="premoderated-games">
    <page-title>Премодерируемые игры</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <template v-else>
      <div class="filters">
        <FormField label="Статус премодерации" name="premoderation-status">
          <Select
            :model-value="statusFilter"
            :options="statusOptions"
            @update:model-value="onFilterChange"
          />
        </FormField>
      </div>

      <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

      <SecondaryText v-else-if="isEmpty">
        Премодерируемых игр пока нет
      </SecondaryText>

      <DataTable
        v-else
        :columns="columns"
        :data="rows"
        :loading="loading"
        empty-text="Премодерируемых игр пока нет"
        aria-label="Премодерируемые игры"
      >
        <template #cell-title="{ row }">
          <router-link
            :to="{ name: 'game', params: { id: row.publicId || row.id } }"
          >
            {{ row.title }}
          </router-link>
        </template>
        <template #cell-master="{ row }">
          <UserLink :user="row.master" />
        </template>
        <template #cell-created="{ row }">
          {{ formatDateFull(row.createdUtc) }}
        </template>
        <template #cell-status="{ row }">
          {{ PREMODERATION_STATUS_LABELS[row.premoderationStatus] }}
        </template>
      </DataTable>
    </template>
  </div>
</template>

<style scoped lang="sass">
.filters
  max-width: 320px
  margin-bottom: $small
</style>
