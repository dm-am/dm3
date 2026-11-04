<script setup lang="ts">
// Shown or not by the caller; the counter stops where the server's limit does.
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import type { SubmitResult } from "@/shared/api/models/common";
import Button from "@/shared/ui/Button/Button.vue";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { BODY_TEXT_MAX_LENGTH } from "@/shared/lib/constants/content";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { useAuthStore, userIsModerator } from "@/entities/user";

const props = defineProps<{
  /** A function, not an event: the text goes back into the field on a refusal. */
  send: (text: string) => Promise<SubmitResult>;
  draftKey: string;
}>();

const { user } = storeToRefs(useAuthStore());
const isModerator = computed(() => userIsModerator(user.value));

const text = ref("");
const sending = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const sendResult = useActionResult();

async function handleSend(): Promise<void> {
  if (!text.value.trim() || sending.value) return;
  const written = text.value;
  text.value = "";
  sending.value = true;
  sendResult.clear();
  const { error } = await props.send(written);
  sending.value = false;

  // The field empties at once and gets the text back on failure; clear() also drops the draft.
  if (error) {
    text.value = written;
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
// Items at their own width: a stretched column drew the button 642 wide.
.comment-composer
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  width: 100%

  .comment-composer__refusal
    --action-result-margin: 0

  :deep(.bbcode-editor-wrapper)
    width: 100%
</style>
