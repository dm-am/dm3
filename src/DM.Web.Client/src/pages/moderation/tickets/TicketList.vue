<script setup lang="ts">
/**
 * The queue "Обращения": every ticket the reader moderates, in the block every
 * screen draws a ticket with, the server's order. The server scopes what each
 * role is shown (TicketVisibility); the filter of reasons offers the same
 * reasons, since one never shown would narrow the queue to nothing. The
 * filters live in the address, so a narrowed queue is a link.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
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
import { parseStringFromUrl, toQueryValue } from "@/shared/lib/filters";
import { TicketThread, TicketThreadSkeleton } from "@/widgets/ticket-thread";

const route = useRoute();
const router = useRouter();
const { user } = storeToRefs(useAuthStore());

/** The reasons each role is shown, by the server's rule (TicketVisibility), in TICKET_SUBTYPE_LABELS order. */
const MODERATOR_SUBTYPES: TicketSubtype[] = ["UserComplaint"];
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

// The filters come from the address; the guard drops the answer the reader has left.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить обращения" });

/** What the two lists choose and the address holds; "" is no filter. */
type QueueFilter = { subtype: "" | TicketSubtype; status: "" | TicketStatus };

/** The four statuses, in the order the server stands the queue in. */
const STATUSES: TicketStatus[] = [
  "WaitingForModeration",
  "WaitingForUser",
  "Closed",
  "Spam",
];

/** The filters the address holds; a value the queue does not know is no filter at all. */
const applied = computed<QueueFilter>(() => {
  const subtype = parseStringFromUrl(
    toQueryValue(route.query.subtype),
  ) as TicketSubtype;
  const status = parseStringFromUrl(
    toQueryValue(route.query.status),
  ) as TicketStatus;
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

function loadTickets() {
  const { subtype, status } = applied.value;
  return run(
    () =>
      ticketApi.getTickets({
        ...(status ? { status } : {}),
        subtype: subtype || undefined,
      }),
    (data) => {
      tickets.value = data?.resources ?? [];
    },
  );
}

watch([() => applied.value.subtype, () => applied.value.status], loadTickets, {
  immediate: true,
});

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

const isEmpty = computed(() => !loading.value && tickets.value.length === 0);

// A queue narrowed to nothing is not an empty queue.
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

    <ErrorState v-if="loadError" :message="loadError" :retry="loadTickets" />

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
