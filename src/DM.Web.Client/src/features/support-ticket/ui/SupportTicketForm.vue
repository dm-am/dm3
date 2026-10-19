<script setup lang="ts">
// One form for /support and /complaint; guests may file most reasons, losing access included.
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import {
  ticketApi,
  TICKET_TEXT_MAX_LENGTH,
  TICKET_VIOLATION_URL_MAX_LENGTH,
  type TicketSubtype,
} from "@/entities/ticket";
import type { BadRequestError } from "@/shared/api/models/common";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import { describeFailure, SERVER_ERROR } from "@/shared/lib/errors";
import { useBotTrap } from "@/shared/lib/composables/useBotTrap";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { useAuthStore } from "@/shared/stores/auth";
import { Select } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import Button from "@/shared/ui/Button/Button.vue";
import HoneypotField from "@/shared/ui/Form/HoneypotField.vue";
import {
  memberOnlyAction,
  reasonHintOf,
  reasonNoteOf,
  reasonOptionsOf,
  requestedReasonOf,
  type SupportTicketKind,
  type Note,
  type NoteLink,
} from "../model/ticketReasons";
import TicketContactField, {
  contactProblem,
  contactRefusal,
} from "./TicketContactField.vue";
import TicketSent from "./TicketSent.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { USERNAME_MAX_LENGTH } from "@/shared/lib/constants/user";

const props = defineProps<{
  kind: SupportTicketKind;
}>();

defineSlots<{
  /** The page draws it: LoginPrompt is a feature of its own. */
  login?: (props: { action: string }) => unknown;
}>();

// A note's window opens over the page on ?action=, so the address is this page's own.
const route = useRoute();
const noteLinkTo = (link: NoteLink) =>
  "action" in link
    ? { query: { ...route.query, action: link.action } }
    : link.to;

const COPY: Record<
  SupportTicketKind,
  { reason: string; placeholder: string; sent: string; again: string }
> = {
  support: {
    reason: "Причина обращения",
    placeholder: "Опишите проблему",
    sent: "Обращение отправлено",
    again: "Написать еще одно обращение",
  },
  complaint: {
    reason: "Причина жалобы",
    placeholder: "Опишите нарушение",
    sent: "Жалоба отправлена",
    again: "Написать еще одну жалобу",
  },
};

const copy = COPY[props.kind];
const isComplaint = props.kind === "complaint";

const reasonOptions = reasonOptionsOf(props.kind);
const requestedReason = () => requestedReasonOf(props.kind, route.query);

// Select emits a plain string: a value of the page's list or the placeholder's empty one.
const reason = ref<string>(requestedReason());
const chosen = computed(() => reason.value as TicketSubtype | "");

const authStore = useAuthStore();
const isGuest = computed(() => !authStore.isAuthenticated);
const username = computed(() => authStore.user?.username ?? "");

const reasonHint = computed(() =>
  chosen.value ? reasonHintOf(chosen.value, isGuest.value) : "",
);

const reasonNote = computed((): Note | undefined =>
  chosen.value ? reasonNoteOf(chosen.value, isGuest.value) : undefined,
);

const signInAction = computed(() =>
  isGuest.value && chosen.value ? memberOnlyAction(chosen.value) : undefined,
);

const showFields = computed(() => !!chosen.value && !signInAction.value);

const aboutPerson = computed(() => chosen.value === "UserComplaint");

const aboutDecision = computed(
  () =>
    chosen.value === "ModeratorDecisionComplaint" ||
    chosen.value === "SeniorModeratorDecisionComplaint",
);

const linkLabel = computed(() =>
  aboutPerson.value ? "Ссылка на нарушение" : "Ссылка на решение",
);

const linkHint = computed(() =>
  aboutPerson.value
    ? "Адрес страницы, где это случилось: пост, комментарий, топик, чат или профиль"
    : "Пост или комментарий, за который вынесено решение",
);

const text = ref("");
const contact = ref("");
const violator = ref("");
const violationUrl = ref("");
const { honeypot } = useBotTrap();

const textError = ref("");
const contactError = ref("");
const violatorError = ref("");
const violationUrlError = ref("");
const reasonError = ref("");
const refusal = ref("");

