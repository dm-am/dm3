<script setup lang="ts">
import type { PostAttachment } from "@/entities/game";
import { isImageAttachment } from "../model/postAttachments";
import { apiUrl } from "@/shared/api/client";
import { vBbcodeInteractive } from "@/shared/directives";
import { formatFileSize } from "@/shared/lib/utils/fileSize";
import { openImageViewer } from "@/shared/lib/composables/useImageViewer";
import { CaptionLine } from "@/shared/ui/CaptionLine";

const props = defineProps<{ file: PostAttachment }>();

const href = apiUrl(props.file.url);

// A picture opens in the viewer; a modifier click and other files keep the browser's meaning.
function openName(event: MouseEvent) {
  if (!isImageAttachment(props.file)) return;
  if (
    event.button !== 0 ||
    event.ctrlKey ||
    event.metaKey ||
    event.shiftKey ||
    event.altKey
  )
    return;
  event.preventDefault();
  openImageViewer(href);
}
</script>

<template>
  <!-- A file kept to some readers is marked as a hidden block: green, its readers in the tooltip -->
  <li class="attachment-row" v-bbcode-interactive="file.recipients ?? ''">
    <a
      class="attachment-name"
      :class="{ 'private-message': !!file.recipients }"
      :data-recipients="file.recipients"
      :href="href"
      target="_blank"
      rel="noopener"
      @click="openName"
      >{{ file.fileName }}</a
    >{{ " "
    }}<CaptionLine class="attachment-caption"
      ><span class="attachment-size">{{
        formatFileSize(file.sizeBytes)
      }}</span></CaptionLine
    >
  </li>
</template>

<style scoped lang="sass">
.attachment-row
  margin: 0

// The text colour: the strip is part of what it is attached to.
.attachment-name
  color: $text
  text-decoration: none
  overflow-wrap: anywhere

  &:hover
    color: $text
    text-decoration: underline

  &.private-message,
  &.private-message:hover
    color: $text-green

.attachment-caption
  display: inline-block
</style>
