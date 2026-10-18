<script setup lang="ts">
/**
 * TicketList — the queue "Обращения" (doc 4.2.3.8.7 / 4.2.3.8.8): every
 * ticket the reader moderates, in one queue whatever its reason. Renders them
 * in the block every screen draws a ticket with (TicketThread): the reason
 * leading to the ticket's page, and the caption of a topic, "Автор: имя,
 * дата | Ответы (N) | состояние", with a red "*" after the state of a ticket
 * that waits for the reader's answer. The tickets waiting for moderation
 * float to the top, then those waiting for their author, then the ended ones,
 * each group newest first.
 *
 * The backend scopes visibility by role (TicketVisibility) and leaves out a
 * complaint about the reader himself, so every ticket here is his to answer
 * but the ones he filed himself. The filter of reasons offers the reasons his
 * role is shown, by the same rule, and no other: a reason he is never shown
 * would narrow the queue to nothing.
 *
 * Two lists over the queue narrow it, "Тип обращения" and "Статус", and a
 * choice in either applies at once. The statuses are the four a ticket is
 * stored with and every screen names it by (TICKET_STATUS_LABELS): whose turn
 * it is, or how it ended. Nobody takes a ticket in hand, so a status is asked
 * of the server as it is. The filters live in the
 * address, so a narrowed queue is a link a moderator can hand to another one.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter, type LocationQuery } from "vue-router";
import { storeToRefs } from "pinia";
import {
  awaitsReader,
  ticketApi,
  TICKET_STATUS_LABELS,
  TICKET_SUBTYPE_LABELS,
  type Ticket,
  type TicketStatus,
  type TicketSubtype,
} from "@/entities/ticket";
import {
  useAuthStore,
  userIsAdmin,
  userIsSeniorModerator,
} from "@/entities/user";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Select, type SelectOption } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { TicketThread, TicketThreadSkeleton } from "@/widgets/ticket-thread";
import { TICKET_STATUS_ORDER } from "../lib/labels";

const route = useRoute();
const router = useRouter();
const { user } = storeToRefs(useAuthStore());

/**
 * The reasons the reader's role is shown, as the server decides it
 * (TicketVisibility): a moderator the complaints about users and the
 * suggestions, a senior moderator the complaints about a junior moderator's
 * decision as well, an administrator every reason, the support desk among
 * them. In the order the reasons are named in (TICKET_SUBTYPE_LABELS).
 */
const MODERATOR_SUBTYPES: TicketSubtype[] = [
  "UserComplaint",
  "SiteImprovementSuggestion",
];
const SENIOR_SUBTYPES: TicketSubtype[] = [
  ...MODERATOR_SUBTYPES,
  "ModeratorDecisionComplaint",
];

const subtypes = computed<TicketSubtype[]>(() => {
  const all = Object.keys(TICKET_SUBTYPE_LABELS) as TicketSubtype[];
  if (userIsAdmin(user.value)) return all;
  const shown = userIsSeniorModerator(user.value)
    ? SENIOR_SUBTYPES
    : MODERATOR_SUBTYPES;
  return all.filter((subtype) => shown.includes(subtype));
});

const tickets = ref<Ticket[]>([]);

// The filters come from the address and change under the reader (the back
// button, a link), so two answers can be on the wire at once; the guard
// drops the one the reader has already left.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить обращения" });

/** What the two lists choose and the address holds; "" is no filter. */
type QueueFilter = { subtype: "" | TicketSubtype; status: "" | TicketStatus };

/** The four statuses, in the order the queue stands in (TICKET_STATUS_ORDER). */
const STATUSES = (Object.keys(TICKET_STATUS_ORDER) as TicketStatus[]).sort(
  (a, b) => TICKET_STATUS_ORDER[a] - TICKET_STATUS_ORDER[b],
);

function single(value: LocationQuery[string]): string {
  const one = Array.isArray(value) ? value[0] : value;
  return one ? String(one) : "";
}

/**
 * The filters the address holds. A value the queue does not know (a reason the
 * role of the reader is not shown, a status that is no more) is no filter at
 * all, rather than a request the server answers with an empty queue.
 */
const applied = computed<QueueFilter>(() => {
  const subtype = single(route.query.subtype) as TicketSubtype;
  const status = single(route.query.status) as TicketStatus;
  return {
    subtype: subtypes.value.includes(subtype) ? subtype : "",
    status: STATUSES.includes(status) ? status : "",
  };
});

function applyFilter(filter: QueueFilter) {
  const query = { ...route.query };
  if (filter.subtype) query.subtype = filter.subtype;
  else delete query.subtype;
  if (filter.status) query.status = filter.status;
  else delete query.status;
  router.push({ query });
}

function fetch() {
  const { subtype, status } = applied.value;
  return run(
    () =>
      ticketApi.getTickets({
        ...(status ? { status } : {}),
        subtype: subtype || undefined,
      }),
    (data) => {
      tickets.value = [...(data?.resources ?? [])].sort((a, b) => {
        // By status, waiting for moderation first, then newest first (doc:
        // "в порядке статуса и времени")
        const byStatus =
          TICKET_STATUS_ORDER[a.status] - TICKET_STATUS_ORDER[b.status];
        if (byStatus !== 0) return byStatus;
        return (
          new Date(b.createdUtc).getTime() - new Date(a.createdUtc).getTime()
        );
      });
    },
  );
}

watch([() => applied.value.subtype, () => applied.value.status], fetch, {
  immediate: true,
});

// =============================================================================
// THE TWO LISTS
// =============================================================================

const subtypeOptions = computed<SelectOption[]>(() => [
  { value: "", label: "Все типы" },
  ...subtypes.value.map((s) => ({ value: s, label: TICKET_SUBTYPE_LABELS[s] })),
]);

const statusOptions: SelectOption[] = [
  { value: "", label: "Все статусы" },
  ...STATUSES.map((s) => ({ value: s, label: TICKET_STATUS_LABELS[s] })),
];

function setSubtype(value: string) {
  applyFilter({ ...applied.value, subtype: value as QueueFilter["subtype"] });
}

function setStatus(value: string) {
  applyFilter({ ...applied.value, status: value as QueueFilter["status"] });
}

const hasFilters = computed(
  () => applied.value.subtype !== "" || applied.value.status !== "",
);

// =============================================================================
// THE QUEUE
// =============================================================================

const isEmpty = computed(() => !loading.value && tickets.value.length === 0);

// A queue narrowed to nothing is not an empty queue (Empty State Standards).
const emptyLine = computed(() =>
  hasFilters.value
    ? "Обращений по заданным фильтрам не найдено"
    : "Обращений пока нет",
);
</script>

<template>
  <div class="ticket-list">
    <div class="filters">
      <FormField label="Тип обращения" name="ticket-subtype">
        <Select
          id="ticket-subtype"
          :model-value="applied.subtype"
          :options="subtypeOptions"
          @update:model-value="setSubtype"
        />
      </FormField>
      <FormField label="Статус" name="ticket-status">
        <Select
          id="ticket-status"
          :model-value="applied.status"
          :options="statusOptions"
          @update:model-value="setStatus"
        />
      </FormField>
    </div>

    <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

    <TicketThreadSkeleton v-else-if="loading && !tickets.length" :count="3" />

    <SecondaryText v-else-if="isEmpty">{{ emptyLine }}</SecondaryText>

    <div v-else class="cards">
      <TicketThread
        v-for="ticket in tickets"
        :key="ticket.id"
        :ticket="ticket"
        :to="{ name: 'moderation-ticket', params: { ticketId: ticket.id } }"
        :awaited="awaitsReader(ticket, user?.id, true)"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
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
</style>
