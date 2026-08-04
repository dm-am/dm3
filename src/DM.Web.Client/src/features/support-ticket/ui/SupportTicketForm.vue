<script setup lang="ts">
/**
 * SupportTicketForm - ticket ("обращение") submission form for /support
 * and /complaint.
 *
 * One component for both kinds: the fields are the same except for the
 * complaint-only "violation link" field and the wording. Guests are allowed
 * to submit - losing account access must not lock a user out of support -
 * so the form never requires authentication.
 *
 * The body is a plain multiline field, not the BBCode editor: every surface
 * that reads a ticket back (MyTicketsPage, TicketTrackPage, the moderation
 * TicketCard) prints it verbatim, so markup typed here would only ever be read
 * back as brackets.
 */
import { computed, ref } from "vue";
import { TextArea } from "@/shared/ui/TextArea";
import { ticketApi, type TicketSubtype } from "@/entities/ticket";
import type { BadRequestError } from "@/shared/api/models/common";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import { useAuthStore } from "@/shared/stores/auth";
import { Select, type SelectOption } from "@/shared/ui/Select";

const props = defineProps<{
  kind: "support" | "complaint";
}>();

// Subtype options per page (doc 4.2.3.2.4/5). Labels mirror the backend
// TicketSubtype [Description] attributes.
//
// One list per page, and the two lists share nothing: support is "the site is
// in my way", a complaint is "a person or a moderator decision is". The owner
// settled this after the audit proposed a single grouped picker on both pages.
// Two pages over one form, each with its own reasons and its own surroundings.
const SUPPORT_SUBTYPES: { value: TicketSubtype; label: string }[] = [
  { value: "Bug", label: "Ошибка" },
  { value: "AccessRecovery", label: "Восстановление доступа" },
  { value: "RegistrationIssue", label: "Проблемы с регистрацией" },
];
const COMPLAINT_SUBTYPES: { value: TicketSubtype; label: string }[] = [
  { value: "UserComplaint", label: "Жалоба на пользователя" },
  {
    value: "ModeratorDecisionComplaint",
    label: "Жалоба на решение младшего модератора",
  },
  {
    value: "SeniorModeratorDecisionComplaint",
    label: "Жалоба на решение старшего модератора",
  },
  {
    value: "SiteImprovementSuggestion",
    label: "Предложение по улучшению сайта",
  },
];

const subtypeOptions = computed<SelectOption[]>(() =>
  props.kind === "complaint" ? COMPLAINT_SUBTYPES : SUPPORT_SUBTYPES,
);
// Select emits a plain string; keep it as string and narrow on submit.
const subtype = ref<string>(
  props.kind === "complaint" ? "UserComplaint" : "Bug",
);

const authStore = useAuthStore();
// Guests have no account to reach them by, so a contact email is mandatory:
// no ticket mail is sent yet, so after submit the tracking number and the link
// to it are shown on screen instead. Authenticated authors are reachable by
// identity and track their tickets from "Мои обращения".
const isGuest = computed(() => !authStore.isAuthenticated);

const subject = ref("");
const text = ref("");
const contact = ref("");
const violationUrl = ref("");
const honeypot = ref("");

const loading = ref(false);
const sent = ref(false);
// Guest tracking token returned on submit (null for authenticated authors)
const trackingToken = ref<string | null>(null);

const subjectError = ref("");
const textError = ref("");
const contactError = ref("");
const violationUrlError = ref("");

// The page decides: its list of reasons holds nothing from the other one, so
// the wording and the complaint-only "Ссылка на нарушение" field follow the
// door the reader came through.
const isComplaint = computed(() => props.kind === "complaint");

// Pragmatic email shape check — the server is the source of truth, this only
// keeps the guest from submitting an obviously invalid address.
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const contactLabel = computed(() =>
  isGuest.value ? "Почта для ответа" : "Контакт для ответа",
);
const contactHint = computed(() =>
  isGuest.value
    ? "Адрес нужен модерации для связи. Ответ появится на странице обращения, номер и ссылку вы увидите после отправки"
    : 'Email или Discord для связи помимо сайта. Ответ появится в разделе "Мои обращения"',
);

