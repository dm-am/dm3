<script setup lang="ts">
/**
 * One ticket for moderators: the block, the answers and the form. Nobody takes
 * a ticket in hand: any moderator its reason is shown to answers and closes it.
 * The status choice is offered where the server says the reader answers as
 * moderation (canResolve). A ticket the reader may not read is "not found".
 */
import { computed, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  TICKET_SUBTYPE_LABELS,
  awaitsReader,
  ticketApi,
  useTicketRead,
  type AnswerStatus,
} from "@/entities/ticket";
import { useAuthStore } from "@/entities/user";
import {
  TicketConversation,
  TicketThread,
  TicketThreadSkeleton,
} from "@/widgets/ticket-thread";
import { ErrorState } from "@/shared/ui/ErrorState";
import { LeadText, SecondaryText } from "@/shared/ui/Layout";
import { MODERATION_SECTIONS } from "@/shared/config/moderationSections";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";

const route = useRoute();
const { user } = storeToRefs(useAuthStore());

const ticketId = computed(() => String(route.params.ticketId ?? ""));

// A 404 is a ticket the reader may not read or one that does not exist.
const { ticket, loading, loadError, notFound, load } = useTicketRead(() =>
  ticketApi.getTicket(ticketId.value),
);

// The reason first: the URL holds a guid and the forms ask for no subject, so
// the reason is what tells two ticket tabs apart.
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

/** The queue every ticket belongs to, by the section table's own words. */
const QUEUE = "moderation-tickets";
const queueLabel =
  MODERATION_SECTIONS.find((section) => section.name === QUEUE)?.label ?? "";

/** The server answers with the ticket as it stands after the answer; a refusal leaves it as it was. */
async function send(text: string, status?: AnswerStatus) {
  const id = ticketId.value;
  const { data, error } = await ticketApi.createComment(id, {
    text,
    ...(status ? { status } : {}),
  });
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
  <div class="moderation-ticket">
    <page-title>Обращение</page-title>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadTicket" />

    <TicketThreadSkeleton v-else-if="loading && !ticket" />

    <SecondaryText v-else-if="notFound || !ticket"
      >Обращение не найдено</SecondaryText
    >

    <template v-else>
      <LeadText>
        <router-link :to="{ name: QUEUE }">{{ queueLabel }}</router-link>
      </LeadText>

      <TicketThread
        :ticket="ticket"
        :answers-to="{
          name: 'moderation-ticket',
          params: { ticketId: ticket.id },
        }"
        :awaited="awaited"
      />

      <TicketConversation
        :ticket="ticket"
        :send="send"
        :draft-key="composerDraftKey('ticket', 'comment', ticket.id)"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.moderation-ticket
  width: 100%
</style>
