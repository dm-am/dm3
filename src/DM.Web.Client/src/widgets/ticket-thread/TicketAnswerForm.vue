<script setup lang="ts">
/**
 * The form an answer of a ticket is written in, under its conversation: the
 * form of a comment of a topic (CommentComposer), the editor across the column
 * and "Отправить" under it at the left, with a ticket's words and, for
 * moderation, the status the answer moves the ticket to.
 *
 * Moderation chooses the status over the editor, as a field of the form it
 * sends: "Ожидает ответа пользователя" unless it chooses to close the ticket or
 * to mark it as spam. The text and the status go in one request and land
 * together or not at all, so no closing stands without the word that came
 * with it. Closing needs no word: with "Закрыто" or "Спам" chosen the form
 * sends an empty field, and the status is the whole answer. The author and
 * the guest choose nothing: their answer always hands the ticket to
 * moderation, and the server refuses a status from them.
 *
 * The choice is a list of the site's forms (Select under a FormField caption),
 * as the forms of moderation choose a value (BanDialog, WarningDialog). It is
 * no filter: it changes nothing on the page until the answer is sent.
 *
 * Its own form and not the comment's, for the words: a ticket holds answers,
 * not comments, and the field and the refusal say so.
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import Button from "@/shared/ui/Button/Button.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { useAuthStore, userIsModerator } from "@/entities/user";
import {
  TICKET_STATUS_LABELS,
  TICKET_TEXT_MAX_LENGTH,
  type AnswerStatus,
} from "@/entities/ticket";

/** What a mutation answers with: null when the server took the change. */
type SubmitResult = { error: GeneralError | null };

const props = defineProps<{
  /**
   * Posts the answer; the result decides whether the draft is dropped. The
   * status is moderation's alone and is left out for anybody else.
   */
  send: (text: string, status?: AnswerStatus) => Promise<SubmitResult>;
  /** The key the editor keeps the unsent draft under. */
  draftKey: string;
  /** Whether the reader answers as moderation and chooses the status. */
  choosesStatus: boolean;
}>();

/** What an answer of moderation leaves the ticket in, the first one chosen. */
const DEFAULT_STATUS: AnswerStatus = "WaitingForUser";

const STATUSES: AnswerStatus[] = ["WaitingForUser", "Closed", "Spam"];

const statusOptions: SelectOption[] = STATUSES.map((value) => ({
  value,
  label: TICKET_STATUS_LABELS[value],
}));

const { user } = storeToRefs(useAuthStore());
const isModerator = computed(() => userIsModerator(user.value));

const text = ref("");
const status = ref<AnswerStatus>(DEFAULT_STATUS);
const sending = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

/** A refused answer, under the "Отправить" that sent it. */
const sendResult = useActionResult();

/** Closing a ticket or marking it as spam needs no word. */
const endsTicket = computed(
  () => props.choosesStatus && status.value !== "WaitingForUser",
);

const canSend = computed(() => !!text.value.trim() || endsTicket.value);

async function handleSend() {
  if (!canSend.value || sending.value) return;
  const written = text.value;
  const chosen = props.choosesStatus ? status.value : undefined;

  // Emptied before the request, so that sending feels instant, and given back
  // when it is refused. The editor's own clear() waits for the answer: it also
  // drops the saved draft, the copy that outlives the tab.
  text.value = "";
  sending.value = true;
  sendResult.clear();
  const { error } = await props.send(written.trim() ? written : "", chosen);
  sending.value = false;

  if (error) {
    text.value = written;
    sendResult.refuse(error, "Не удалось отправить ответ");
    return;
  }
  editorRef.value?.clear();
  status.value = DEFAULT_STATUS;
}
</script>

<template>
  <div class="ticket-answer-form">
    <FormField v-if="choosesStatus" label="Статус" name="ticket-answer-status">
      <Select
        id="ticket-answer-status"
        :model-value="status"
        :options="statusOptions"
        :disabled="sending"
        @update:model-value="(value) => (status = value as AnswerStatus)"
      />
    </FormField>
    <BBCodeEditor
      ref="editorRef"
      v-model="text"
      context="common"
      placeholder="Написать ответ..."
      :draft-key="draftKey"
      :disabled="sending"
      :min-height="100"
      :max-height="300"
      :max-length="TICKET_TEXT_MAX_LENGTH"
      :resizable="true"
      :is-moderator="isModerator"
      @submit="handleSend"
    />
    <Button
      variant="primary"
      :loading="sending"
      :disabled="!canSend"
      @click="handleSend"
    >
      Отправить
    </Button>
    <ActionResult :result="sendResult" class="ticket-answer-form__refusal" />
  </div>
</template>

<style scoped lang="sass">
// The form of a comment (CommentComposer): the editor across the column,
// "Отправить" under it at its own width and at the left, a small step
// between them. The caption of the status and its list stand over the editor
// on the same step: the field keeps no margin of its own here.
.ticket-answer-form
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  width: 100%

  // Spaced by the gap of the column.
  .ticket-answer-form__refusal
    --action-result-margin: 0

  :deep(.form-field__labeled)
    align-self: stretch
    margin: 0

  :deep(.bbcode-editor-wrapper)
    width: 100%
</style>