const copy = computed(() =>
  isComplaint.value
    ? {
        subtypeLabel: "Тип жалобы",
        subjectLabel: "Тема жалобы",
        subjectPlaceholder: "Кратко: кто и что нарушает",
        textLabel: "Текст жалобы",
        textPlaceholder: "Опишите нарушение: что произошло, где и когда...",
        action: "Отправить жалобу",
        successTitle: "Жалоба отправлена",
        successNext: "Модерация рассмотрит жалобу в ближайшее время",
        again: "Отправить еще одну жалобу",
      }
    : {
        subtypeLabel: "Тип обращения",
        subjectLabel: "Тема обращения",
        subjectPlaceholder: "Кратко: что случилось",
        textLabel: "Описание проблемы",
        textPlaceholder:
          "Опишите проблему: что вы делали, что ожидали и что произошло...",
        action: "Отправить",
        successTitle: "Обращение отправлено",
        successNext: "Мы рассмотрим обращение в ближайшее время",
        again: "Отправить еще одно обращение",
      },
);

const canSubmit = computed(
  () =>
    subject.value.trim().length > 0 &&
    text.value.trim().length > 0 &&
    (!isGuest.value || contact.value.trim().length > 0),
);

const clearErrors = () => {
  subjectError.value = "";
  textError.value = "";
  contactError.value = "";
  violationUrlError.value = "";
};

const validate = (): boolean => {
  clearErrors();
  if (!subject.value.trim()) {
    subjectError.value = "Empty";
  } else if (subject.value.length > 200) {
    subjectError.value = "Long";
  }
  if (!text.value.trim()) {
    textError.value = "Empty";
  } else if (text.value.length > 10000) {
    textError.value = "Long";
  }
  if (isGuest.value) {
    // Guests must give a valid email — it is the only way to reach them.
    const c = contact.value.trim();
    if (!c) contactError.value = "Empty";
    else if (!EMAIL_RE.test(c)) contactError.value = "Invalid";
    else if (contact.value.length > 200) contactError.value = "Long";
  } else if (contact.value.length > 200) {
    // Authenticated authors may leave any contact (email or Discord).
    contactError.value = "Long";
  }
  if (violationUrl.value.length > 500) {
    violationUrlError.value = "Long";
  }
  return (
    !subjectError.value &&
    !textError.value &&
    !contactError.value &&
    !violationUrlError.value
  );
};

const submit = async () => {
  if (loading.value || !validate()) return;

  loading.value = true;
  const { data, error } = await ticketApi.createTicket({
    subtype: subtype.value as TicketSubtype,
    subject: subject.value.trim(),
    text: text.value.trim(),
    contact: contact.value.trim() || undefined,
    violationUrl: isComplaint.value
      ? violationUrl.value.trim() || undefined
      : undefined,
    website: honeypot.value,
  });
  loading.value = false;

  if (error) {
    const errors = parseApiErrors(error as BadRequestError);
    subjectError.value = getFieldError(errors, "subject") ?? "";
    textError.value = getFieldError(errors, "text") ?? "";
    contactError.value = getFieldError(errors, "contact") ?? "";
    violationUrlError.value = getFieldError(errors, "violationUrl") ?? "";
    return;
  }

  // Guests get a tracking token to follow up without an account
  trackingToken.value = data?.trackingToken ?? null;
  sent.value = true;
};

const reset = () => {
  subject.value = "";
  text.value = "";
  contact.value = "";
  violationUrl.value = "";
  honeypot.value = "";
  clearErrors();
  trackingToken.value = null;
  sent.value = false;
};
</script>

