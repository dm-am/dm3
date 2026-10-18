<script setup lang="ts">
/**
 * The tooltip of a tag as a reader will see it, over the name of the tag,
 * drawn while the moderator types its description.
 *
 * The bubble is the site's own (TooltipBubble), standing in the flow of the
 * window and open: a tooltip carried to <body> would cover the window's own
 * panels and live by a position of its own as the window scrolls. The words in
 * it are the markup the server will render, drawn by hintToHtml, the preview
 * the one exception BBCODE_RENDERING.md makes for the editor's converter
 * holds for too, and they work as a reader's do: a word under a dotted line
 * opens its small tooltip on hover, on a tap and on a focus from the keyboard.
 * A description left empty shows the name of the tag, as the tooltip does.
 */
import { computed } from "vue";
import TooltipBubble from "@/shared/ui/Tooltip/TooltipBubble.vue";
import { TooltipContent } from "@/shared/ui/Tooltip";
import { hintToHtml } from "@/shared/lib/utils/bbcode";

const props = defineProps<{
  /** The description as typed. */
  description: string;
  /** The name of the tag, under the bubble. */
  title: string;
}>();

const html = computed(() => hintToHtml(props.description));
const name = computed(() => props.title.trim() || "Тег");
</script>

<template>
  <div class="hint-preview">
    <TooltipBubble inline rich :anchor="() => null">
      <TooltipContent :html="html" :text="name" />
    </TooltipBubble>
    <span class="hint-preview-tag">{{ name }}</span>
  </div>
</template>

<style scoped lang="sass">
// A part of the window a shade darker, as the band of the markup check: the
// preview is a picture of the page, set off from the fields around it.
.hint-preview
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  padding: $medium
  background-color: $control-bg-overlay

// The name of the tag as a reader meets it in the cloud and in the list of
// games, a link under the bubble.
.hint-preview-tag
  color: $link
  text-decoration: underline
</style>
