<script setup lang="ts">
/**
 * TicketTrackPage — the page of a guest's ticket, at /support/track/<token>.
 *
 * A guest has no account and gets no letters about his ticket (LEGAL-18), so
 * this page is the only place the answer reaches him: the support form gives
 * him its address on the card it shows after the press. It says so first, in
 * one sentence with the address of the page in it as an ordinary link, to keep
 * and to open on another device, and shows the ticket as the author's page
 * does: the block every screen draws a ticket with (TicketThread), the answers
 * of moderation and his own under it and the form at their foot
 * (TicketConversation). He is signed "Гость", in the caption of the block and
 * on his answers.
 *
 * The address carries the tracking token and nothing else. The token is the
 * only credential of the calls (GET /v1/tickets/track and POST
 * /v1/tickets/track/comments, in a header): the number of the ticket would let
 * anybody open somebody else's ticket with its address and its answer, so a
 * number in its place is simply a token that matches nothing. An unknown token
 * reads "не найдено".
 *
 * His answer goes by the token and is his whoever may be signed in on the
 * device. It hands the turn to moderation, and the page puts the answer the
 * server answered with at the end of the conversation rather than reading the
 * ticket again: every call by the token costs him a place under the limit of
 * five a minute. What he has not sent is kept nowhere but the form: the only
 * thing the page holds that tells one guest ticket from another is the token,
 * and a token is not written into the names of the browser's storage.
 */
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ticketApi, type TrackedTicket } from "@/entities/ticket";
import {
  TicketConversation,
  TicketThread,
  TicketThreadSkeleton,
} from "@/widgets/ticket-thread";
import { LeadText, SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { permalinkOrigin } from "@/shared/config/site";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";

const route = useRoute();
const router = useRouter();

const token = computed(() => String(route.params.token ?? ""));

const ticket = ref<TrackedTicket | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);
const notFound = ref(false);

async function fetch() {
  if (!token.value) {
    notFound.value = true;
    return;
  }
  loading.value = true;
  notFound.value = false;
  const { data, error } = await ticketApi.trackTicket(token.value);
  loading.value = false;
  if (error) {
    // A 404 means the token does not match any ticket; anything else is a
    // transient failure the user can retry.
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

onMounted(fetch);
watch(token, fetch);

/**
 * Writes the guest's answer by the token. It hands the turn to moderation,
 * and the answer the server answered with joins the end of the conversation;
 * a refusal leaves both as they were, and the form gives the text back.
 */
async function send(text: string) {
  const sentBy = token.value;
  const { data, error } = await ticketApi.createTrackedComment(sentBy, text);
  if (error) return { error };
  const comment = data?.resource;
  if (comment && ticket.value && token.value === sentBy) {
    ticket.value = {
      ...ticket.value,
      comments: [...ticket.value.comments, comment],
      status: "WaitingForModeration",
    };
  }
  return { error: null };
}

/**
 * The page's own address, spelled out in full the way the support form builds
 * it: the text of the link the guest keeps and opens on another device.
 */
const address = computed(
  () =>
    permalinkOrigin() +
    router.resolve({ name: "support-track", params: { token: token.value } })
      .href,
);
</script>

<template>
  <div class="ticket-track-page">
    <PageTitle>Обращение</PageTitle>

    <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

    <TicketThreadSkeleton v-else-if="loading && !ticket" />

    <SecondaryText v-else-if="notFound">
      Обращение по этой ссылке не найдено. Проверьте, что ссылка скопирована
      полностью
    </SecondaryText>

    <template v-else-if="ticket">
      <LeadText>
        Ответ появится на этой странице:
        <router-link
          class="track-address"
          :to="{ name: 'support-track', params: { token } }"
          >{{ address }}</router-link
        >. Сохраните ссылку, чтобы вернуться к обращению.
      </LeadText>
      <TicketThread
        :ticket="ticket"
        :answers-to="{ name: 'support-track', params: { token } }"
      />
      <!-- No identifier to keep the draft under but the token, which is not
           written into storage: the builder answers no key without one, and
           the form keeps no draft under no key. -->
      <TicketConversation
        :ticket="ticket"
        :send="send"
        :draft-key="composerDraftKey('ticket', 'comment', null)"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.ticket-track-page
  width: 100%

// The address is one word of eighty letters: it breaks where the column ends.
.track-address
  overflow-wrap: anywhere
</style>
