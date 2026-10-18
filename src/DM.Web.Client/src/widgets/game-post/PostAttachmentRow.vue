<script setup lang="ts">
/**
 * One file of a post, as a row of the strip the paper clip opens: the name,
 * a link that opens the file, then its size as the grey caption of the row.
 * The name is the link to copy: no button of its own does it.
 */
import type { PostAttachment } from "@/entities/game";
import { isImageAttachment } from "@/features/upload";
import { apiUrl } from "@/shared/api/client";
import { formatFileSize } from "@/shared/lib/utils/fileSize";
import { openImageViewer } from "@/shared/lib/composables/useImageViewer";
import { CaptionLine } from "@/shared/ui/CaptionLine";

const props = defineProps<{ file: PostAttachment }>();

// The payload carries the API path and the origin is the client's to add. The
// endpoint behind it re-decides who may read the bytes on every request, so the
// address grants nothing on its own, copied or not.
const href = apiUrl(props.file.url);

/**
 * The name of a picture opens it over the page, in the viewer a picture of the
 * text opens in, instead of in a tab of its own. A click with a modifier keeps
 * the browser's meaning (a tab, a window, a download), and so does a file of
 * any other kind: the viewer shows pictures only.
 */
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
  <li class="attachment-row">
    <a
      class="attachment-name"
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

// The name is the post's own text, not a link colour: the strip is part of the
// post, and the pointer's underline is what says it opens.
.attachment-name
  color: $text
  text-decoration: none
  overflow-wrap: anywhere

  &:hover
    color: $text
    text-decoration: underline

// The size goes with the name, and moves to the next line whole when it does
// not fit after it.
.attachment-caption
  display: inline-block
</style>