const loading = ref(false);
const sent = ref(false);
const trackingToken = ref<string | null>(null);
const ticketId = ref<string | null>(null);

const editor = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const rateLimit = useRateLimitNotice();
const refusalLine = computed(() => rateLimit.message.value || refusal.value);

const isWebAddress = (value: string): boolean => {
  try {
    const address = new URL(value);
    return address.protocol === "http:" || address.protocol === "https:";
  } catch {
    return false;
  }
};
const LINK_INVALID = "Нужен адрес страницы, начиная с https://";

// In the wording of the form's own check, so one mistake is not named in two ways.
const linkRefusal = (fields: Record<string, string[]>): string =>
  fields.violationurl?.[0] === "Invalid"
    ? LINK_INVALID
    : (readFieldError(fields, "violationUrl") ?? "");

const TEXT_MAX = TICKET_TEXT_MAX_LENGTH;

const validate = (): boolean => {
  textError.value = !text.value.trim()
    ? "Empty"
    : text.value.length > TEXT_MAX
      ? "Long"
      : "";

  contactError.value = isGuest.value ? contactProblem(contact.value) : "";

  violatorError.value =
    aboutPerson.value && !violator.value.trim() ? "Empty" : "";

  const link = violationUrl.value.trim();
  violationUrlError.value =
    link.length > 500
      ? "Long"
      : aboutPerson.value && !link
        ? "Empty"
        : link && aboutPerson.value && !isWebAddress(link)
          ? LINK_INVALID
          : "";

  return (
    !textError.value &&
    !contactError.value &&
    !violatorError.value &&
    !violationUrlError.value
  );
};

// A dead connection and an expired session are the interceptor's to say.
const showRefusal = (error: BadRequestError) => {
  if (rateLimit.take(error)) return;
  if (error.status === 0 || error.status === 401) return;

  const fields = parseApiErrors(error);
  textError.value = readFieldError(fields, "text") ?? "";
  contactError.value = isGuest.value ? contactRefusal(fields) : "";
  violatorError.value = aboutPerson.value
    ? (readFieldError(fields, "targetUsername") ?? "")
    : "";
  violationUrlError.value = isComplaint ? linkRefusal(fields) : "";
  reasonError.value = readFieldError(fields, "subtype") ?? "";

  const placed =
    textError.value ||
    contactError.value ||
    violatorError.value ||
    violationUrlError.value ||
    reasonError.value;
  if (!placed) refusal.value = describeFailure(error, SERVER_ERROR);
};

const submit = async () => {
  if (!showFields.value || loading.value || rateLimit.waiting.value) return;
  refusal.value = "";
  reasonError.value = "";
  if (!validate()) return;

  loading.value = true;
  const { data, error } = await ticketApi.createTicket(
    {
      subtype: chosen.value as TicketSubtype,
      text: text.value,
      contact: isGuest.value ? contact.value.trim() : undefined,
      violationUrl: isComplaint
        ? violationUrl.value.trim() || undefined
        : undefined,
      targetUsername: aboutPerson.value ? violator.value.trim() : undefined,
      website: honeypot.value,
    },
    OWNS_REFUSAL,
  );
  loading.value = false;

  if (error) {
    showRefusal(error as BadRequestError);
    return;
  }

  editor.value?.clearDraft();
  trackingToken.value = data?.trackingToken ?? null;
  ticketId.value = data?.ticketId ?? null;
  // The answer takes the place of the form, and the focus with it (TicketSent).
  sent.value = true;
};

// Watched on the values: the editor also emits when it is only unlocked after the request.
watch(text, () => {
  textError.value = "";
  refusal.value = "";
});
watch(contact, () => {
  contactError.value = "";
  refusal.value = "";
});
watch(violator, () => {
  violatorError.value = "";
  refusal.value = "";
});
watch(violationUrl, () => {
  violationUrlError.value = "";
  refusal.value = "";
});
watch(reason, () => {
  reasonError.value = "";
  violatorError.value = "";
  violationUrlError.value = "";
  refusal.value = "";
});