<template>
  <div v-if="sent" class="success-card" role="status">
    <p class="success-title">{{ copy.successTitle }}</p>
    <p class="success-next">{{ copy.successNext }}</p>

    <div v-if="trackingToken" class="tracking-block">
      <p class="tracking-number">
        Номер обращения: <code>{{ trackingToken }}</code>
      </p>
      <router-link
        class="tracking-link"
        :to="{ name: 'support-track', params: { token: trackingToken } }"
      >
        Проверить статус обращения
      </router-link>
      <p class="tracking-hint">
        Сохраните номер и ссылку. Писем по обращениям мы не отправляем, ответ
        модерации появится на странице обращения
      </p>
    </div>

    <p v-else-if="!isGuest" class="success-next">
      Ответ появится в разделе
      <router-link :to="{ name: 'my-tickets' }">Мои обращения</router-link>
    </p>

    <button type="button" class="success-again" @click="reset">
      {{ copy.again }}
    </button>
  </div>

  <Form
    v-else
    class="support-form"
    :valid="canSubmit"
    :loading="loading"
    :action="copy.action"
    @submit="submit"
  >
    <form-field :label="copy.subtypeLabel" name="subtype">
      <Select v-model="subtype" :options="subtypeOptions" :disabled="loading" />
    </form-field>

    <form-field
      :label="contactLabel"
      name="contact"
      :optional="!isGuest"
      :errors="contactError ? [contactError] : []"
    >
      <template #hint>{{ contactHint }}</template>
      <input
        v-model="contact"
        :type="isGuest ? 'email' : 'text'"
        id="contact"
        maxlength="200"
        autocomplete="email"
        :disabled="loading"
      />
    </form-field>

    <form-field
      :label="copy.subjectLabel"
      name="subject"
      :errors="subjectError ? [subjectError] : []"
    >
      <input
        v-model="subject"
        type="text"
        id="subject"
        maxlength="200"
        :placeholder="copy.subjectPlaceholder"
        :disabled="loading"
      />
    </form-field>

    <form-field
      v-if="isComplaint"
      label="Ссылка на нарушение"
      name="violationUrl"
      optional
      :errors="violationUrlError ? [violationUrlError] : []"
    >
      <input
        v-model="violationUrl"
        type="url"
        id="violationUrl"
        maxlength="500"
        placeholder="https://..."
        :disabled="loading"
      />
    </form-field>

    <form-field
      :label="copy.textLabel"
      name="text"
      :errors="textError ? [textError] : []"
    >
      <TextArea
        v-model="text"
        :placeholder="copy.textPlaceholder"
        :disabled="loading"
        :max-length="10000"
      />
    </form-field>

    <input
      name="website"
      v-model="honeypot"
      class="honeypot-field"
      autocomplete="off"
      tabindex="-1"
      aria-hidden="true"
    />
  </Form>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Variables"
@import "@/assets/styles/Themes"

// The panel the site's other forms sit in (the create-game section, the
// create-topic card, the registration dialog). The shared Form draws its
// action bar with negative margins that assume exactly this $medium of
// padding around it; with no panel the bar hangs outside the content.
.support-form
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.success-card
  padding: $medium
  margin: $medium 0
  background-color: $bg-element
  border: 1px solid $border-accent-green

.success-title
  margin: 0 0 $small
  font-weight: 700

.success-next
  margin: 0 0 $medium
  color: $text-muted

.tracking-block
  padding: $small
  margin: 0 0 $medium
  background-color: $bg-page
  border: 1px dashed $border

.tracking-number
  margin: 0 0 $small

  code
    font-family: $code-font
    word-break: break-all

.tracking-link
  font-weight: 700
  color: $link
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

.tracking-hint
  margin: $small 0 0
  color: $text-muted
  font-size: $secondary-font-size

// Button-as-link — matches inline link buttons used across auth forms
.success-again
  padding: 0
  background: none
  border: none
  color: $link
  font-size: inherit
  cursor: pointer

  &:hover
    color: $link-hover
    text-decoration: underline
</style>
