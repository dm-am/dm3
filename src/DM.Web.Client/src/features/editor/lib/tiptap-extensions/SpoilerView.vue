<script setup lang="ts">
/**
 * SpoilerView - Interactive Vue NodeView for BBCode Spoiler
 *
 * Matches the backend appearance: simple toggle link + content block.
 * Backend renders: <a class="spoiler-head">[toggle text]</a><div class="spoiler">...</div>
 */
import { computed } from "vue";
import { NodeViewWrapper, NodeViewContent, nodeViewProps } from "@tiptap/vue-3";
import {
  SPOILER_SHOW_TEXT,
  SPOILER_HIDE_TEXT,
} from "@/shared/lib/utils/bbcodeConstants";

const props = defineProps(nodeViewProps);

const isCollapsed = computed(() => props.node?.attrs?.collapsed !== false);

function toggleCollapsed() {
  props.updateAttributes({ collapsed: !isCollapsed.value });
}
</script>

<template>
  <NodeViewWrapper class="spoiler-wrapper">
    <!-- Toggle button - styled like a link to match backend .spoiler-head -->
    <span
      class="spoiler-head"
      @click="toggleCollapsed"
      @keydown.enter.prevent="toggleCollapsed"
      @keydown.space.prevent="toggleCollapsed"
      contenteditable="false"
      :aria-expanded="!isCollapsed"
      role="button"
      tabindex="0"
      >{{ isCollapsed ? SPOILER_SHOW_TEXT : SPOILER_HIDE_TEXT }}</span
    >
    <!-- Content block - matches backend .spoiler -->
    <div
      v-show="!isCollapsed"
      class="spoiler"
      role="region"
      aria-label="Содержимое спойлера"
    >
      <NodeViewContent />
    </div>
  </NodeViewWrapper>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"

.spoiler-wrapper
  display: block
  // Apply bbcode-content styles to children (spoiler-head, spoiler)
  +bbcode-content
</style>
