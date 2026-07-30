<script setup lang="ts">
/**
 * TicketTrackPage — public, token-gated status view for a guest ticket.
 *
 * Guests have no account, so after submitting via /support or /complaint they
 * get a tracking link (?token). This page reads that token, calls the public
 * GET /v1/tickets/track/{token} endpoint (no auth) and shows the ticket status
 * and the moderation thread. An unknown/expired token yields a not-found state.
 */
import { computed, onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import {
  ticketApi,
  type TrackedTicket,
  type TicketStatus,
  type TicketSubtype,
} from "@/entities/ticket";
import { LeadText, SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { formatDateFull } from "@/shared/lib/utils/datetime";

const route = useRoute();

// Labels mirror the backend enum Descriptions (TicketSubtype/TicketStatus)
const SUBTYPE_LABELS: Record<TicketSubtype, string> = {
  UserComplaint: "Жалоба на пользователя",
  ModeratorDecisionComplaint: "Жалоба на решение младшего модератора",
  SeniorModeratorDecisionComplaint: "Жалоба на решение старшего модератора",
  SiteImprovementSuggestion: "Предложение по улучшению сайта",
  Bug: "Ошибка",
  AccessRecovery: "Восстановление доступа",
  RegistrationIssue: "Проблемы с регистрацией",
};

const STATUS_LABELS: Record<TicketStatus, string> = {
  WaitingForModeration: "Ожидает ответа модерации",
  WaitingForUser: "Ожидает ответа пользователя",
  Closed: "Закрыто",
  Spam: "Спам",
};

// Status color contract from doc 4.2.2.23: green / normal / gray / red
const STATUS_CLASSES: Record<TicketStatus, string> = {
  WaitingForModeration: "status--waiting-moderation",
  WaitingForUser: "status--waiting-user",
  Closed: "status--closed",
  Spam: "status--spam",
};

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

const subject = computed(
  () =>
    ticket.value &&
    (ticket.value.subject || SUBTYPE_LABELS[ticket.value.subtype]),
);
</script>

<template>
  <div class="ticket-track-page">
    <PageTitle>Статус обращения</PageTitle>
    <LeadText>
      Статус вашего обращения по ссылке отслеживания. Новое обращение можно
      оставить на странице
      <router-link to="/support"><strong>поддержки</strong></router-link>
      или
      <router-link to="/complaint"><strong>жалобы</strong></router-link>
    </LeadText>

    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetch"
    />

    <SecondaryText v-else-if="loading && !ticket">
      Загрузка обращения...
    </SecondaryText>

    <SecondaryText v-else-if="notFound">
      Обращение по этой ссылке не найдено. Проверьте, что ссылка скопирована
      полностью
    </SecondaryText>

    <div v-else-if="ticket" class="ticket">
      <div class="ticket-header">
        <h2 class="ticket-subject">{{ subject }}</h2>
        <span :class="STATUS_CLASSES[ticket.status]">
          {{ STATUS_LABELS[ticket.status] }}
        </span>
      </div>

      <SecondaryText class="ticket-meta">
        {{ SUBTYPE_LABELS[ticket.subtype] }} | Создано:
        {{ formatDateFull(ticket.createdUtc) }}
      </SecondaryText>

      <div class="ticket-text">{{ ticket.description }}</div>

      <template v-if="ticket.answer">
        <div class="answer-heading">Ответ модерации</div>
        <div class="ticket-text">{{ ticket.answer }}</div>
        <SecondaryText v-if="ticket.resolvedUtc" class="answer-meta">
          {{ formatDateFull(ticket.resolvedUtc) }}
        </SecondaryText>
      </template>
      <SecondaryText v-else>Ответа модерации пока нет</SecondaryText>

      <template v-if="ticket.responses.length">
        <div class="answer-heading">Переписка</div>
        <div
          v-for="(response, index) in ticket.responses"
          :key="index"
          class="thread-item"
          :class="{ 'thread-item--moderator': response.isFromModerator }"
        >
          <SecondaryText class="thread-meta">
            {{ response.isFromModerator ? "Модерация" : "Вы" }} |
            {{ formatDateFull(response.createdUtc) }}
          </SecondaryText>
          <div class="ticket-text">{{ response.text }}</div>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped lang="sass">
.ticket-track-page
  width: 100%

.error-banner
  margin-bottom: $medium

.ticket
  display: flex
  flex-direction: column
  gap: $small

.ticket-header
  display: flex
  align-items: baseline
  justify-content: space-between
  gap: $small

.ticket-subject
  margin: 0
  font-size: $font-size
  font-weight: 700
  color: $heading

// Ticket bodies are stored as raw text (BBCode source) — render verbatim,
// preserving author line breaks
.ticket-text
  white-space: pre-wrap
  overflow-wrap: break-word

.answer-heading
  margin-top: $small
  font-weight: 600
  color: $heading

.thread-item
  padding-left: $small
  border-left: 2px solid $border

.thread-item--moderator
  border-left-color: $border-accent-green

// Status colors per doc 4.2.2.23: green / normal / gray / red
.status--waiting-moderation
  color: $accent-green

.status--waiting-user
  color: $text

.status--closed
  color: $text-muted

.status--spam
  color: $accent-red
</style>
