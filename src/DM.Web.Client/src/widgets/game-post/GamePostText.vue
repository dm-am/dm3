<script setup lang="ts">
/** One of the two blocks of text the author wrote; the host makes its HTML. */
import { storeToRefs } from "pinia";
import { ContentText } from "@/shared/ui/Content";
import { useUiStore } from "@/shared/stores/ui";

defineProps<{
  html: string;
  kind: "game" | "metagame";
  /** Search query for highlighting matches in the text */
  searchQuery?: string;
}>();

const { hyphenateWords } = storeToRefs(useUiStore());
</script>

<template>
  <div
    :class="[
      kind === 'game' ? 'game-text' : 'metagame-text',
      { hyphenated: hyphenateWords },
    ]"
  >
    <content-text :html="html" :search-query="searchQuery" />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *

// DM2's div.dmtxt > div.p: 10px in from either edge.
.game-text
  color: $text
  margin: 11px 10px

// DM2's div.p.postcomment, in the meta colour.
.metagame-text
  color: $text-meta
  margin: 11px 10px

  b, strong, i, em, u, s, strike, del, pre, code, li, span, div
    color: inherit

// 33px under the game text, as on DM2; under the rolls their 22px.
.game-text + .metagame-text
  margin-top: 33px

// DM2's 16px on a step of 20, flush on both sides.
.game-text,
.metagame-text
  +body-text
</style>
