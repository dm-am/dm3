<script setup lang="ts">
/**
 * The page of a guest's ticket, at /support/track#token=<token>: a guest gets
 * no letters, so the answer reaches him only here. The token is the only
 * credential, in the fragment so it stays out of the edge's access log; a
 * ticket number in its place would open somebody else's ticket. Every call by
 * the token counts against the limit, so a sent answer is appended rather than
 * the ticket read again, and no draft is kept: a token is not written into the
 * names of the browser's storage.
 */
import { computed, onMounted, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import {
  ticketApi,
  trackHash,
  trackToken,
  useTicketRead,
} from "@/entities/ticket";
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

const token = computed(() => trackToken(route.hash));
/** The fragment of the page's own address, the one the guest keeps. */
const hash = computed(() => trackHash(token.value));

// A 404 is a token that matches no ticket; anything else can be retried.
const { ticket, loading, loadError, notFound, load } = useTicketRead(() =>
  ticketApi.trackTicket(token.value),
);

async function loadTicket() {
  if (token.value) await load();
  else notFound.value = true;
}

onMounted(loadTicket);
watch(token, loadTicket);

/** A refusal leaves the conversation as it was, and the form gives the text back. */
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

/** The address in full, as the support form builds it: the link the guest keeps. */
const address = computed(
  () =>
    permalinkOrigin() +
    router.resolve({ name: "support-track", hash: hash.value }).href,
);
</script>

<template>
  <div class="ticket-track-page">
    <PageTitle>Обращение</PageTitle>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadTicket" />

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
          :to="{ name: 'support-track', hash }"
          >{{ address }}</router-link
        >. Сохраните ссылку, чтобы вернуться к обращению.
      </LeadText>
      <TicketThread
        :ticket="ticket"
        :answers-to="{ name: 'support-track', hash }"
      />
      <!-- No key to keep a draft under but the token, which is not written into storage. -->
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
