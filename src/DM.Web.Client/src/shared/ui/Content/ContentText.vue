<script setup lang="ts">
import { ref, onMounted, watch, nextTick } from "vue";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";

const props = defineProps<{
  html: string;
}>();

const contentRef = ref<HTMLElement | null>(null);

onMounted(() => {
  nextTick(() => {
    initBbcodeInteractive(contentRef.value);
  });
});

watch(
  () => props.html,
  () => {
    nextTick(() => {
      initBbcodeInteractive(contentRef.value);
    });
  },
);
</script>

<template>
  <div ref="contentRef" class="content-text" v-html="html" />
</template>

<style scoped lang="sass">
@import "src/assets/styles/BbcodeContent"

.content-text
  word-wrap: break-word
  word-break: break-word
  overflow-wrap: break-word
  +bbcode-content
</style>
