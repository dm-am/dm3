<script setup lang="ts">
/**
 * ModerationTicketPage — single ticket view for moderators (doc 4.2.3.2.6
 * composition: ticket block 4.2.2.23 + reply block 4.2.2.24).
 *
 * A ticket works as a topic with answers and looks like one: under a link
 * back to the queue "Обращения", the block of the ticket (TicketThread) with
 * its author in the caption, the answers of the author and of moderation under
 * it and the form at their foot (TicketConversation). Nobody takes a ticket in
 * hand: any moderator to whom its reason is shown answers and closes it.
 *
 * The moderator chooses in the form, over the field of the answer, the status
 * the answer moves the ticket to: "Ожидает ответа пользователя" unless he
 * closes it or marks it as spam, and closing needs no word. The text and the
 * status go in one request (TicketAnswerForm), and the server answers with the
 * ticket as it stands after them, which the page shows: a closed ticket loses
 * its form and ends with the line of who closed it. The choice is there only
 * where the server says the reader answers as moderation (canResolve): never
 * on a closed ticket, never for its author, whose answer hands the ticket to
 * moderation whatever his role.
 *
 * The ticket punishes nobody: a warning and a ban are given from the profile
 * of the violator, where the rest of his record is.
 *
 * A ticket the moderator may not read, its reason not shown to his role or a
 * complaint about him, is "not found", the same as one that does not exist.
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  TICKET_SUBTYPE_LABELS,
  awaitsReader,
  ticketApi,
  type AnswerStatus,
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
import { MODERATION_SECTIONS } from "@/shared/config/moderationSections";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import { useRoleGate } from "./lib/useRoleGate";

const route = useRoute();
const { hasAccess, deniedText } = useRoleGate("Moderator");
const { user } = storeToRefs(useAuthStore());

const ticketId = computed(() => String(route.params.ticketId ?? ""));

const ticket = ref<TicketDetails | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);
const notFound = ref(false);

// The reason first: the model carries no ticket number (the URL holds a guid),
// and the forms ask for no subject, so the reason is what tells two ticket tabs
// apart.
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
    // A 404 is a ticket the reader may not read or one that does not exist;
    // anything else is a failure he can retry.
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

/** The queue every ticket belongs to, by the section table's own words. */
const QUEUE = "moderation-tickets";
const queueLabel =
  MODERATION_SECTIONS.find((section) => section.name === QUEUE)?.label ?? "";

/**
 * Writes the reader's answer, and for moderation the status it moves the
 * ticket to. The server answers with the ticket as it stands after them, and
 * the page shows that; a refusal leaves the ticket as it was, and the form
 * gives the text back.
 */
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

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <ErrorState v-else-if="loadError" :message="loadError" :retry="fetch" />

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
