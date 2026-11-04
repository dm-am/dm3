<script setup lang="ts">
// Nothing is uploaded here: the host uploads after sending, once the post has an id.
import { computed } from "vue";
import type { UploadAudience } from "@/shared/api/models/common/upload";
import { ActionResult } from "@/shared/ui/ActionResult";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import {
  MAX_POST_ATTACHMENTS,
  MAX_POST_ATTACHMENT_BYTES,
  POST_ATTACHMENT_ACCEPT,
  pickAttachments,
} from "../model/postAttachments";
import {
  everyReader,
  type AudienceCandidate,
  type PendingAttachment,
} from "../model/attachmentAudience";
import AttachmentLine from "./AttachmentLine.vue";

const files = defineModel<PendingAttachment[]>({ required: true });
/** The host clears it when it sends. */
const error = defineModel<string | null>("error", { required: true });

defineProps<{
  disabled: boolean;
  /** The characters of the room a file can be shown to. */
  candidates: readonly AudienceCandidate[];
}>();

const full = computed(() => files.value.length >= MAX_POST_ATTACHMENTS);

// Whole megabytes, as the hint has always said it: "5 МБ", not "5,0 МБ".
const MAX_MEGABYTES = MAX_POST_ATTACHMENT_BYTES / (1024 * 1024);

function choose(event: Event) {
  const input = event.target as HTMLInputElement;
  const picked = pickAttachments(
    Array.from(input.files ?? []),
    files.value.length,
  );
  files.value = [
    ...files.value,
    ...picked.accepted.map((file) => ({ file, audience: everyReader() })),
  ];
  error.value = picked.error;

  // The picker keeps the chosen file selected, so the same pick again would fire no change.
  input.value = "";
}

function remove(index: number) {
  files.value = files.value.filter((_, i) => i !== index);
  error.value = null;
}

function mark(index: number, audience: UploadAudience) {
  files.value = files.value.map((one, i) =>
    i === index ? { ...one, audience } : one,
  );
}
</script>

<template>
  <div class="attachments">
    <div class="attachments-title">Вложения:</div>
    <ul v-if="files.length" class="attachment-list">
      <AttachmentLine
        v-for="(one, i) in files"
        :key="i"
        :name="one.file.name"
        :size-bytes="one.file.size"
        :audience="one.audience"
        :candidates="candidates"
        :disabled="disabled"
        @mark="(audience) => mark(i, audience)"
        @remove="remove(i)"
      />
    </ul>
    <label class="attachment-add">
      <input
        type="file"
        class="attachment-input"
        :accept="POST_ATTACHMENT_ACCEPT"
        :disabled="disabled || full"
        multiple
        @change="choose"
      />
      <span class="attachment-add-label">Прикрепить файл</span>
    </label>
    <SecondaryText class="attachment-hint">
      Не больше {{ MAX_POST_ATTACHMENTS }} картинок, до {{ MAX_MEGABYTES }} МБ
      каждая
    </SecondaryText>
    <ActionResult :text="error" />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Buttons" as *

.attachments
  margin-top: $medium

.attachments-title
  margin-bottom: $small
  +field-caption

// A file a line, as in the strip of the post, each as wide as its words: the
// panel under it takes the line's width, between a dropdown's and the editor's.
.attachment-list
  list-style: none
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $tiny
  margin: 0 0 $small

.attachment-add
  position: relative
  display: inline-flex

.attachment-input
  position: absolute
  inset: 0
  width: 100%
  opacity: 0
  cursor: pointer

  &:disabled
    cursor: default

.attachment-add-label
  +button

.attachment-hint
  display: block
  margin-top: $tiny
</style>
