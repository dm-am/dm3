<script setup lang="ts">
/** The hint of an achievement tile: the chain, its progress and its table of tiers. */
import {
  chainProgressLabel,
  tierNumeral,
  formatThreshold,
  awardMetal,
  metalStyle,
  type AchievementChain,
} from "@/entities/achievement";

defineProps<{
  chain: AchievementChain;
}>();
</script>

<template>
  <div class="chain-popover">
    <div class="chain-popover__header">
      <strong class="chain-popover__title">{{ chain.title }}</strong>
      <span class="chain-popover__badge">
        {{ chain.earnedCount }} / {{ chain.tiers.length }}
      </span>
    </div>

    <p class="chain-popover__desc">{{ chain.description }}</p>

    <!-- One text node, so it copies as "197 из 500 отметок Нравится". -->
    <p v-if="!chain.completed" class="chain-popover__progress">
      {{ chainProgressLabel(chain) }}
    </p>

    <ul class="chain-popover__tiers">
      <li
        v-for="t in chain.tiers"
        :key="t.type.id"
        class="chain-popover__tier"
        :class="
          t.earnedUtc
            ? 'chain-popover__tier--earned'
            : 'chain-popover__tier--locked'
        "
        :style="metalStyle(awardMetal(t.type.tier))"
      >
        <span class="chain-popover__tier-degree">{{
          tierNumeral(t.position)
        }}</span>
        <span class="chain-popover__tier-title">{{ t.type.title }}</span>
        <span class="chain-popover__tier-threshold">
          {{ formatThreshold(t.type.category.metric, t.type.threshold) }}
        </span>
      </li>
    </ul>
  </div>
</template>

<style scoped lang="sass">
// Teleported to body, so the tile's --tier-metal does not reach it: each tier row carries its own.

// Wide enough for the longest tier title and its threshold on one line.
$hint-min-width: 300px
$pill-font-size: 10px
$threshold-font-size: 11px
// The column of the tier numeral.
$degree-width: 22px

.chain-popover
  display: flex
  flex-direction: column
  gap: $small
  min-width: $hint-min-width
  white-space: normal

  &__header
    display: flex
    align-items: center
    gap: $small

  &__title
    flex: 1 1 auto
    font-size: $secondary-font-size
    color: $tooltip-text

  // Tiers taken of tiers there are: a count, so nothing here takes the metal.
  &__badge
    flex: 0 0 auto
    padding: $tiny $tile-hint-step
    font-size: $pill-font-size
    font-weight: 700
    line-height: 1
    color: $tooltip-text
    background-color: var(--tooltip-chip-bg)
    border-radius: $border-radius
    letter-spacing: $tier-pill-letter-spacing

  &__desc
    margin: 0
    font-size: $secondary-font-size
    line-height: 1.4
    color: $tooltip-text
    opacity: $opacity-soft

  &__progress
    margin: 0
    font-size: $secondary-font-size
    color: $tooltip-text

  &__tiers
    margin: $minor 0 0
    padding: 0
    list-style: none
    display: flex
    flex-direction: column
    gap: $tiny
    border-top: 1px solid var(--tooltip-divider)
    padding-top: $tile-hint-step

  &__tier
    display: grid
    grid-template-columns: $degree-width 1fr auto
    align-items: center
    gap: $small
    padding: $tiny 0
    font-size: $secondary-font-size
    color: $tooltip-text

    &--locked
      opacity: $locked-text-opacity

  // The same badge as on the tile: metal fill, light numeral.
  &__tier-degree
    text-align: center
    font-size: $pill-font-size
    font-weight: 700
    color: var(--tier-badge-text)
    background-color: var(--tier-metal, $text-muted)
    border-radius: $minor
    padding: $tiny 0
    line-height: 1
    text-shadow: 0 0 1px var(--tier-badge-halo)

  // Never truncated: a longer title wraps whole.
  &__tier-title
    overflow-wrap: break-word

  &__tier-threshold
    font-variant-numeric: tabular-nums
    opacity: $opacity-soft
    font-size: $threshold-font-size
</style>
