<script setup lang="ts">
/**
 * SupportTicketForm - the ticket ("обращение") form of /support and /complaint.
 *
 * One component for both pages: the fields are the same, the list of reasons
 * and a few words differ, and the complaint adds the violation link. Guests
 * may submit most reasons - losing access to the account must not lock a
 * person out of the one place that can give it back.
 *
 * The shape and the words follow the error report form of the old site: the
 * reason, the contact, what happened, one button. The contact keeps the old
 * caption, "Контактные данные". A guest fills it with an address; its hint is
 * the old one without "для обратной связи": the answer is on the ticket's page
 * and no letter is sent, which the reader learns after sending, so the hint
 * must not promise one. A signed-in author is known by the account, so the
 * same field holds his name and cannot be changed: the reader sees who the
 * ticket comes from, and the site does not ask for what it already knows.
 * There is no subject line: the reason and the text say it, and every screen
 * that reads a ticket back names it by its reason when no subject was given.
 *
 * The reason comes first and the fields after it (the owner, 28.09: "как
 * сделать так, чтобы модерацию не заебывали хуйней"). The picker opens on
 * "Выберите причину" and nothing else stands under it until one is chosen, so
 * nobody files a question as an error because the error was preselected. Under
 * the chosen reason stands a note, where the hints of the other fields stand:
 * when to write here, and what a reader can do himself, the windows that do it
 * opening over the page. A link that already knows the reason (supportFor)
 * opens the form on it, fields and all.
 *
 * Two reasons are filed from an account only, as on DM2: a complaint about a
 * person, which moderation has to be able to write back about, and the removal
 * of an account, which is honoured for its owner. A guest gets the sign-in
 * line in their place, drawn by the page through the `login` slot (LoginPrompt
 * is a feature of its own, and a feature does not import another), and a way
 * out for whoever cannot sign in. The complaint about a person names the
 * violator and the link to the violation, both required; the server writes
 * them into the text of the ticket as lines of it.
 *
 * Where the answer appears is said once, after sending, where the reader can
 * act on it: the guest gets the ticket's page, the member his tickets.
 *
 * The text is written in the site's markup editor, both modes, like everything
 * else a person writes here, and autosaves as a draft under the form's own key.
 */
import { computed, nextTick, ref, watch } from "vue";
import { useRoute, useRouter, type RouteLocationRaw } from "vue-router";
import {
  ticketApi,
  TICKET_SUBTYPE_LABELS,
  TICKET_TEXT_MAX_LENGTH,
  type TicketSubtype,
} from "@/entities/ticket";
import { removedAccountRoute } from "@/entities/user";
import type { BadRequestError } from "@/shared/api/models/common";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import { describeFailure, SERVER_ERROR } from "@/shared/lib/errors";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { permalinkOrigin } from "@/shared/config/site";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { useAuthStore } from "@/shared/stores/auth";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import Button from "@/shared/ui/Button/Button.vue";

type Kind = "support" | "complaint";

const props = defineProps<{
  kind: Kind;
}>();

defineSlots<{
  /**
   * The site's sign-in line, "Войдите, чтобы <action>", for a reason that is
   * filed from an account. The page draws it: it is a feature of its own.
   */
  login?: (props: { action: string }) => unknown;
}>();

/**
 * The reasons each page offers, in the order it offers them. The two lists
 * share nothing: support is "the site is in my way", a complaint is "a person
 * or a moderator decision is". A suggestion is neither and is not filed here
 * at all: ideas for the site are discussed on the forum, in the open.
 */
const REASONS: Record<Kind, TicketSubtype[]> = {
  support: [
    "Bug",
    "AccessRecovery",
    "RegistrationIssue",
    "AccountRemoval",
    "AccountRestoration",
    "PersonalData",
    "AgreementQuestion",
  ],
  complaint: [
    "UserComplaint",
    "ModeratorDecisionComplaint",
    "SeniorModeratorDecisionComplaint",
  ],
};

