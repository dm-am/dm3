<script setup lang="ts">
/**
 * One of the reader's own tickets, at /my-tickets/<id>. It is read through
 * the moderation endpoint, which hands an author his own ticket and anybody
 * else a 404, so somebody else's ticket is "not found".
 */
import { computed, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  TICKET_SUBTYPE_LABELS,
  awaitsReader,
  ticketApi,
  useTicketRead,
} from "@/entities/ticket";
import { useAuthStore } from "@/entities/user";
import {
  TicketConversation,
  TicketThread,
  TicketThreadSkeleton,
} from "@/widgets/ticket-thread";
import { ErrorState } from "@/shared/ui/ErrorState";
import { LeadText, SecondaryText } from "@/shared/ui/Layout";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";

const route = useRoute();
const ticketId = computed(() => String(route.params.ticketId ?? ""));
const { user } = storeToRefs(useAuthStore());

// A 404 is a ticket that is not the reader's or does not exist.
const { ticket, loading, loadError, notFound, load } = useTicketRead(() =>
  ticketApi.getTicket(ticketId.value),
);

// The reason first: it is what tells two ticket tabs apart.
useDocumentTitle(() =>
  joinTitleSegments(
    ticket.value ? TICKET_SUBTYPE_LABELS[ticket.value.subtype] : null,
    "Обращение",
  ),
);

async function loadTicket() {
  if (ticketId.value) await load();
}

watch(ticketId, loadTicket, { immediate: true });

/** The server answers with the ticket as it stands after the answer; a refusal leaves it as it was. */
async function send(text: string) {
  const id = ticketId.value;
  const { data, error } = await ticketApi.createComment(id, { text });
  if (error) return { error };
  const answered = data?.resource;
  if (answered && ticket.value?.id === id) ticket.value = answered;
  return { error: null };
}

const awaited = computed(
  () =>
    !!ticket.value &&
    awaitsReader(ticket.value, user.value?.id, ticket.value.canResolve),
);
</script>

<template>
  <div class="my-ticket-page">
    <PageTitle>Обращение</PageTitle>
    <LeadText>
      <router-link :to="{ name: 'my-tickets' }">Мои обращения</router-link>
    </LeadText>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadTicket" />

    <TicketThreadSkeleton v-else-if="loading && !ticket" />

    <SecondaryText v-else-if="notFound">Обращение не найдено</SecondaryText>

    <template v-else-if="ticket">
      <TicketThread
        :ticket="ticket"
        :answers-to="{ name: 'my-ticket', params: { ticketId: ticket.id } }"
        :awaited="awaited"
      />
      <TicketConversation
        :ticket="ticket"
        :send="send"
        :draft-key="composerDraftKey('ticket', 'comment', ticket.id)"
        empty-text="Когда модерация ответит, здесь появится ответ, а вам придет уведомление"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.my-ticket-page
  width: 100%
</style>
