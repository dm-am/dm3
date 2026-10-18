<script setup lang="ts">
/**
 * "Создание предупреждения" (product doc 4.2.4.1): the window a moderator
 * issues a warning from, opened under a topic, a comment, a message of the
 * global chat or on a profile.
 *
 * It is drawn as the other moderation windows are (BanDialog,
 * ChangeWarningPointsDialog): the site's heading, the lines that say whom the
 * decision is about, the fields, the two buttons. Whom, as the profile says
 * it: "Нарушитель:" with the name as a link, and "Нарушения: N/6", the points
 * he carries now, in the profile's own line (ProfilePage.vue,
 * ProfileViolations.vue), red while he carries any. The profile reds the same
 * line under an active ban too, but there the line of the ban stands right
 * under it and says why; the window has no line of the ban, so its red stands
 * for points alone. What for, as the message itself: its text whole,
 * rendered, set as a [quote] of the site is, and under it in the corner its
 * number linking to its place in the discussion, where it has one, as under
 * a comment (CommentItem.vue). The address is never written out as text.
 *
 * One field for the weight, "Баллы", from 0 to 6, as the window that changes
 * the points has it; 0 is the verbal warning and says so.
 *
 * The text comes from the opener where it holds it (the comment lists); for a
 * topic and a chat message the window asks for it by the id it is handed, so
 * those openers say no more than they did. The violation itself is sent as
 * entityId/entityType, and the server takes its own copy of the text.
 *
 * Validation mirrors the backend CreateWarningValidator: points 0-6, reason
 * required and max 2000 chars.
 *
 * The window runs the request itself (Dialog's `action`): it waits shut, the
 * editor included, prints a refusal over its buttons or under the field the
 * server named, and closes on success alone. A typed reason is asked about
 * before it is dropped.
 */
import { ref, computed, nextTick, onMounted } from "vue";
import { storeToRefs } from "pinia";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { StatLine } from "@/shared/ui/StatLine";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  initBbcodeInteractive,
  trimHtmlWhitespace,
} from "@/shared/lib/utils/bbcodeInteractive";
import { useUiStore } from "@/shared/stores/ui";
import moderationActionsApi, { type WarningResult } from "../api";

const props = defineProps<{
  /** Target user (violator) username. */
  username: string;
  /** Id of the violating entity (comment, topic, message...). */
  entityId?: string;
  /** Entity type name for the backend ("Comment", "Topic", "Message"...). */
  entityType?: string;
  /** The address of the violating message: where its number leads. */
  entityLink?: string;
  /**
   * The text of the violating message as the site renders it, handed over by
   * an opener that holds it. Without it the window asks for a topic's and a
   * chat message's text itself.
   */
  evidence?: string;
  /** The number of the message in its discussion, where it has one. */
  number?: number;
  /**
   * Set by an opener whose own screen shows the issued warning, so that the
   * dialog does not repeat it with a toast (UI_STANDARDS, "Всплывающие
   * сообщения": a result the reader can see needs no confirmation).
   *
   * The profile moderation block is that opener: behind the closing dialog its
   * "Предупреждения: N" counter and the active-points bracket are refetched,
   * and the open list of warnings picks up the new row. A topic, a chat
   * message and a comment list have nowhere to put a warning — it is a record
   * in the violator's profile, not on the page being moderated — so there the
   * toast is the only answer there is and stays.
   */
  hideToast?: boolean;
}>();

const emit = defineEmits<{
  (e: "success", warning: WarningResult | null): void;
  (e: "cancel"): void;
}>();

const toast = useToast();
const { hyphenateWords } = storeToRefs(useUiStore());

/** The scale the warning counts against, as the profile writes it ("N/6"). */
const WARNING_LIMIT = 6;

/** The whole scale; zero is the verbal warning, and its option says so. */
const pointsOptions: SelectOption[] = Array.from(
  { length: WARNING_LIMIT + 1 },
  (_, value) => ({
    value: String(value),
    label: value === 0 ? "0 (устное предупреждение)" : String(value),
  }),
);

const points = ref("1");
const reason = ref("");

// Mirror of the backend rules (points 0-6, reason required + max 2000).
const reasonTooLong = computed(() => reason.value.length > 2000);
const canSubmit = computed(
  () => reason.value.trim().length > 0 && !reasonTooLong.value,
);

// --- "Нарушения: N/6", the points the person carries now ---
const currentPoints = ref<number | null>(null);
const pointsFailed = ref(false);

// --- The message the warning is for ---
const quoted = ref(trimHtmlWhitespace(props.evidence));
const quoteRef = ref<HTMLElement | null>(null);

/**
 * The window is asking for the text of the message. The quote stands above
 * the fields, so the fields wait for it: drawn before it, they would move down
 * under the moderator's hand when it lands. Whatever the answer, the fields
 * come with it, the quote only where there was a text.
 */
const quoteLoading = ref(!quoted.value && !!props.entityId);

/** Spoilers and the 18+ veil open in the quote as they do on the page. */
async function bindQuote() {
  await nextTick();
  if (quoteRef.value) initBbcodeInteractive(quoteRef.value);
}