/** A word a reader tells apart by who is reading: a guest or a member. */
type ByReader<T> = T | { guest: T; member: T };

/**
 * What the reader of each reason is asked to put into the text: the facts the
 * administration would otherwise have to ask for in a second round. The error
 * keeps the old site's words, the note its form printed under the button.
 *
 * Access recovery asks a guest for what only the owner knows, the names of his
 * games and characters and roughly when he registered: without the mailbox,
 * that is how the administration tells the owner from somebody who knows his
 * name, and asked for up front it spares the second letter.
 */
const REASON_HINTS: Partial<Record<TicketSubtype, ByReader<string>>> = {
  Bug: "Пожалуйста, скопируйте адрес страницы, на которой была замечена ошибка, а также постарайтесь указать, как можно воспроизвести ошибку",
  AccessRecovery: {
    guest:
      "Напишите имя пользователя и почту аккаунта, а также то, что знает только владелец: названия ваших игр и персонажей, примерную дату регистрации",
    member: "Напишите, что не получается и что при этом показывает сайт",
  },
  RegistrationIssue:
    "Напишите почту, на которую регистрируетесь, и что пошло не так",
  AccountRemoval:
    "Если удалить нужно не этот аккаунт, а другой, напишите его имя пользователя",
  AccountRestoration:
    "Напишите имя пользователя удаленного аккаунта и почту, к которой он был привязан",
  PersonalData:
    "Напишите, что сделать с вашими данными: показать, исправить, удалить или прекратить обработку",
  AgreementQuestion: "Напишите, какого пункта соглашения касается вопрос",
  UserComplaint:
    "Напишите, что произошло, а если это было в личных сообщениях, скопируйте сюда их текст",
  ModeratorDecisionComplaint: {
    guest:
      "Напишите имя пользователя аккаунта, какое решение оспариваете и почему не согласны",
    member: "Напишите, какое решение оспариваете и почему не согласны",
  },
  SeniorModeratorDecisionComplaint: {
    guest:
      "Напишите имя пользователя аккаунта, какое решение оспариваете и почему не согласны",
    member: "Напишите, какое решение оспариваете и почему не согласны",
  },
};

/**
 * One line of the note under the reason: text with at most one link in it,
 * the link either a place to go or a window to open over the page.
 */
type NoteLine = {
  before: string;
  link?: { text: string; to: RouteLocationRaw };
  after?: string;
};

/** The note under a reason: the lead line, and the grey lines after it. */
type Note = { lead?: NoteLine; lines: NoteLine[] };

/**
 * The windows the header opens on ?action=: the recovery of access and the
 * request to switch the second factor off (GuestActions.vue). Opened over the
 * page, so the form stays where it is.
 */
const route = useRoute();
const actionLink = (action: string): RouteLocationRaw => ({
  query: { ...route.query, action },
});

/** The two decision complaints share their note. */
const APPEAL_NOTE: Note = {
  lines: [
    {
      before: 'Кто из модераторов старший, видно в разделе "',
      link: {
        text: "Администрация",
        to: { name: "rules", hash: "#admins" },
      },
      after: '" правил',
    },
    {
      before:
        "Решения по жалобам принимаются коллегиально, хамство в жалобе добавляет баллы",
    },
  ],
};

/**
 * When to write here, and what can be done without the administration. The
 * reasons with no note have no other way than this form: an error is covered
 * by the page's own text, and the rest only the administration can answer.
 */
