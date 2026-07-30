<script setup lang="ts">
/**
 * MyTicketsPage — "Мои обращения" (doc 4.2.3.2.6).
 *
 * The current user's own support/moderation tickets, newest first, with
 * type and status filters applied server-side (GET /v1/moderation/tickets/mine
 * honours status/subtype query params). Each row expands into the ticket
 * thread: the original message and the moderation answer (single answer per
 * ticket in the current backend model).
 *
 * Intake field mapping (see TicketService.CreateIntakeTicket): `comment`
 * holds the subject line, `description` holds the ticket body.
 */
import { computed, onMounted, ref, watch } from "vue";
import {
  ticketApi,
  type Ticket,
  type TicketStatus,
  type TicketSubtype,
} from "@/entities/ticket";
import type { ListEnvelope } from "@/shared/api/models/common";
import { useAuthStore } from "@/shared/stores/auth";
import {
  ExpandableList,
  type ExpandableItem,
  type ExpandableListColumn,
} from "@/shared/ui/ExpandableList";
import { ExpandableListSkeleton } from "@/shared/ui/Skeleton";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { LeadText, SecondaryText } from "@/shared/ui/Layout";
import { LoginPrompt } from "@/features/auth";
import { formatDateFull } from "@/shared/lib/utils/datetime";

const authStore = useAuthStore();

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

// --- Filters (doc: "Тип обращения" + "Статус" dropdowns) ---
const subtypeFilter = ref("");
const statusFilter = ref("");

const subtypeOptions: SelectOption[] = [
  { value: "", label: "Все типы" },
  ...(Object.keys(SUBTYPE_LABELS) as TicketSubtype[]).map((value) => ({
    value,
    label: SUBTYPE_LABELS[value],
  })),
];

// Doc-specified status filter set: all / waiting for moderation /
// waiting for the user / closed (spam tickets still show under "all")
const statusOptions: SelectOption[] = [
  { value: "", label: "Все статусы" },
  { value: "WaitingForModeration", label: STATUS_LABELS.WaitingForModeration },
  { value: "WaitingForUser", label: STATUS_LABELS.WaitingForUser },
  { value: "Closed", label: STATUS_LABELS.Closed },
];

// --- Data ---
const envelope = ref<ListEnvelope<Ticket> | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

async function fetch() {
  loading.value = true;
  const { data, error } = await ticketApi.getMyTickets({
    status: (statusFilter.value || undefined) as TicketStatus | undefined,
    subtype: (subtypeFilter.value || undefined) as TicketSubtype | undefined,
  });
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить обращения";
    return;
  }
  loadError.value = null;
  envelope.value = data ?? null;
}

onMounted(() => {
  if (authStore.isAuthenticated) fetch();
});

// Filters are honoured server-side — refetch whenever either changes
watch([subtypeFilter, statusFilter], () => {
  if (authStore.isAuthenticated) fetch();
});

const hasActiveFilter = computed(
  () => !!subtypeFilter.value || !!statusFilter.value,
);

// Newest first (doc: "от последнего к первому"); ISO strings compare safely
const allTickets = computed(() =>
  [...(envelope.value?.resources ?? [])].sort((a, b) =>
    b.createdUtc.localeCompare(a.createdUtc),
  ),
);

type TicketItem = ExpandableItem & {
  subject: string;
  subtypeLabel: string;
  statusLabel: string;
  createdLabel: string;
  ticket: Ticket;
};

const items = computed<TicketItem[]>(() =>
  allTickets.value.map((t) => ({
    id: t.id,
    subject: t.comment || SUBTYPE_LABELS[t.subtype],
    subtypeLabel: SUBTYPE_LABELS[t.subtype],
    statusLabel: STATUS_LABELS[t.status],
    createdLabel: formatDateFull(t.createdUtc),
    ticket: t,
  })),
);

const columns: ExpandableListColumn<TicketItem>[] = [
  { key: "subject", label: "Тема", bold: true },
  { key: "subtypeLabel", label: "Тип", width: "180px" },
  { key: "statusLabel", label: "Статус", width: "180px" },
  { key: "createdLabel", label: "Дата", width: "130px" },
];

const isEmpty = computed(
  () =>
    envelope.value !== null &&
    allTickets.value.length === 0 &&
    !hasActiveFilter.value,
);
const isFilteredEmpty = computed(
  () =>
    envelope.value !== null &&
    allTickets.value.length === 0 &&
    hasActiveFilter.value,
);
</script>

