<script setup lang="ts">
/**
 * MyTicketPage — one of the reader's own tickets, at /my-tickets/<id>.
 *
 * Where the reader's list "Мои обращения", the card after the support form and
 * the notifications of the ticket lead him. A ticket works as a topic with
 * answers and looks like one: the block of the ticket (TicketThread) with the
 * caption of a topic, the answers of the author and of moderation under it and
 * the form at their foot (TicketConversation). Until somebody answers, the
 * place of the answer says what will come, so the reader knows he need not
 * watch the page. A ticket that waits for his answer has the red "*" after
 * its state.
 *
 * His answer is the author's: it hands the turn to moderation. The server
 * answers with the ticket as it stands after it, and the page shows that, as
 * the next reading of the page would; the form keeps what he has not sent
 * under this ticket.
 *
 * The ticket is read through the moderation endpoint, which hands an author
 * his own ticket and anybody else a 404; a ticket of somebody else is "not
 * found", the same as one that does not exist.
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  TICKET_SUBTYPE_LABELS,
  awaitsReader,
  ticketApi,
  type TicketDetails,
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

const ticket = ref<TicketDetails | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);
const notFound = ref(false);

// The reason first: it is all that tells two tabs of tickets apart.
useDocumentTitle(() =>
  joinTitleSegments(
    ticket.value ? TICKET_SUBTYPE_LABELS[ticket.value.subtype] : null,
    "Обращение",
  ),
);

async function fetch() {
  if (!ticketId.value) return;
  loading.value = true;
  notFound.value = false;
  const { data, error } = await ticketApi.getTicket(ticketId.value);
  loading.value = false;
  if (error) {
    // A 404 is a ticket that is not the reader's or does not exist; anything
    // else is a failure the reader can retry.
    if ((error as { status?: number }).status === 404) {
      notFound.value = true;
      ticket.value = null;
      loadError.value = null;
      return;
    }
    loadError.value = "Не удалось загрузить обращение";
    return;
  }
  loadError.value = null;
  ticket.value = data?.resource ?? null;
  notFound.value = ticket.value === null;
}

watch(ticketId, fetch, { immediate: true });

/**
 * Writes the reader's answer. The server answers with the ticket as it stands
 * after it, the answer at the end of the conversation and the turn handed to
 * moderation; a refusal leaves the ticket as it was, and the form gives the
 * text back.
 */
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

    <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

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