const REASON_NOTES = computed(
  (): Partial<Record<TicketSubtype, ByReader<Note>>> => ({
    AccessRecovery: {
      guest: {
        lead: { before: "Сюда пишут, если нет доступа к почте аккаунта" },
        lines: [
          {
            before: "Забыли пароль или не пришло письмо активации: ",
            link: {
              text: "Восстановление доступа",
              to: actionLink("recovery"),
            },
            after: " пришлет ссылку на почту",
          },
          {
            before: "Нет доступа к приложению и резервным кодам: ",
            link: {
              text: "Запрос на отключение",
              to: actionLink("two-factor"),
            },
            after:
              " пришлет письмо, ссылка из него отключит двухфакторную аутентификацию через 7 дней",
          },
        ],
      },
      member: {
        lead: { before: "Вы уже вошли в аккаунт" },
        lines: [
          {
            before: "Пароль, почту и двухфакторную аутентификацию меняют в ",
            link: { text: "настройках аккаунта", to: { name: "account" } },
          },
        ],
      },
    },
    RegistrationIssue: {
      guest: {
        lead: {
          before:
            "Сюда пишут, если регистрация не проходит или письмо активации не приходит и после повторной отправки",
        },
        lines: [
          {
            before:
              'Письмо могло попасть в папку "Спам", отправить его еще раз можно через ',
            link: {
              text: "Восстановление доступа",
              to: actionLink("recovery"),
            },
          },
        ],
      },
      member: { lines: [] },
    },
    AccountRemoval: {
      guest: {
        lead: { before: "Удаляем аккаунт только по просьбе из него самого" },
        lines: [
          {
            before:
              'Не получается войти: выберите причину "Восстановление доступа"',
          },
        ],
      },
      member: {
        lead: {
          before:
            'Посты, комментарии и сообщения останутся на месте, вместо имени над ними будет "Аккаунт удален"',
        },
        lines: [
          {
            before:
              'Вернуть имя можно через поддержку, подробнее на странице "',
            link: { text: "Аккаунт удален", to: removedAccountRoute },
            after: '"',
          },
        ],
      },
    },
    UserComplaint: {
      guest: {
        lead: {
          before: "Жалобу на пользователя через форму подают из аккаунта",
        },
        lines: [
          {
            before:
              'Аккаунт в бане: выберите причину "Жалоба на решение старшего модератора"',
          },
        ],
      },
      member: { lines: [] },
    },
    ModeratorDecisionComplaint: APPEAL_NOTE,
    SeniorModeratorDecisionComplaint: APPEAL_NOTE,
  }),
);

/**
 * The reasons a guest cannot file, and what the sign-in line says for each:
 * "Войдите, чтобы <action>". The server refuses both from a guest as well
 * (CreateTicketIntakeValidator).
 */
const MEMBER_ONLY: Partial<Record<TicketSubtype, string>> = {
  UserComplaint: "пожаловаться на пользователя",
  AccountRemoval: "запросить удаление аккаунта",
};

/** The words that differ between the two pages. */
const COPY: Record<
  Kind,
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

/** One vocabulary: the picker names a reason the way every ticket list does. */
const reasonOptions: SelectOption[] = REASONS[props.kind].map((value) => ({
  value,
  label: TICKET_SUBTYPE_LABELS[value],
}));

/**
 * The reason the form opens on, or none. The address may name one - supportFor
 * builds it for every page that sends a reader here for one thing - and only a
 * reason this page offers is taken: a stale or made-up value would file the
 * ticket under a category the page never showed. Without it the picker shows
 * "Выберите причину" and the form waits for the reader's choice. It is the
 * starting value and not a binding, so the reader may pick another.
 */
const requestedReason = (): TicketSubtype | "" => {
  const requested = route.query.subtype;
  const name = Array.isArray(requested) ? requested[0] : requested;
  return REASONS[props.kind].find((reason) => reason === name) ?? "";
};

// Select emits a plain string; it only ever holds a value from the list above,
// or the empty one of the placeholder.
const reason = ref<string>(requestedReason());
const chosen = computed(() => reason.value as TicketSubtype | "");

const authStore = useAuthStore();
const isGuest = computed(() => !authStore.isAuthenticated);
const username = computed(() => authStore.user?.username ?? "");

const forReader = <T,>(value: ByReader<T> | undefined): T | undefined => {
  if (value === undefined) return undefined;
  if (typeof value === "object" && value !== null && "guest" in value)
    return isGuest.value ? value.guest : value.member;
  return value as T;
};

