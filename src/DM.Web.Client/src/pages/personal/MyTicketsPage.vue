<script setup lang="ts">
/**
 * The reader's own tickets, newest first, each in the block every screen draws
 * a ticket with, without the author: every ticket here is his. No filters: a
 * member has a handful, and the state of each is in its caption.
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
const { pageSize } = usePaging();
const { user } = storeToRefs(useAuthStore());

const envelope = ref<ListEnvelope<Ticket> | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

// The page lives in the address, so a reload and a link land on it.
const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

async function loadTickets() {
  loading.value = true;
  const { data, error } = await ticketApi.getMyTickets({
    number: pageNumber.value,
    take: pageSize.value,
  });
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить обращения";
    return;
  }
  loadError.value = null;
  envelope.value = data ?? null;
}

// The route is requiresAuth, so the router turns a guest away.
onMounted(loadTickets);

// The pager only changes the address
watch(pageNumber, loadTickets);

// Newest first, as the server sends them.
const tickets = computed(() => envelope.value?.resources ?? []);

const isEmpty = computed(
  () => envelope.value !== null && tickets.value.length === 0,
);

// Paging draws itself only when there is more than one page.
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

    <!-- A failed refetch never hides the tickets already loaded. -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="loadTickets"
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
