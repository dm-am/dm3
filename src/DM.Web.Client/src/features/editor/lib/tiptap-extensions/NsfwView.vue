<script setup lang="ts">
/**
 * NsfwView - Interactive Vue NodeView for BBCode NSFW
 *
 * Three states:
 * 1. Collapsed - red link with toggle text
 * 2. Expanded + not confirmed - yellow box with red blur overlay and 18+ warning
 * 3. Expanded + confirmed - yellow box (like regular spoiler)
 */
import { ref, computed, onBeforeUnmount } from "vue";
import { NodeViewWrapper, NodeViewContent, nodeViewProps } from "@tiptap/vue-3";
import {
  NSFW_SHOW_TEXT,
  NSFW_HIDE_TEXT,
  NSFW_WARNING_TEXT,
} from "@/shared/lib/utils/bbcodeConstants";
import { registerExpandable } from "@/shared/lib/composables";

const props = defineProps(nodeViewProps);

const isCollapsed = computed(() => props.node?.attrs?.collapsed !== false);
const isConfirmed = ref(false);

function toggleCollapsed() {
  props.updateAttributes({ collapsed: !isCollapsed.value });
}

function confirmAge() {
  isConfirmed.value = true;
}

// Bulk expand auto-confirms the 18+ overlay — user made an explicit
// page-wide gesture via the ScrollNav toggle button.
const unregister = registerExpandable({
  id: Symbol("NsfwView"),
  isExpanded: () => !isCollapsed.value,
  expand: () => {
    if (isCollapsed.value) {
      isConfirmed.value = true;
      props.updateAttributes({ collapsed: false });
    }
  },
  collapse: () => {
    if (!isCollapsed.value) props.updateAttributes({ collapsed: true });
  },
});
onBeforeUnmount(unregister);
</script>

<template>
  <NodeViewWrapper class="nsfw-wrapper">
    <!-- Toggle button - styled like a link, red color -->
    <span
      class="nsfw-head"
      @click="toggleCollapsed"
      @keydown.enter.prevent="toggleCollapsed"
      @keydown.space.prevent="toggleCollapsed"
      contenteditable="false"
      role="button"
      tabindex="0"
      >{{ isCollapsed ? NSFW_SHOW_TEXT : NSFW_HIDE_TEXT }}</span
    >

    <!-- Content block (yellow box) -->
    <div v-show="!isCollapsed" class="nsfw-spoiler">
      <NodeViewContent />
      <!-- 18+ overlay (shown until confirmed) -->
      <div
        v-if="!isConfirmed"
        class="nsfw-overlay"
        @click="confirmAge"
        role="button"
        tabindex="0"
        @keydown.enter="confirmAge"
        @keydown.space.prevent="confirmAge"
      >
        <span class="nsfw-warning">{{ NSFW_WARNING_TEXT }}</span>
      </div>
    </div>
  </NodeViewWrapper>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"

.nsfw-wrapper
  display: block
  // Apply bbcode-content styles to children
  +bbcode-content
</style>