const reasonHint = computed(() =>
  chosen.value ? (forReader(REASON_HINTS[chosen.value]) ?? "") : "",
);

const reasonNote = computed((): Note | undefined => {
  if (!chosen.value) return undefined;
  const note = forReader(REASON_NOTES.value[chosen.value]);
  return note && (note.lead || note.lines.length) ? note : undefined;
});

/** The sign-in line of a reason a guest cannot file, or nothing. */
const signInAction = computed(() =>
  isGuest.value && chosen.value ? MEMBER_ONLY[chosen.value] : undefined,
);

/** The fields stand once a reason is chosen, and not for a gated one. */
const showFields = computed(() => !!chosen.value && !signInAction.value);

/** A complaint about a person names him and the place, both required. */
const aboutPerson = computed(() => chosen.value === "UserComplaint");

/** The complaints about a decision point at it, optionally. */
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
const honeypot = ref("");

const textError = ref("");
const contactError = ref("");
const violatorError = ref("");
const violationUrlError = ref("");
const reasonError = ref("");
/** A refusal that belongs to no field: it stands over the button. */
const refusal = ref("");

const loading = ref(false);
const sent = ref(false);
/** The guest's way back to the ticket; null for a signed-in author. */
const trackingToken = ref<string | null>(null);
/** The ticket just filed, whose page a signed-in author opens. */
const ticketId = ref<string | null>(null);

const editor = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const confirmation = ref<HTMLElement | null>(null);

/**
 * The limit on filing tickets, shown in the form that earned it: the reader
 * who sees nothing change presses again.
 */
const rateLimit = useRateLimitNotice();
const refusalLine = computed(() => rateLimit.message.value || refusal.value);

// A pragmatic shape check: the server has the last word, this only keeps an
// obviously broken address from a round trip.
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const EMAIL_INVALID = "Неверный формат почты";

/** The address a moderator opens from the ticket: http or https, absolute. */
const isWebAddress = (value: string): boolean => {
  try {
    const address = new URL(value);
    return address.protocol === "http:" || address.protocol === "https:";
  } catch {
    return false;
  }
};
const LINK_INVALID = "Нужен адрес страницы, начиная с https://";

// The longest text the server takes, the one copy the conversation of the
// ticket counts to as well.
const TEXT_MAX = TICKET_TEXT_MAX_LENGTH;

const validate = (): boolean => {
  textError.value = !text.value.trim()
    ? "Empty"
    : text.value.length > TEXT_MAX
      ? "Long"
      : "";

  contactError.value = "";
  if (isGuest.value) {
    // The guest's address is the only way to reach him.
    const address = contact.value.trim();
    if (!address) contactError.value = "Empty";
    else if (!EMAIL_RE.test(address)) contactError.value = EMAIL_INVALID;
    else if (address.length > 200) contactError.value = "Long";
  }

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

/**
 * The server's answer to a press that did not go through. A field it names
 * gets the word under itself; anything else stands in the line over the
 * button. A dead connection and an expired session are the interceptor's to
 * say, and a second copy here would only repeat it.
 */
const showRefusal = (error: BadRequestError) => {
  if (rateLimit.take(error)) return;
  if (error.status === 0 || error.status === 401) return;

  const fields = parseApiErrors(error);
  textError.value = getFieldError(fields, "text") ?? "";
  // Only a guest has the field; the address wording is the one the form's own
  // check uses, so one mistake is not named in two ways.
  contactError.value = !isGuest.value
    ? ""
    : fields.contact?.[0] === "Invalid"
      ? EMAIL_INVALID
      : (getFieldError(fields, "contact") ?? "");
  violatorError.value = aboutPerson.value
    ? (getFieldError(fields, "targetUsername") ?? "")
    : "";
  violationUrlError.value = isComplaint
    ? fields.violationurl?.[0] === "Invalid"
      ? LINK_INVALID
      : (getFieldError(fields, "violationUrl") ?? "")
    : "";
  reasonError.value = getFieldError(fields, "subtype") ?? "";

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
      text: text.value.trim(),
      contact: isGuest.value ? contact.value.trim() : undefined,
      violationUrl: isComplaint
        ? violationUrl.value.trim() || undefined
        : undefined,
      targetUsername: aboutPerson.value ? violator.value.trim() : undefined,
      website: honeypot.value,
    },
    // The form prints every refusal of this press itself, the wait included.
    { ownsRefusal: true, ownsRateLimit: true },
  );
  loading.value = false;

  if (error) {
    showRefusal(error as BadRequestError);
    return;
  }

  // The text is filed: the draft of it has nothing left to protect.
  editor.value?.clearDraft();
  trackingToken.value = data?.trackingToken ?? null;
  ticketId.value = data?.ticketId ?? null;
  sent.value = true;
  // The form the focus stood in is gone; the answer takes its place.
  await nextTick();
  confirmation.value?.focus({ preventScroll: true });
};

