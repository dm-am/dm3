<script setup lang="ts">
import { ref, onMounted, watch, nextTick } from "vue";
import { vBbcodeInteractive } from "@/shared/directives";
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

function highlight() {
  nextTick(() => {
    const content = contentRef.value;
    if (!content) return;
    clearDomHighlight(content);
    if (props.searchQuery) highlightDom(content, props.searchQuery);
  });
}

onMounted(highlight);
watch([() => props.html, () => props.searchQuery], highlight);
</script>

<template>
  <div
    ref="contentRef"
    v-bbcode-interactive="html"
    class="bbcode-content"
    v-html="html"
  />
</template>
