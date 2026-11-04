<script
  setup
  lang="ts"
  generic="TItem extends { id: string; title: string; createdUtc: string }"
>
/**
 * The body of a premoderation queue, oldest first. "Все" merges both
 * buckets: the list endpoints take one status per request, so the two go out
 * in parallel and the rows are merged by createdUtc.
 */
import { computed, onMounted, ref, type Ref } from "vue";
import type {
  ApiResult,
  ListEnvelope,
  PremoderationStatus,
} from "@/shared/api/models/common";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Select, type SelectOption } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { UserLink, type UserRef } from "@/entities/user";
import type { RouteLocationRaw } from "vue-router";
import { PREMODERATION_STATUS_LABELS } from "./lib/labels";

type QueueStatus = Exclude<PremoderationStatus, "Approved">;
type QueueRow = TItem & { premoderationStatus: QueueStatus };

const props = defineProps<{
  /** The heading, and the accessible name of the table under it. */
  title: string;
  /** Column set, including the owner column this page names its own way. */
  columns: Column[];
  /** Key of the owner column ("author" for a blog, "master" for a game). */
  ownerKey: string;
  /** What stands where the table would be when the queue is empty. */
  emptyText: string;
  /** What the error banner says when the queue could not be loaded. */
  errorText: string;
  /** One premoderation bucket from the server. */
  load: (status: QueueStatus) => Promise<ApiResult<ListEnvelope<TItem>>>;
  /** Where the title of a row leads. */
  titleRoute: (row: QueueRow) => RouteLocationRaw;
  /** Whose entry it is: the blog's author, the game's master. */
  owner: (row: QueueRow) => UserRef;
}>();

const rows = ref([]) as Ref<QueueRow[]>;
const loading = ref(false);
const loadError = ref<string | null>(null);

const statusFilter = ref<"" | QueueStatus>("");

const statusOptions: SelectOption[] = [
  { value: "", label: "Все статусы" },
  {
    value: "AwaitingApproval",
    label: PREMODERATION_STATUS_LABELS.AwaitingApproval,
  },
  { value: "AwaitingEdits", label: PREMODERATION_STATUS_LABELS.AwaitingEdits },
];

/** The slot DataTable calls for the owner cell, named by this page's column. */
const ownerCell = computed(() => `cell-${props.ownerKey}`);

async function fetchQueue(status: QueueStatus): Promise<QueueRow[]> {
  const { data, error } = await props.load(status);
  if (error) throw error;
  return (data?.resources ?? []).map((item) => ({
    ...item,
    premoderationStatus: status,
  }));
}

async function loadQueue() {
  loading.value = true;
  try {
    const buckets: QueueStatus[] = statusFilter.value
      ? [statusFilter.value]
      : ["AwaitingApproval", "AwaitingEdits"];
    const results = await Promise.all(buckets.map(fetchQueue));
    rows.value = results
      .flat()
      .sort(
        (a, b) =>
          new Date(a.createdUtc).getTime() - new Date(b.createdUtc).getTime(),
      );
    loadError.value = null;
  } catch {
    loadError.value = props.errorText;
  } finally {
    loading.value = false;
  }
}

function onFilterChange(value: string) {
  statusFilter.value = value as "" | QueueStatus;
  loadQueue();
}

const isEmpty = computed(() => !loading.value && rows.value.length === 0);

onMounted(loadQueue);
</script>

<template>
  <div>
    <page-title>{{ title }}</page-title>

    <div class="filters">
      <FormField label="Статус премодерации" name="premoderation-status">
        <Select
          id="premoderation-status"
          :model-value="statusFilter"
          :options="statusOptions"
          @update:model-value="onFilterChange"
        />
      </FormField>
    </div>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadQueue" />

    <SecondaryText v-else-if="isEmpty">{{ emptyText }}</SecondaryText>

    <DataTable
      v-else
      :columns="columns"
      :data="rows"
      :loading="loading"
      :empty-text="emptyText"
      :aria-label="title"
    >
      <template #cell-title="{ row }">
        <router-link :to="titleRoute(row)">
          {{ row.title }}
        </router-link>
      </template>
      <template #[ownerCell]="{ row }">
        <UserLink :user="owner(row)" />
      </template>
      <template #cell-created="{ row }">
        {{ formatDateFull(row.createdUtc) }}
      </template>
      <template #cell-status="{ row }">
        {{ PREMODERATION_STATUS_LABELS[row.premoderationStatus] }}
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
.filters
  max-width: 320px
  margin-bottom: $small
</style>
