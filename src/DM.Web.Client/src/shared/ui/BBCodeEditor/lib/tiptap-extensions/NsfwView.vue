<script setup lang="ts">
import { ref, computed, onBeforeUnmount } from "vue";
import { NodeViewWrapper, NodeViewContent, nodeViewProps } from "@tiptap/vue-3";
import {
  NSFW_SHOW_TEXT,
  NSFW_HIDE_TEXT,
  NSFW_WARNING_TEXT,
} from "@/shared/lib/bbcode/bbcodeConstants";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";

const props = defineProps(nodeViewProps);

const isCollapsed = computed(() => props.node.attrs.collapsed !== false);
const isConfirmed = ref(false);

// A press of the reader's own clears the registry's pending bulk action.
function toggleCollapsed() {
  notifyExpandableChanged();
  props.updateAttributes({ collapsed: !isCollapsed.value });
}

function confirmAge() {
  isConfirmed.value = true;
}

// "Развернуть все" is an explicit page-wide gesture, so it confirms the 18+ overlay too.
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

    <!-- The display path's .bb-collapse, for the same expand animation. -->
    <div
      class="bb-collapse"
      :class="{ open: !isCollapsed }"
      :inert="isCollapsed"
    >
      <div class="bb-collapse-clip">
        <div class="nsfw-spoiler">
          <NodeViewContent />
          <!-- Stays mounted and fades out once confirmed, as on the display path. -->
          <div
            class="nsfw-overlay"
            :class="{ confirmed: isConfirmed }"
            @click="confirmAge"
            role="button"
            tabindex="0"
            @keydown.enter="confirmAge"
            @keydown.space.prevent="confirmAge"
          >
            <span class="nsfw-warning">{{ NSFW_WARNING_TEXT }}</span>
          </div>
        </div>
      </div>
    </div>
  </NodeViewWrapper>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *

.nsfw-wrapper
  display: block
  +bbcode-content
</style>
