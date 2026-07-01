<script setup lang="ts">
import { ref, onMounted, watch, nextTick } from "vue";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";
import { highlightDom, clearDomHighlight } from "@/shared/lib/utils/highlight";

const props = withDefaults(
  defineProps<{
    html: string;
    /** Optional search query — highlights matches in rendered text */
    searchQuery?: string;
  }>(),
  { searchQuery: "" },
);

const contentRef = ref<HTMLElement | null>(null);

function processContent() {
  nextTick(() => {
    if (!contentRef.value) return;
    initBbcodeInteractive(contentRef.value);
    // Apply search highlighting after BBCode interactive init
    clearDomHighlight(contentRef.value);
    if (props.searchQuery) {
      highlightDom(contentRef.value, props.searchQuery);
    }
  });
}

onMounted(processContent);
watch(() => props.html, processContent);
watch(
  () => props.searchQuery,
  () => {
    nextTick(() => {
      if (!contentRef.value) return;
      clearDomHighlight(contentRef.value);
      if (props.searchQuery) {
        highlightDom(contentRef.value, props.searchQuery);
      }
    });
  },
);
</script>

<template>
  <div ref="contentRef" class="bbcode-content" v-html="html" />
</template>

<!-- No scoped styles needed - .bbcode-content is defined globally in BbcodeGlobal.sass -->
