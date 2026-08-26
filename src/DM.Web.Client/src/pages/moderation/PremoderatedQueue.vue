<script
  setup
  lang="ts"
  generic="TItem extends { id: string; title: string; createdUtc: string }"
>
/**
 * PremoderatedQueue — the body of a premoderation review queue: everything
 * awaiting a moderator, oldest first, with a status filter over the two
 * premoderation buckets.
 *
 * "Все" merges both queues because the list endpoints take one status set per
 * request: the two queries go out in parallel and the rows are merged by
 * createdUtc. The page above supplies what legitimately differs — its heading
 * and column labels, the list call, where a row's title leads and who its
 * owner is (a blog has an author, a game has a master).
 *
 * Used by ModerationPremoderatedBlogs (doc 4.2.1.5) and
 * ModerationPremoderatedGames (doc 4.2.3.8.2), which were one file written
 * twice down to the merge and the sort.
 */
import { computed, onMounted, ref, type Ref } from "vue";
import type { PremoderationStatus } from "@/entities/moderation";
import type { ApiResult, ListEnvelope } from "@/shared/api/models/common";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Select, type SelectOption } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { UserLink, type UserRef } from "@/entities/user";
import type { RouteLocationRaw } from "vue-router";
import { useRoleGate } from "./lib/useRoleGate";
import { PREMODERATION_STATUS_LABELS } from "./lib/labels";

type QueueStatus = Exclude<PremoderationStatus, "Approved">;
type QueueRow = TItem & { premoderationStatus: QueueStatus };

const props = defineProps<{
  /** Class of the page root, kept per page for the inspector. */
  rootClass: string;
  /** The heading, and the accessible name of the table under it. */
  title: string;
  /** Column set, including the owner column this page names its own way. */
  columns: Column[];
  /** Key of the owner column ("author" for a blog, "master" for a game). */
  ownerKey: string;
  /**
   * What stands where the table would be when the queue is empty.
   *
   * Drawn as `{{ " " }}{{ emptyText }}{{ " " }}`, which is not decoration: the
   * two pages this replaced wrote the sentence out on its own line, and the
   * compiler condenses the newlines around a text node into the spaces the
   * node keeps. An interpolation written the same way loses them instead —
   * whitespace-only siblings at the edges of an element are dropped — and the
   * rendered node stops being the one the pages rendered.
   */
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

const { hasAccess, deniedText } = useRoleGate("Moderator");

const rows = ref([]) as Ref<QueueRow[]>;
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
    loadError.value = props.errorText;
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
  <div :class="rootClass">
    <page-title>{{ title }}</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <template v-else>
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

      <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

      <SecondaryText v-else-if="isEmpty"
        >{{ " " }}{{ emptyText }}{{ " " }}</SecondaryText
      >

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
    </template>
  </div>
</template>

<style scoped lang="sass">
.filters
  max-width: 320px
  margin-bottom: $small
</style>
