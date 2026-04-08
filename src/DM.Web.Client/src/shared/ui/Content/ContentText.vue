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
  <div ref="contentRef" class="bbcode-content" v-html="html" />
</template>

<!-- No scoped styles needed - .bbcode-content is defined globally in BbcodeGlobal.sass -->
