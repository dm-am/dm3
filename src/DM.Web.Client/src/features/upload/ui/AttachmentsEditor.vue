<script setup lang="ts">
// Lines of a column the host lays out, so the block has no root of its own.
import type { PostAttachment } from "@/entities/game";
import { apiUrl } from "@/shared/api/client";
import type { UploadAudience } from "@/shared/api/models/common/upload";
import { ActionResult } from "@/shared/ui/ActionResult";
import { POST_ATTACHMENT_ACCEPT } from "../model/postAttachments";
import {
  attachmentCandidates,
  audienceOf,
  type AudienceCandidate,
  type PendingAttachment,
} from "../model/attachmentAudience";
import AttachmentLine from "./AttachmentLine.vue";

defineProps<{
  attachments: PostAttachment[];
  /** Chosen in this edit, sent by "Сохранить". */
  pending: PendingAttachment[];
  busy: boolean;
  /** The author's: only he attaches files and marks who sees them. */
  isAuthor: boolean;
  full: boolean;
  error: string | null;
  candidates: readonly AudienceCandidate[];
}>();

const emit = defineEmits<{
  choose: [files: File[]];
  unchoose: [index: number];
  markPending: [index: number, audience: UploadAudience];
  mark: [file: PostAttachment, audience: UploadAudience];
  remove: [file: PostAttachment];
}>();

function pick(event: Event) {
  const input = event.target as HTMLInputElement;
  const chosen = Array.from(input.files ?? []);
  // The picker keeps the chosen file selected, so the same pick again would fire no change.
  input.value = "";
  emit("choose", chosen);
}
</script>

<template>
  <ul v-if="attachments.length || pending.length" class="attachment-list">
    <AttachmentLine
      v-for="file in attachments"
      :key="file.id"
      :name="file.fileName"
      :size-bytes="file.sizeBytes"
      :href="apiUrl(file.url)"
      :audience="isAuthor ? audienceOf(file) : undefined"
      :recipients="file.recipients"
      :candidates="attachmentCandidates(candidates, file)"
      :disabled="busy"
      @mark="(audience) => emit('mark', file, audience)"
      @remove="emit('remove', file)"
    />
    <AttachmentLine
      v-for="(one, i) in pending"
      :key="`pending-${i}`"
      :name="one.file.name"
      :size-bytes="one.file.size"
      :audience="one.audience"
      :candidates="candidates"
      :disabled="busy"
      @mark="(audience) => emit('markPending', i, audience)"
      @remove="emit('unchoose', i)"
    />
  </ul>
  <label v-if="isAuthor" class="attachment-add">
    <input
      type="file"
      class="attachment-input"
      :accept="POST_ATTACHMENT_ACCEPT"
      :disabled="busy || full"
      multiple
      @change="pick"
    />
    <span class="attachment-add-label">Прикрепить файл</span>
  </label>
  <ActionResult :text="error" class="attachment-error" />
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *

// A file a line, as in the form of a new post.
.attachment-list
  list-style: none
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $tiny
  margin: 0

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

.attachment-error
  --action-result-margin: 0
</style>