/**
 * The guest's ticket page, spelled out in full as the text of its link: it is
 * the only way back to the answer, no letter carries it, and an address on
 * screen can be kept and opened on another device.
 */
const router = useRouter();
const trackingAddress = computed(() =>
  trackingToken.value
    ? permalinkOrigin() +
      router.resolve({
        name: "support-track",
        params: { token: trackingToken.value },
      }).href
    : "",
);

/**
 * A word about a field goes when the field changes, and so does the refusal
 * over the button: the reader is answering it. Watched on the values and not
 * on the input events, because the editor also emits when it is only unlocked
 * after the request, and that took the refusal away the moment it arrived.
 */
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
// A word about the link or the violator was about the reason it was asked for.
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
  <div
    v-if="sent"
    ref="confirmation"
    class="ticket-sent"
    role="status"
    tabindex="-1"
  >
    <p class="ticket-sent-title">{{ copy.sent }}</p>

    <!-- The guest's one way back to the answer: the address of the ticket's
         page, an ordinary link in the sentence that says what it is. -->
    <p v-if="trackingToken" class="ticket-sent-text">
      Ответ появится на странице обращения:
      <router-link
        class="ticket-sent-address"
        :to="{ name: 'support-track', params: { token: trackingToken } }"
        >{{ trackingAddress }}</router-link
      >. Сохраните ссылку, чтобы вернуться к обращению.
    </p>

    <!-- Moderation answers in the conversation of the ticket, on its own
         page, so the card leads there. -->
    <p v-else-if="ticketId" class="ticket-sent-text">
      Ответ появится на
      <router-link :to="{ name: 'my-ticket', params: { ticketId } }"
        >странице обращения</router-link
      >
    </p>

    <p v-else class="ticket-sent-text">
      Ответ появится в разделе "<router-link :to="{ name: 'my-tickets' }"
        >Мои обращения</router-link
      >"
    </p>

    <p class="ticket-sent-again">
      <button type="button" class="again-button" @click="reset">
        {{ copy.again }}
      </button>
    </p>
  </div>

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
      <!-- The note stands where the hints of the fields stand, in their size:
           the lead line in the colour of the text says when to write here,
           the grey lines after it what can be done without the form. A reason
           filed from an account puts the sign-in line between the two. -->
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
            }}<router-link v-if="noteLine.link" :to="noteLine.link.to">{{
              noteLine.link.text
            }}</router-link
            >{{ noteLine.after ?? "" }}
          </p>
        </div>
      </template>
    </form-field>

    <template v-if="showFields">
      <form-field
        v-if="isGuest"
        label="Контактные данные"
        name="contact"
        :errors="contactError ? [contactError] : []"
      >
        <template #hint>Укажите почту</template>
        <input
          id="contact"
          v-model="contact"
          type="email"
          maxlength="200"
          autocomplete="email"
          :disabled="loading"
        />
      </form-field>

      <!-- The guest's field, holding the name instead of an address. Read-only
           and not disabled: the value is read out with its caption, selected
           and copied like any other, and only typing is refused. The ground
           under it is drawn by a layer of its own, as an <input> draws no
           pseudo-element. -->
      <form-field v-else label="Контактные данные" name="sender">
        <span class="sender-plate">
          <span class="sender-ground" aria-hidden="true" />
          <input id="sender" type="text" :value="username" readonly />
        </span>
      </form-field>

      <!-- Who broke the rules, by name: the server checks the name and writes
           it into the text of the ticket as a line of it. -->
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
          maxlength="20"
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
          maxlength="500"
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
        <!-- What to put into the text stands over the editor, where it is
             read before the writing starts, and not under the counters. -->
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

      <input
        v-model="honeypot"
        name="website"
        class="honeypot-field"
        autocomplete="off"
        tabindex="-1"
        aria-hidden="true"
      />

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
@use "@/assets/styles/Inputs" as *

