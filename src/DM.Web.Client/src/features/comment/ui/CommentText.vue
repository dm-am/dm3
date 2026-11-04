<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { vBbcodeInteractive } from "@/shared/directives";
import { trimHtmlWhitespace } from "@/shared/lib/utils/trimHtmlWhitespace";
import { highlightDom, clearDomHighlight } from "@/shared/lib/utils/highlight";
import { COMMENT_MAX_HEIGHT } from "@/shared/lib/constants/content";

const props = defineProps<{
  html: string;
  searchQuery?: string;
}>();

const { hyphenateWords } = storeToRefs(useUiStore());
const typesetting = computed(() => ({
  hyphenated: hyphenateWords.value,
}));

// Trimmed so phantom empty lines never eat the truncation budget.
const commentHtml = computed(() => trimHtmlWhitespace(props.html));

const contentEl = ref<HTMLElement | null>(null);

// The directive binds the markup; the search highlight is laid over it once it is drawn.
function highlightComment(el: HTMLElement) {
  contentEl.value = el;
  clearDomHighlight(el);
  if (props.searchQuery) {
    highlightDom(el, props.searchQuery);
  }
}

watch(
  () => props.searchQuery,
  (query) => {
    if (!contentEl.value) return;
    clearDomHighlight(contentEl.value);
    if (query) {
      highlightDom(contentEl.value, query);
    }
  },
);
</script>

<template>
  <TruncatedContent
    :truncatable="true"
    :max-height="COMMENT_MAX_HEIGHT"
    :watch-key="commentHtml"
    :on-content-mounted="highlightComment"
  >
    <div
      v-bbcode-interactive="commentHtml"
      class="comment-text bbcode-content"
      :class="typesetting"
      v-html="commentHtml"
    />
  </TruncatedContent>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *

.comment-text
  color: $text
  +body-text
</style>
