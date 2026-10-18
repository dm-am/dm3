<script setup lang="ts">
/**
 * ChatComposer: the editor under the frame of a chat and its "Отправить",
 * one and the same in the global chat and in a correspondence.
 *
 * It stands only where the reader may write (ChatFeed decides with its
 * `canWrite`): a guest, or a reader outside a closed event, has no field at
 * all rather than a field with a line above it saying the message will be
 * refused (owner, 5 October 2026, ACC-364). The server refuses all the same.
 *
 * The send itself is ChatFeed's (useChatComposer); here is what is drawn: the
 * field, its hint, the button, and the refused send under it with the text
 * back in the field.
 */
import { ref } from "vue";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { ActionResult } from "@/shared/ui/ActionResult";
import type { BBCodeContext } from "@/shared/lib/utils/bbcode";
import type { ActionResult as SendResult } from "@/shared/lib/composables/useActionResult";
import { BODY_TEXT_MAX_LENGTH } from "@/shared/lib/constants/content";

defineProps<{
  /** The tag set: "direct" in a correspondence, which renders no [mod]. */
  context: BBCodeContext;
  /** Where the unsent text outlives the tab. */
  draftKey: string;
  /** A send is in flight, or there is nowhere to send to yet. */
  disabled: boolean;
  /** Offers the moderator's tags. */
  isModerator: boolean;
  /** The refused send, said under the button. */
  result: SendResult;
}>();

const text = defineModel<string>({ required: true });

const emit = defineEmits<{ submit: [] }>();

const editor = ref<InstanceType<typeof BBCodeEditor> | null>(null);

defineExpose({
  /** Empties the field and drops its draft, once the send has landed. */
  clear: () => editor.value?.clear(),
});
</script>

<template>
  <div class="chat-composer">
    <BBCodeEditor
      ref="editor"
      v-model="text"
      :context="context"
      placeholder="Написать сообщение..."
      :draft-key="draftKey"
      :disabled="disabled"
      :min-height="60"
      :max-height="200"
      :max-length="BODY_TEXT_MAX_LENGTH"
      :resizable="true"
      :is-moderator="isModerator"
      @submit="emit('submit')"
    />
    <button
      type="button"
      class="chat-composer__send"
      :disabled="disabled || !text.trim()"
      @click="emit('submit')"
    >
      Отправить
    </button>
    <ActionResult :result="result" class="chat-composer__refusal" />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.chat-composer
  display: flex
  flex-direction: column
  gap: $small
  width: 100%

  :deep(.bbcode-editor-wrapper)
    width: 100%

// The main action of the composer: the one filled button of the form.
.chat-composer__send
  align-self: flex-start
  +button
  &
    +button-primary

// Spaced by the gap of the column.
.chat-composer__refusal
  --action-result-margin: 0
</style>