const reset = () => {
  text.value = "";
  contact.value = "";
  violator.value = "";
  violationUrl.value = "";
  honeypot.value = "";
  textError.value = "";
  contactError.value = "";
  violatorError.value = "";
  violationUrlError.value = "";
  reasonError.value = "";
  refusal.value = "";
  rateLimit.clear();
  reason.value = requestedReason() || reason.value;
  trackingToken.value = null;
  ticketId.value = null;
  sent.value = false;
};
</script>

<template>
  <TicketSent
    v-if="sent"
    :title="copy.sent"
    :again="copy.again"
    :tracking-token="trackingToken"
    :ticket-id="ticketId"
    @again="reset"
  />

  <form v-else class="ticket-form" novalidate @submit.prevent="submit">
    <form-field
      :label="copy.reason"
      name="subtype"
      :errors="reasonError ? [reasonError] : []"
    >
      <Select
        id="subtype"
        v-model="reason"
        :options="reasonOptions"
        placeholder="Выберите причину"
        :disabled="loading"
      />
      <template v-if="reasonNote || signInAction" #hint>
        <div class="reason-note">
          <p v-if="reasonNote?.lead" class="reason-note-line is-lead">
            {{ reasonNote.lead.before }}
          </p>
          <div v-if="signInAction" class="reason-note-line is-login">
            <slot name="login" :action="signInAction" />
          </div>
          <p
            v-for="(noteLine, index) in reasonNote?.lines ?? []"
            :key="index"
            class="reason-note-line"
          >
            {{ noteLine.before
            }}<router-link
              v-if="noteLine.link"
              :to="noteLinkTo(noteLine.link)"
              >{{ noteLine.link.text }}</router-link
            >{{ noteLine.after ?? "" }}
          </p>
        </div>
      </template>
    </form-field>

    <template v-if="showFields">
      <TicketContactField
        v-model="contact"
        :is-guest="isGuest"
        :username="username"
        :error="contactError"
        :disabled="loading"
      />

      <form-field
        v-if="aboutPerson"
        label="Нарушитель"
        name="violator"
        :errors="violatorError ? [violatorError] : []"
      >
        <input
          id="violator"
          v-model="violator"
          type="text"
          :maxlength="USERNAME_MAX_LENGTH"
          autocomplete="off"
          placeholder="Имя пользователя"
          :disabled="loading"
        />
      </form-field>

      <form-field
        v-if="aboutPerson || aboutDecision"
        :label="linkLabel"
        name="violationUrl"
        :optional="!aboutPerson"
        :errors="violationUrlError ? [violationUrlError] : []"
      >
        <input
          id="violationUrl"
          v-model="violationUrl"
          type="url"
          :maxlength="TICKET_VIOLATION_URL_MAX_LENGTH"
          placeholder="https://..."
          :disabled="loading"
        />
        <template #hint>{{ linkHint }}</template>
      </form-field>

      <form-field
        label="Описание"
        name="text"
        :errors="textError ? [textError] : []"
      >
        <p v-if="reasonHint" class="reason-hint">{{ reasonHint }}</p>
        <BBCodeEditor
          ref="editor"
          v-model="text"
          context="info"
          :draft-key="composerDraftKey(kind, 'ticket')"
          :placeholder="copy.placeholder"
          :disabled="loading"
          :min-height="200"
          :max-length="TEXT_MAX"
          @submit="submit"
        />
      </form-field>

      <HoneypotField v-model="honeypot" />

      <p v-if="refusalLine" class="ticket-refusal" role="alert">
        {{ refusalLine }}
      </p>

      <div class="ticket-actions">
        <Button
          variant="primary"
          :loading="loading"
          :disabled="rateLimit.waiting.value"
          >Отправить</Button
        >
      </div>
    </template>
  </form>
</template>

<style scoped lang="sass">
.ticket-form
  display: flex
  flex-direction: column
  gap: $medium

  :deep(.form-field__labeled)
    margin: 0

.reason-hint
  margin: 0 0 $minor
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4

.reason-note
  line-height: 1.45

.reason-note-line
  margin: 0

  & + &
    margin-top: $minor

  &.is-lead
    color: $text

  // The colour and the underline come of the site's link; only the weight is its own.
  a
    font-weight: 700

.ticket-refusal
  margin: 0
  color: $accent-red
  font-size: $secondary-font-size

.ticket-actions
  display: flex
</style>