onMounted(async () => {
  if (quoted.value) {
    void bindQuote();
  } else if (quoteLoading.value) {
    void moderationActionsApi
      .getQuotedText(props.entityType, props.entityId)
      .then((text) => {
        quoted.value = trimHtmlWhitespace(text);
        if (quoted.value) void bindQuote();
      })
      .finally(() => (quoteLoading.value = false));
  }

  const { data } = await moderationActionsApi.getUserWarnings(props.username);
  if (data) currentPoints.value = data.totalPoints;
  else pointsFailed.value = true;
});

/**
 * The window's action. A form that breaks the rules above never gets here:
 * the main button is shut by `canSubmit`, and a shut button submits nothing.
 */
async function send() {
  const result = await moderationActionsApi.createWarning(
    {
      username: props.username,
      points: Number(points.value),
      reason: reason.value.trim(),
      entityId: props.entityId,
      entityType: props.entityType,
    },
    { ownsRefusal: true, ownsRateLimit: true },
  );
  if (!result.error) {
    // Silence is only safe where the opener answers for the warning itself.
    if (!props.hideToast) toast.success("Предупреждение выдано");
    emit("success", result.data?.resource ?? null);
  }
  return result;
}
</script>

<template>
  <Dialog
    v-slot="{ busy }"
    editor
    :action="send"
    failure="Не удалось выдать предупреждение"
    :dirty="reason.trim().length > 0"
    discard-text="Набранная причина пропадет"
    @close="emit('cancel')"
  >
    <DialogTitle>Создание предупреждения</DialogTitle>

    <div class="warn-context">
      <!-- Whom: the lines of the profile, the name a link to it and the
           points red while he carries any. The second line keeps its height
           while the points load, so nothing under it moves. -->
      <div class="warn-person">
        <StatLine
          label="Нарушитель"
          :value="username"
          :to="{ name: 'profile', params: { username } }"
        />
        <div class="warn-points">
          <StatLine
            v-if="currentPoints !== null"
            label="Нарушения"
            :value="`${currentPoints}/${WARNING_LIMIT}`"
            :variant="currentPoints > 0 ? 'negative' : 'default'"
          />
          <StatLine
            v-else-if="pointsFailed"
            label="Нарушения"
            value="не удалось загрузить"
            variant="muted"
          />
        </div>
      </div>

      <!-- What for: the message whole, as a [quote], and its number in the
           corner under it, a link to its place as under a comment. -->
      <div v-if="quoted" class="warn-evidence">
        <div
          ref="quoteRef"
          class="warn-quote bbcode-content"
          :class="{ hyphenated: hyphenateWords }"
        >
          <div class="quote" v-html="quoted" />
        </div>
        <div v-if="number && entityLink" class="warn-evidence-footer">
          <a
            :href="entityLink"
            target="_blank"
            rel="noopener"
            class="warn-number"
            :aria-label="`Комментарий ${number}`"
            >{{ number }}</a
          >
        </div>
      </div>
    </div>

    <Form
      v-if="!quoteLoading"
      :valid="canSubmit"
      action="Выдать"
      cancel="Отмена"
    >
      <FormField label="Баллы" name="warning-points" field="points">
        <Select
          id="warning-points"
          :model-value="points"
          :options="pointsOptions"
          @update:model-value="(v) => (points = v)"
        />
      </FormField>

      <FormField
        label="Причина предупреждения"
        name="warning-reason"
        field="reason"
        :errors="reasonTooLong ? ['Слишком длинное значение'] : []"
      >
        <BBCodeEditor
          v-model="reason"
          context="common"
          placeholder="Опишите нарушение..."
          :disabled="busy"
          :min-height="100"
          :max-height="300"
          :is-moderator="true"
        />
      </FormField>
    </Form>
  </Dialog>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/BbcodeContent" as *

// One field step down to the first field, as between two fields, and the
// same step between whom and what for.
.warn-context
  display: flex
  flex-direction: column
  gap: $medium
  margin-bottom: $medium

// The lines of the profile (ProfilePage.vue, .stats-group): one under another
// with the profile's step between them.
.warn-person
  display: flex
  flex-direction: column
  gap: $minor

// The height of one line held while the points load (ProfileViolations.vue,
// .violations-inline).
.warn-points
  min-height: calc($font-size * 1.25)

// The text of a comment (CommentItem.vue, .comment-text, +body-text): DM2's
// 16 on a step of 20, flush on both sides, broken at syllables while the
// switch "Перенос слов" is on. The quote itself is the site's [quote]
// (_BbcodeContent.sass).
.warn-quote
  color: $text
  +body-text

  > .quote
    margin: 0

// The footer of a comment: the number pinned to its right corner, a small
// step under the text (CommentItem.vue, .comment-footer, .comment-number).
.warn-evidence-footer
  display: flex
  margin-top: $small

.warn-number
  flex: none
  margin-left: auto
  font-size: $tertiary-font-size
  +muted-link
</style>
