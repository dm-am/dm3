<script setup lang="ts">
/**
 * TicketList — shared ticket worklist for the "Поддержка" and "Жалобы"
 * pages (doc 4.2.3.8.7 / 4.2.3.8.8). Renders TicketCard previews for the
 * given subtype group with type and status filters; tickets waiting for
 * a moderation response float to the top, then newest first.
 *
 * The backend already scopes visibility by role; the subtype group here
 * only splits the visible tickets between the two pages.
 */
import { computed, onMounted, ref } from "vue";
import ModerationApi, {
  type Ticket,
  type TicketStatus,
} from "@/shared/api/moderationApi";
import type { TicketSubtype } from "@/shared/api/supportApi";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Select, type SelectOption } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import TicketCard from "./TicketCard.vue";
import {
  TICKET_STATUS_LABELS,
  TICKET_STATUS_ORDER,
  TICKET_SUBTYPE_LABELS,
} from "../lib/labels";

const props = defineProps<{
  /** Subtype group shown on this page (support vs complaints). */
  subtypes: TicketSubtype[];
  /** Empty-state text, e.g. "Обращений пока нет". */
  emptyText: string;
}>();

const tickets = ref<Ticket[]>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);

// "" = no filter
const subtypeFilter = ref<"" | TicketSubtype>("");
const statusFilter = ref<"" | TicketStatus>("");

const subtypeOptions = computed<SelectOption[]>(() => [
  { value: "", label: "Все типы" },
  ...props.subtypes.map((s) => ({
    value: s,
    label: TICKET_SUBTYPE_LABELS[s],
  })),
]);

const statusOptions: SelectOption[] = [
  { value: "", label: "Все статусы" },
  ...(Object.keys(TICKET_STATUS_LABELS) as TicketStatus[]).map((s) => ({
    value: s,
    label: TICKET_STATUS_LABELS[s],
  })),
];

async function fetch() {
  loading.value = true;
  const { data, error } = await ModerationApi.getTickets({
    status: statusFilter.value || undefined,
    subtype: subtypeFilter.value || undefined,
  });
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить обращения";
    return;
  }
  loadError.value = null;
  tickets.value = (data?.resources ?? [])
    .filter((t) => props.subtypes.includes(t.subtype))
    .sort((a, b) => {
      // Waiting-for-moderation first, then newest first (doc: "в порядке
      // статуса и времени")
      const byStatus =
        TICKET_STATUS_ORDER[a.status] - TICKET_STATUS_ORDER[b.status];
      if (byStatus !== 0) return byStatus;
      return (
        new Date(b.createdUtc).getTime() - new Date(a.createdUtc).getTime()
      );
    });
}

function onSubtypeChange(value: string) {
  subtypeFilter.value = value as "" | TicketSubtype;
  fetch();
}

function onStatusChange(value: string) {
  statusFilter.value = value as "" | TicketStatus;
  fetch();
}

onMounted(fetch);

const isEmpty = computed(() => !loading.value && tickets.value.length === 0);
</script>

<template>
  <div class="ticket-list">
    <div class="filters">
      <FormField label="Тип обращения" name="ticket-subtype">
        <Select
          :model-value="subtypeFilter"
          :options="subtypeOptions"
          @update:model-value="onSubtypeChange"
        />
      </FormField>
      <FormField label="Статус" name="ticket-status">
        <Select
          :model-value="statusFilter"
          :options="statusOptions"
          @update:model-value="onStatusChange"
        />
      </FormField>
    </div>

    <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

    <!-- Skeleton: geometric twin of a TicketCard preview
         (header + meta + subject + clamped body) -->
    <div
      v-else-if="loading && !tickets.length"
      class="cards"
      aria-hidden="true"
    >
      <div v-for="i in 4" :key="i" class="skeleton-card">
        <div class="skeleton-line skeleton-title"></div>
        <div class="skeleton-line skeleton-meta"></div>
        <div class="skeleton-line skeleton-body"></div>
        <div class="skeleton-line skeleton-body-short"></div>
      </div>
    </div>

    <SecondaryText v-else-if="isEmpty">{{ emptyText }}</SecondaryText>

    <div v-else class="cards">
      <TicketCard
        v-for="ticket in tickets"
        :key="ticket.id"
        :ticket="ticket"
        preview
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

.filters
  display: flex
  gap: $medium
  margin-bottom: $small

  > *
    flex: 0 1 320px

.cards
  display: flex
  flex-direction: column
  gap: $medium

// --- Skeleton (mirrors TicketCard: $medium padding, four text lines) ---
.skeleton-card
  border: 1px solid $border
  border-radius: $border-radius
  padding: $medium
  background: $bg-element

.skeleton-line
  height: 1em
  margin-bottom: $small
  +skeleton-shimmer

  &:last-child
    margin-bottom: 0

.skeleton-title
  width: 40%

.skeleton-meta
  width: 65%

.skeleton-body
  width: 95%

.skeleton-body-short
  width: 70%
</style>
