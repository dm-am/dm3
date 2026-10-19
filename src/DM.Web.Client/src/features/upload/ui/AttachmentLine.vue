<script setup lang="ts">
/**
 * A file of a post in a form, one line as in the post's strip: "карта.png
 * 70 Б | видят все | убрать". The word of who sees it opens the panel under
 * the line; the host decides when a new mark goes to the server.
 */
import { computed, ref, useTemplateRef } from "vue";
import type { UploadAudience } from "@/shared/api/models/common/upload";
import { vBbcodeInteractive } from "@/shared/directives";
import { formatFileSize } from "@/shared/lib/utils/fileSize";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import {
  audienceLabel,
  audienceRecipients,
  type AudienceCandidate,
} from "../model/attachmentAudience";
import AttachmentAudiencePanel from "./AttachmentAudiencePanel.vue";

const props = defineProps<{
  name: string;
  sizeBytes: number;
  /** The file's address once it is attached; a file still to be sent has none. */
  href?: string;
  /** Who sees it; absent where the reader may not change it (not the author's). */
  audience?: UploadAudience;
  /** The server's "Получатели:" of an attached file; a file to be sent has its own from the boxes. */
  recipients?: string;
  candidates: readonly AudienceCandidate[];
  disabled: boolean;
}>();

const emit = defineEmits<{ mark: [audience: UploadAudience]; remove: [] }>();

const open = ref(false);
const toggle = useTemplateRef<{ $el: HTMLElement }>("toggle");

// The mark on the name is the hidden block's: green, its readers in the tooltip.
const shownRecipients = computed(() => {
  if (props.recipients) return props.recipients;
  const audience = props.audience;
  return audience?.visibleTo === "Selected"
    ? audienceRecipients(props.candidates, audience.characterIds)
    : undefined;
});

// Nobody to check and nothing to undo: the word says it and opens nothing.
const changeable = computed(
  () =>
    !!props.audience &&
    (props.candidates.length > 0 || props.audience.visibleTo === "Selected"),
);

const label = computed(() =>
  props.audience ? audienceLabel(props.candidates, props.audience) : "",
);
</script>

<template>
  <li class="attachment-line" v-bbcode-interactive="shownRecipients ?? ''">
    <a
      v-if="href"
      class="attachment-name"
      :class="{ 'private-message': !!shownRecipients }"
      :data-recipients="shownRecipients"
      :href="href"
      target="_blank"
      rel="noopener"
      >{{ name }}</a
    ><span
      v-else
      class="attachment-name"
      :class="{ 'private-message': !!shownRecipients }"
      :data-recipients="shownRecipients"
      >{{ name }}</span
    >{{ " "
    }}<CaptionLine class="attachment-caption"
      ><span>{{ formatFileSize(sizeBytes) }}</span
      ><CaptionAction
        v-if="changeable"
        ref="toggle"
        class="attachment-audience"
        :aria-expanded="open"
        :disabled="disabled"
        @click="open = !open"
        >{{ label }}</CaptionAction
      ><span v-else-if="audience">{{ label }}</span
      ><CaptionAction
        class="attachment-remove"
        aria-label="Убрать вложение"
        :disabled="disabled"
        @click="emit('remove')"
        >убрать</CaptionAction
      ></CaptionLine
    >
    <AttachmentAudiencePanel
      v-if="open && audience"
      :file-name="name"
      :audience="audience"
      :candidates="candidates"
      :anchor="toggle?.$el ?? null"
      @done="(chosen) => emit('mark', chosen)"
      @close="open = false"
    />
  </li>
</template>

<style scoped lang="sass">
// The panel hangs from the line.
.attachment-line
  position: relative
  margin: 0

// The text colour of the strip's name, the hidden block's green when kept to some.
.attachment-name
  color: $text
  text-decoration: none
  overflow-wrap: anywhere

  &.private-message
    color: $text-green

a.attachment-name:hover
  text-decoration: underline

.attachment-caption
  display: inline-block

// Open, the word keeps the look of the pointer on it.
.attachment-audience[aria-expanded="true"]
  color: $link-hover
  text-decoration: underline
</style>
