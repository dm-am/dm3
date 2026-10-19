<script setup lang="ts">
/** The answer form of a ticket: a comment's form in a ticket's words, the status for moderation. */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import type { SubmitResult } from "@/shared/api/models/common";
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

const props = defineProps<{
  /** Posts the answer; the status is moderation's alone. */
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

  // Emptied at once and given back on a refusal; clear() waits, since it drops the draft too.
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
// The form of a comment, the status field over the editor on the same step.
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
