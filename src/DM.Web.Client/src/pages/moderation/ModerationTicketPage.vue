<script setup lang="ts">
/**
 * ModerationTicketPage — single ticket view for moderators (doc
 * 4.2.3.2.6 composition: ticket block 4.2.2.23 + reply block 4.2.2.24).
 * Actions: assign to me, resolve with an answer + resolution status
 * ("Закрыто" / "Спам" per backend ResolveTicketValidator) and an optional
 * warning (0-6 points) or ban (SeniorModerator+, time-boxed).
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import {
  ticketApi,
  type ResolveTicketRequest,
  type Ticket,
  type TicketStatus,
} from "@/entities/ticket";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { Button } from "@/shared/ui/Button";
import { ErrorState } from "@/shared/ui/ErrorState";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import TicketCard from "./tickets/TicketCard.vue";
import { useRoleGate } from "./lib/useRoleGate";
import { notifyFailure } from "@/shared/lib/errors";

const route = useRoute();
const toast = useToast();
const { hasAccess, isSeniorModerator } = useRoleGate("Moderator");

const ticketId = computed(() => String(route.params.ticketId ?? ""));

const ticket = ref<Ticket | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

// The subject first: the model carries no ticket number (the URL holds a
// guid), so the subject line is all that tells two ticket tabs apart.
useDocumentTitle(() => joinTitleSegments(ticket.value?.comment, "Обращение"));

async function fetch() {
  if (!ticketId.value) return;
  loading.value = true;
  const { data, error } = await ticketApi.getTicket(ticketId.value);
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить обращение";
    return;
  }
  loadError.value = null;
  ticket.value = data?.resource ?? null;
}

watch(ticketId, fetch, { immediate: true });

const isOpen = computed(
  () =>
    !!ticket.value &&
    ticket.value.status !== "Closed" &&
    ticket.value.status !== "Spam",
);

// --- Assign to me ---
const assigning = ref(false);

async function assignToMe() {
  if (!ticket.value || assigning.value) return;
  assigning.value = true;
  const { data, error } = await ticketApi.assignTicketToMe(ticket.value.id);
  assigning.value = false;
  if (error) {
    notifyFailure(error, "Не удалось взять обращение в работу");
    return;
  }
  ticket.value = data?.resource ?? ticket.value;
  toast.success("Обращение взято в работу");
}

// --- Resolve form ---
const answer = ref("");
const resolutionStatus = ref<TicketStatus>("Closed");

const statusOptions: SelectOption[] = [
  { value: "Closed", label: "Закрыто" },
  { value: "Spam", label: "Спам" },
];

// Optional warning (only when the ticket has a target user)
const issueWarning = ref(false);
const warningKind = ref<"verbal" | "scored">("scored");
const warningPoints = ref("1");
const warningText = ref("");

const warningKindOptions: SelectOption[] = [
  { value: "verbal", label: "Устное предупреждение (0 баллов)" },
  { value: "scored", label: "Предупреждение (1-6 баллов)" },
];

const warningPointsOptions: SelectOption[] = ["1", "2", "3", "4", "5", "6"].map(
  (v) => ({ value: v, label: v }),
);

// Optional ban (SeniorModerator+; ticket resolution bans are time-boxed)
const issueBan = ref(false);
const banDuration = ref(String(24));
const banComment = ref("");

const banDurationOptions: SelectOption[] = [
  { value: String(24), label: "1 день" },
  { value: String(2 * 24), label: "2 дня" },
  { value: String(3 * 24), label: "3 дня" },
  { value: String(7 * 24), label: "7 дней" },
  { value: String(14 * 24), label: "2 недели" },
  { value: String(28 * 24), label: "4 недели" },
  { value: String(90 * 24), label: "3 месяца" },
  { value: String(180 * 24), label: "6 месяцев" },
  { value: String(365 * 24), label: "12 месяцев" },
];

const canSanction = computed(() => !!ticket.value?.targetUsername);

const canSubmit = computed(() => {
  if (!answer.value.trim() || answer.value.length > 2000) return false;
  if (issueWarning.value) {
    if (!warningText.value.trim() || warningText.value.length > 2000)
      return false;
  }
  if (issueBan.value) {
    if (!banComment.value.trim() || banComment.value.length > 2000)
      return false;
  }
  return true;
});

const resolving = ref(false);

async function resolve() {
  if (!ticket.value || !canSubmit.value || resolving.value) return;
  resolving.value = true;

  const request: ResolveTicketRequest = {
    status: resolutionStatus.value,
    answer: answer.value.trim(),
  };
  if (issueWarning.value && canSanction.value) {
    request.issueWarning = true;
    request.warningPoints =
      warningKind.value === "verbal" ? 0 : parseInt(warningPoints.value, 10);
    request.warningText = warningText.value.trim();
  }
  if (issueBan.value && canSanction.value && isSeniorModerator.value) {
    request.issueBan = true;
    request.banDurationHours = parseInt(banDuration.value, 10);
    request.banComment = banComment.value.trim();
  }

  const { data, error } = await ticketApi.resolveTicket(
    ticket.value.id,
    request,
  );
  resolving.value = false;
  if (error) {
    notifyFailure(error, "Не удалось разрешить обращение");
    return;
  }
  ticket.value = data?.resource ?? ticket.value;
  answer.value = "";
  issueWarning.value = false;
  issueBan.value = false;
  toast.success("Обращение разрешено");
}
</script>

<template>
  <div class="moderation-ticket">
    <page-title>Обращение</page-title>

    <SecondaryText v-if="!hasAccess">
      Страница доступна только модераторам
    </SecondaryText>

    <ErrorState v-else-if="loadError" :message="loadError" :retry="fetch" />

    <!-- Skeleton: geometric twin of the full TicketCard -->
    <div
      v-else-if="loading && !ticket"
      class="skeleton-card"
      aria-hidden="true"
    >
      <div class="skeleton-line skeleton-title"></div>
      <div class="skeleton-line skeleton-meta"></div>
      <div class="skeleton-line skeleton-body"></div>
      <div class="skeleton-line skeleton-body"></div>
      <div class="skeleton-line skeleton-body-short"></div>
    </div>

    <SecondaryText v-else-if="!ticket">Обращение не найдено</SecondaryText>

    <template v-else>
      <TicketCard :ticket="ticket" />

      <!-- Moderator's answer (reply block per doc 4.2.2.24) -->
      <div v-if="ticket.answer" class="ticket-answer">
        <div class="answer-meta">
          <template v-if="ticket.assignedModeratorUsername">
            <router-link
              :to="{
                name: 'profile',
                params: { username: ticket.assignedModeratorUsername },
              }"
            >
              {{ ticket.assignedModeratorUsername }} </router-link
            ><span class="answer-role" title="Модератор">[M]</span>
          </template>
          <span v-else>Модерация</span>
          <span v-if="ticket.resolvedUtc" class="answer-date">
            {{ formatDateFull(ticket.resolvedUtc) }}
          </span>
        </div>
        <p class="answer-body">{{ ticket.answer }}</p>
      </div>

      <!-- Actions for open tickets -->
      <div v-if="isOpen" class="ticket-actions">
        <Button
          v-if="!ticket.assignedModeratorUsername"
          type="button"
          :loading="assigning"
          @click="assignToMe"
        >
          Взять в работу
        </Button>

        <section class="resolve-form">
          <BlockTitle>Ответ модерации</BlockTitle>

          <Form
            :valid="canSubmit"
            :loading="resolving"
            action="Разрешить обращение"
            @submit="resolve"
          >
            <FormField label="Ответ" name="ticket-answer">
              <BBCodeEditor
                v-model="answer"
                context="common"
                placeholder="Текст ответа..."
                :disabled="resolving"
                :min-height="100"
                :max-height="300"
                :is-moderator="true"
              />
            </FormField>

            <FormField label="Статус решения" name="ticket-resolution">
              <Select
                :model-value="resolutionStatus"
                :options="statusOptions"
                @update:model-value="
                  (v) => (resolutionStatus = v as TicketStatus)
                "
              />
            </FormField>

            <template v-if="canSanction">
              <FormField name="ticket-issue-warning">
                <label class="checkbox-label">
                  <input v-model="issueWarning" type="checkbox" />
                  Выдать предупреждение пользователю
                  {{ ticket.targetUsername }}
                </label>
              </FormField>

              <template v-if="issueWarning">
                <FormField
                  label="Тип предупреждения"
                  name="ticket-warning-kind"
                >
                  <Select
                    :model-value="warningKind"
                    :options="warningKindOptions"
                    @update:model-value="
                      (v) => (warningKind = v as 'verbal' | 'scored')
                    "
                  />
                </FormField>
                <FormField
                  v-if="warningKind === 'scored'"
                  label="Баллы"
                  name="ticket-warning-points"
                >
                  <Select
                    :model-value="warningPoints"
                    :options="warningPointsOptions"
                    @update:model-value="(v) => (warningPoints = v)"
                  />
                </FormField>
                <FormField
                  label="Причина предупреждения"
                  name="ticket-warning-text"
                >
                  <textarea
                    v-model="warningText"
                    rows="3"
                    maxlength="2000"
                    placeholder="Опишите нарушение..."
                  ></textarea>
                </FormField>
              </template>

              <template v-if="isSeniorModerator">
                <FormField name="ticket-issue-ban">
                  <label class="checkbox-label">
                    <input v-model="issueBan" type="checkbox" />
                    Выдать бан пользователю {{ ticket.targetUsername }}
                  </label>
                </FormField>

                <template v-if="issueBan">
                  <FormField label="Срок бана" name="ticket-ban-duration">
                    <Select
                      :model-value="banDuration"
                      :options="banDurationOptions"
                      @update:model-value="(v) => (banDuration = v)"
                    />
                  </FormField>
                  <FormField label="Причина бана" name="ticket-ban-comment">
                    <textarea
                      v-model="banComment"
                      rows="3"
                      maxlength="2000"
                      placeholder="Причина бана..."
                    ></textarea>
                  </FormField>
                </template>
              </template>
            </template>
          </Form>
        </section>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

.moderation-ticket
  display: flex
  flex-direction: column
  gap: $medium

// --- Answer block (doc 4.2.2.24) ---
.ticket-answer
  +card()

.answer-meta
  display: flex
  align-items: baseline
  gap: $small
  margin-bottom: $small
  color: $text-muted
  font-size: $secondary-font-size

.answer-role
  color: $accent-green
  font-weight: bold

.answer-date
  margin-left: auto

.answer-body
  margin: 0
  white-space: pre-line
  overflow-wrap: anywhere

// --- Actions ---
.ticket-actions
  display: flex
  flex-direction: column
  gap: $medium
  align-items: flex-start

.resolve-form
  width: 100%

.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

// --- Skeleton (mirrors the full TicketCard: $medium padding, 5 lines) ---
.skeleton-card
  +card()

.skeleton-line
  height: 1em
  margin-bottom: $small
  +skeleton-shimmer

  &:last-child
    margin-bottom: 0

.skeleton-title
  width: 40%

.skeleton-meta
  width: 65%

.skeleton-body
  width: 95%

.skeleton-body-short
  width: 70%
</style>
