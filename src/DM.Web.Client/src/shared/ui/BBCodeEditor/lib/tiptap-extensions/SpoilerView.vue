<script setup lang="ts">
/**
 * SpoilerView - Interactive Vue NodeView for BBCode Spoiler
 *
 * Matches the backend appearance: simple toggle link + content block.
 * Backend renders: <a class="spoiler-head">[toggle text]</a><div class="spoiler">...</div>
 */
import { computed, onBeforeUnmount } from "vue";
import { NodeViewWrapper, NodeViewContent, nodeViewProps } from "@tiptap/vue-3";
import {
  SPOILER_SHOW_TEXT,
  SPOILER_HIDE_TEXT,
} from "@/shared/lib/utils/bbcodeConstants";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables";

const props = defineProps(nodeViewProps);

const isCollapsed = computed(() => props.node?.attrs?.collapsed !== false);

// Manual user toggle: notify clears the registry's pending bulk action
// (registry-driven expand/collapse below go through updateAttributes only).
function toggleCollapsed() {
  notifyExpandableChanged();
  props.updateAttributes({ collapsed: !isCollapsed.value });
}

const unregister = registerExpandable({
  id: Symbol("SpoilerView"),
  isExpanded: () => !isCollapsed.value,
  expand: () => {
    if (isCollapsed.value) props.updateAttributes({ collapsed: false });
  },
  collapse: () => {
    if (!isCollapsed.value) props.updateAttributes({ collapsed: true });
  },
});
onBeforeUnmount(unregister);
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
    <!-- Content block - matches backend .spoiler. Wrapped into the shared
         .bb-collapse grid structure (same smooth expand/collapse animation
         as the display path, see _BbcodeContent.sass). inert keeps the
         collapsed content out of the tab order and accessibility tree. -->
    <div
      class="bb-collapse"
      :class="{ open: !isCollapsed }"
      :inert="isCollapsed"
    >
      <div class="bb-collapse-clip">
        <div class="spoiler" role="region" aria-label="Содержимое спойлера">
          <NodeViewContent />
        </div>
      </div>
    </div>
  </NodeViewWrapper>
</template>

<style scoped lang="sass">
@import "src/assets/styles/BbcodeContent"

.spoiler-wrapper
  display: block
  // Apply bbcode-content styles to children (spoiler-head, spoiler)
  +bbcode-content
</style>