// The form stands on the page itself, as the old site's did: no panel around
// it, the fields and the editor across the column, the button under them.
.ticket-form
  display: flex
  flex-direction: column
  gap: $medium

  // The fields keep their own labels and hints; the column gap spaces them,
  // one step wider than a field's own, so each label reads with its field.
  :deep(.form-field__labeled)
    margin: 0

// The author's name in the field the guest types his address into. The field
// of a page in size and place, and the settled value of the registration
// dialog in look (AuthSurface.sass, .auth-value): the fill and the frame of a
// control at half strength with the disabled hatch over them, and the name on
// top at full strength, because it is the thing shown. The ground is the
// site's disabled state drawn on a layer and not on the field, so the name is
// not dimmed with it.
.sender-plate
  position: relative
  // The ground stands behind the field and in front of nothing else.
  z-index: 0
  display: block
  border-radius: $border-radius

.sender-ground
  +disabled-hatch("")

  &
    position: absolute
    inset: 0
    z-index: -1
    pointer-events: none
    box-sizing: border-box
    border-radius: inherit
    background-color: $control-fill-overlay
    border: 1px solid $control-border-overlay

.sender-plate input
  display: block
  background-color: transparent
  border-color: transparent
  // The pointer and the caret of a value that is shown, not of one to type.
  cursor: default
  caret-color: transparent

  // The keyboard still stops here and sees where it stands: the field's own
  // ring, the frame turned $link.
  &:focus
    border-color: $link

.reason-hint
  margin: 0 0 $minor
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4

// The note under the reason, in the hint's size and grey (the field draws
// that), its lead line in the colour of the text. A link in it opens a window
// over the page or leads to the one page that answers the question, bold as
// the links of a lead are.
.reason-note
  line-height: 1.45

.reason-note-line
  margin: 0

  & + &
    margin-top: $minor

  &.is-lead
    color: $text

  a
    color: $link
    font-weight: 700
    text-decoration: none

    &:hover
      color: $link-hover
      text-decoration: underline

.ticket-refusal
  margin: 0
  color: $accent-red
  font-size: $secondary-font-size

.ticket-actions
  display: flex

.ticket-sent
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small

  // Focused by the script when it replaces the form, so the next Tab starts
  // from the answer. A mouse press brings no ring; a keyboard one keeps it.
  &:focus:not(:focus-visible)
    outline: none

  // The way on from here, bold as the links of a lead are, so the address and
  // the section read as the thing to take away and not as more of the text.
  a
    color: $link
    font-weight: 700
    text-decoration: none

    &:hover
      color: $link-hover
      text-decoration: underline

.ticket-sent-title
  margin: 0
  font-weight: 700

.ticket-sent-text,
.ticket-sent-again
  margin: 0

// The address is one word of eighty letters: it breaks where the column ends.
.ticket-sent-address
  overflow-wrap: anywhere

.again-button
  +inline-link-button
</style>
