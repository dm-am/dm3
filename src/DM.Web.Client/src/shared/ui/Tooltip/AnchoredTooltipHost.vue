<script setup lang="ts">
/**
 * The one bubble of the page that code without a component of its own puts on
 * screen (anchoredTooltip.ts): the same TooltipBubble a <Tooltip> shows, with
 * the same fade. A new request is a new key, so the bubble is measured and
 * placed afresh over its new anchor.
 */
import TooltipBubble from "./TooltipBubble.vue";
import type { AnchoredTooltipRequest } from "./anchoredTooltip";

defineProps<{
  request: AnchoredTooltipRequest | null;
}>();
</script>

<template>
  <Transition name="tooltip-fade">
    <TooltipBubble
      v-if="request"
      :id="request.id"
      :key="request.id"
      :text="request.text"
      :anchor="request.anchor"
    />
  </Transition>
</template>
