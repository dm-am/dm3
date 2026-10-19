<script setup lang="ts">
// In the frame of a comment and a post, so a record reads as a piece of content of the site.
import { computed } from "vue";
import { getActivePinia } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import { CaptionLine } from "@/shared/ui/CaptionLine";

// A record drawn where no store is installed keeps the switch's default, on.
const pinia = getActivePinia();
const ui = pinia ? useUiStore(pinia) : null;
const typesetting = computed(() => ({
  hyphenated: ui ? ui.hyphenateWords : true,
}));
</script>

<template>
  <div class="record-block">
    <h2 v-if="$slots.title" class="record-block__title">
      <slot name="title" />
    </h2>
    <!-- Every direct child of the details slot is a line of its own. -->
    <div v-if="$slots.details" class="record-block__details">
      <slot name="details" />
    </div>
    <div v-if="$slots.default" class="record-block__text" :class="typesetting">
      <slot />
    </div>
    <!-- Every direct child of the caption slot is a part of the line. -->
    <div v-if="$slots.caption" class="record-block__caption">
      <CaptionLine><slot name="caption" /></CaptionLine>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *
@use "@/assets/styles/CommentParts" as *

.record-block
  +comment-frame

// The heading of a topic, TopicCard.vue.
.record-block__title
  margin: 0 0 $small
  padding-bottom: $small
  border-bottom: 1px dashed $border
  font-size: $font-size
  font-weight: bold
  overflow-wrap: anywhere

.record-block__details
  margin-bottom: $small
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4

  > :deep(*)
    display: block

// The text of a comment, CommentText.vue.
.record-block__text
  color: $text
  +body-text
  overflow-wrap: anywhere

  & + .record-block__caption
    margin-top: $small

// On the block too, so the line box is the caption's height and not the text's.
.record-block__caption
  font-size: $tertiary-font-size
</style>
