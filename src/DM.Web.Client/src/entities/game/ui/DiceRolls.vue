<script setup lang="ts">
// One root, so the host's scoped rule places it; the rolls of a post are never folded.
import { computed } from "vue";
import type { DiceRoll } from "../model/diceTypes";
import { DICE_RESULTS_HEADING, formatDiceRoll } from "../model/diceLine";

const props = defineProps<{ rolls: DiceRoll[] }>();

const diceLines = computed(() =>
  props.rolls.map((roll, index) => formatDiceRoll(roll, index + 1)),
);
</script>

<template>
  <div class="dice-rolls">
    <div class="dice-heading">{{ DICE_RESULTS_HEADING }}</div>
    <div
      v-for="(line, lineIndex) in diceLines"
      :key="lineIndex"
      class="dice-roll"
    >
      {{ line }}
    </div>
  </div>
</template>

<style scoped lang="sass">
// DM2's div.diceRollsbg, on the theme grey nearest to it.
.dice-rolls
  padding: $tiny 7px
  background-color: $bg-element-accent
  color: $text
  font-size: $font-size
  line-height: 1.25
  text-align: justify
  hyphens: manual
</style>
