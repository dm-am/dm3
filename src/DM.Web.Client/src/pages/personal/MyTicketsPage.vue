<script setup lang="ts">
/**
 * MyTicketsPage — "Мои обращения" (doc 4.2.3.2.6).
 *
 * The current user's own tickets, newest first, each the block every screen
 * draws a ticket with (TicketThread): the reason over the text, and under it
 * the caption "дата | Ответы (N) | состояние", the state a grey word with a
 * red "*" after it where the ticket waits for the reader's answer. Not the
 * author: every ticket here is the reader's. Nothing stands under a block:
 * moderation answers in the conversation of the ticket, on its own page,
 * where the reason and the number of answers lead. A long text folds.
 *
 * No filters: a member has a handful of tickets, two dropdowns over three rows
 * only pushed them down, and the state of each is in its caption. The page is
 * taken server-side (GET /v1/moderation/tickets/mine): the pile of a
 * long-standing member only grows.
 */
import { computed, onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { awaitsReader, ticketApi, type Ticket } from "@/entities/ticket";
import { useAuthStore } from "@/entities/user";
import { TicketThread, TicketThreadSkeleton } from "@/widgets/ticket-thread";
import type { ListEnvelope } from "@/shared/api/models/common";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Paging } from "@/shared/ui/Paging";
import { LeadText, SecondaryText } from "@/shared/ui/Layout";
import { parsePageNumber } from "@/shared/lib/filters";
import { usePaging } from "@/shared/lib/composables/usePaging";

const route = useRoute();
const { entitiesPerPage } = usePaging();
const { user } = storeToRefs(useAuthStore());

const envelope = ref<ListEnvelope<Ticket> | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

// The page lives in the address bar, so a reload and a shared link land on the
// page that was being read.
const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

async function fetch() {
  loading.value = true;
  const { data, error } = await ticketApi.getMyTickets({
    number: pageNumber.value,
    take: entitiesPerPage.value,
  });
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить обращения";
    return;
  }
  loadError.value = null;
  envelope.value = data ?? null;
}

// No auth guard on the fetches: the route is requiresAuth, so the router is
// the one mechanism that turns a guest away (guardAuthenticated).
onMounted(fetch);

// Following the pager only changes the address, so the refetch hangs off it
watch(pageNumber, fetch);

// Newest first (doc: "от последнего к первому"); ISO strings compare safely
const tickets = computed(() =>
  [...(envelope.value?.resources ?? [])].sort((a, b) =>
    b.createdUtc.localeCompare(a.createdUtc),
  ),
);

const isEmpty = computed(
  () => envelope.value !== null && tickets.value.length === 0,
);

// Paging draws itself only when there is more than one page, so a reader with a
// handful of tickets sees the screen unchanged.
const paging = computed(() => envelope.value?.paging ?? null);
</script>

<template>
  <div class="my-tickets-page">
    <PageTitle>Мои обращения</PageTitle>
    <LeadText>
      Новое обращение можно оставить на странице
      <router-link to="/support">поддержки</router-link>
      или
      <router-link to="/complaint">жалобы</router-link>
    </LeadText>

    <!-- The error is independent of the list: a failed refetch never hides
         the tickets already loaded. -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetch"
    />

    <TicketThreadSkeleton v-if="loading && !envelope" :count="3" />

    <SecondaryText v-else-if="isEmpty">Обращений пока нет</SecondaryText>

    <div v-else-if="tickets.length" class="tickets">
      <TicketThread
        v-for="ticket in tickets"
        :key="ticket.id"
        :ticket="ticket"
        :with-author="false"
        :to="{ name: 'my-ticket', params: { ticketId: ticket.id } }"
        :awaited="awaitsReader(ticket, user?.id, false)"
      />
    </div>

    <Paging
      v-if="paging"
      class="pager"
      :paging="paging"
      :to="{ name: 'my-tickets' }"
      use-query
    />
  </div>
</template>

<style scoped lang="sass">
.my-tickets-page
  width: 100%

.error-banner
  margin-bottom: $medium

// The column of tickets: a medium step between two, as between two topics.
.tickets
  display: flex
  flex-direction: column
  gap: $medium

.pager
  margin-top: $medium
</style>