<template>
  <div class="my-tickets-page">
    <PageTitle>Мои обращения</PageTitle>
    <LeadText>
      Ваши обращения в поддержку и модерацию. Новое обращение можно оставить на
      странице
      <router-link to="/support"><strong>поддержки</strong></router-link>
      или
      <router-link to="/complaint"><strong>жалобы</strong></router-link>
    </LeadText>

    <LoginPrompt
      v-if="!authStore.isAuthenticated"
      action="видеть свои обращения"
    />

    <template v-else>
      <div class="filters">
        <FormField label="Тип обращения" name="tickets-subtype">
          <Select v-model="subtypeFilter" :options="subtypeOptions" />
        </FormField>
        <FormField label="Статус" name="tickets-status">
          <Select v-model="statusFilter" :options="statusOptions" />
        </FormField>
      </div>

      <!-- Error banner is independent of the list: a failed refetch never
           hides already-loaded tickets -->
      <ErrorState
        v-if="loadError"
        class="error-banner"
        :message="loadError"
        :retry="fetch"
      />

      <ExpandableListSkeleton
        v-if="loading && !envelope"
        :count="4"
        with-header
      />

      <SecondaryText v-else-if="isEmpty">
        У вас пока нет обращений
      </SecondaryText>

      <SecondaryText v-else-if="isFilteredEmpty">
        Обращений по заданным фильтрам не найдено
      </SecondaryText>

      <ExpandableList
        v-else-if="items.length"
        :items="items"
        :columns="columns"
        allow-multiple
      >
        <template #cell-statusLabel="{ item }">
          <span :class="STATUS_CLASSES[item.ticket.status]">
            {{ item.statusLabel }}
          </span>
        </template>

        <template #content="{ item }">
          <div class="ticket-details">
            <SecondaryText class="ticket-meta">
              Создано: {{ formatDateFull(item.ticket.createdUtc)
              }}<template v-if="item.ticket.targetUsername">
                | Жалоба на:
                <router-link
                  :to="{
                    name: 'profile',
                    params: { username: item.ticket.targetUsername },
                  }"
                  >{{ item.ticket.targetUsername }}</router-link
                ></template
              >
            </SecondaryText>

            <div class="ticket-text">{{ item.ticket.description }}</div>

            <template v-if="item.ticket.answer">
              <div class="answer-heading">Ответ модерации</div>
              <div class="ticket-text">{{ item.ticket.answer }}</div>
              <SecondaryText class="answer-meta">
                <template v-if="item.ticket.assignedModeratorUsername">
                  <router-link
                    :to="{
                      name: 'profile',
                      params: {
                        username: item.ticket.assignedModeratorUsername,
                      },
                    }"
                    >{{ item.ticket.assignedModeratorUsername }}</router-link
                  >
                  |
                </template>
                {{ formatDateFull(item.ticket.resolvedUtc) }}
              </SecondaryText>
            </template>
            <SecondaryText v-else>Ответа модерации пока нет</SecondaryText>

            <SecondaryText
              v-if="item.ticket.hasWarning || item.ticket.hasBan"
              class="resolution-flags"
            >
              <template v-if="item.ticket.hasWarning"
                >По обращению выдано предупреждение</template
              ><template v-if="item.ticket.hasWarning && item.ticket.hasBan">
                |
              </template>
              <template v-if="item.ticket.hasBan"
                >По обращению выдан бан</template
              >
            </SecondaryText>
          </div>
        </template>
      </ExpandableList>
    </template>
  </div>
</template>

<style scoped lang="sass">
.my-tickets-page
  width: 100%

.filters
  display: flex
  gap: $small
  margin-bottom: $small

  > *
    flex: 0 1 280px

@media (max-width: 600px)
  .filters
    flex-direction: column

    > *
      flex: 1 1 auto

.error-banner
  margin-bottom: $medium

.ticket-details
  display: flex
  flex-direction: column
  gap: $small

// Ticket bodies are stored as raw text (BBCode source) - render verbatim,
// preserving author line breaks
.ticket-text
  white-space: pre-wrap
  overflow-wrap: break-word

.answer-heading
  font-weight: 600
  color: $heading

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
