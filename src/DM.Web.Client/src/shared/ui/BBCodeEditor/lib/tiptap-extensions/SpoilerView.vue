<script setup lang="ts">
// The server's look: a toggle link and the content block.
import { computed, onBeforeUnmount } from "vue";
import { NodeViewWrapper, NodeViewContent, nodeViewProps } from "@tiptap/vue-3";
import {
  SPOILER_SHOW_TEXT,
  SPOILER_HIDE_TEXT,
} from "@/shared/lib/bbcode/bbcodeConstants";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";

const props = defineProps(nodeViewProps);

const isCollapsed = computed(() => props.node.attrs.collapsed !== false);

// The caption names the button open or closed, as the server draws it.
const caption = computed(() => (props.node.attrs.text as string | null) || "");
const showText = computed(() => caption.value || SPOILER_SHOW_TEXT);
const hideText = computed(() => caption.value || SPOILER_HIDE_TEXT);

// A press of the reader's own clears the registry's pending bulk action.
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
    <span
      class="spoiler-head"
      @click="toggleCollapsed"
      @keydown.enter.prevent="toggleCollapsed"
      @keydown.space.prevent="toggleCollapsed"
      contenteditable="false"
      :aria-expanded="!isCollapsed"
      role="button"
      tabindex="0"
      >{{ isCollapsed ? showText : hideText }}</span
    >
    <!-- The display path's .bb-collapse; inert keeps the collapsed content out of the tab order. -->
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
@use "@/assets/styles/BbcodeContent" as *

.spoiler-wrapper
  display: block
  +bbcode-content
</style>
