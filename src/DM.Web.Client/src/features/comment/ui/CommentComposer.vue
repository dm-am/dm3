<script setup lang="ts">
/**
 * The form a comment is written in: the editor across the column, "Отправить"
 * under it at the left, and the refusal of the last send in a line under the
 * button.
 *
 * The form of the discussions of the site (DiscussionSection); an answer of a
 * ticket has a form of its own in the same look (TicketAnswerForm). It does
 * not decide whether it is shown: the caller does, by what the server says
 * the reader may do.
 *
 * The counter of the editor counts to what the server takes for a comment: a
 * counter that ran past it would let the writer learn of the limit only from
 * the refusal.
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import Button from "@/shared/ui/Button/Button.vue";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { BODY_TEXT_MAX_LENGTH } from "@/shared/lib/constants/content";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { useAuthStore, userIsModerator } from "@/entities/user";

/** What a mutation answers with: null when the server took the change. */
type SubmitResult = { error: GeneralError | null };

const props = defineProps<{
  /**
   * Posts the comment; the answer decides whether the draft is dropped. A
   * function rather than an event, because only the answer may: the text
   * goes back into the field on a refusal.
   */
  send: (text: string) => Promise<SubmitResult>;
  /** The key the editor keeps the unsent draft under. */
  draftKey: string;
}>();

const { user } = storeToRefs(useAuthStore());
const isModerator = computed(() => userIsModerator(user.value));

const text = ref("");
const sending = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

/** A refused comment, under the "Отправить" that sent it. */
const sendResult = useActionResult();

/**
 * Sends what is in the field. A blank field sends nothing, and a press while
 * a send is on its way sends nothing again.
 */
async function handleSend(): Promise<void> {
  if (!text.value.trim() || sending.value) return;
  const written = text.value;
  text.value = "";
  sending.value = true;
  sendResult.clear();
  const { error } = await props.send(written);
  sending.value = false;

  // Give the text back on failure. Emptying the field before the request is
  // what makes sending feel instant; losing what was written when it fails is
  // not part of that bargain. The editor's own clear() waits for the send to
  // land — it also drops the saved draft, and that copy is the one that
  // outlives the tab.
  if (error) {
    text.value = written;
    // A 400 is the refusal a comment gets most: the server names what is
    // wrong with the text. Without this line the text came back into the
    // field and nothing said why; it stands under the button, not in a plate.
    sendResult.refuse(error, "Не удалось отправить комментарий");
    return;
  }
  editorRef.value?.clear();
}
</script>

<template>
  <div class="comment-composer">
    <BBCodeEditor
      ref="editorRef"
      v-model="text"
      context="common"
      placeholder="Написать комментарий..."
      :draft-key="draftKey"
      :disabled="sending"
      :min-height="100"
      :max-height="300"
      :max-length="BODY_TEXT_MAX_LENGTH"
      :resizable="true"
      :is-moderator="isModerator"
      @submit="handleSend"
    />
    <Button
      variant="primary"
      :loading="sending"
      :disabled="!text.trim()"
      @click="handleSend"
    >
      Отправить
    </Button>
    <ActionResult :result="sendResult" class="comment-composer__refusal" />
  </div>
</template>

<style scoped lang="sass">
// The editor across the column, "Отправить" under it at its own width and at
// the left, as in the chat, the messages and the room: a column that
// stretched its items drew the button 642 wide.
.comment-composer
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  width: 100%

  // Spaced by the gap of the column.
  .comment-composer__refusal
    --action-result-margin: 0

  :deep(.bbcode-editor-wrapper)
    width: 100%
</style>
