<script setup lang="ts">
/**
 * GameIcon — renders an icon from the game-icons.net sprite.
 *
 * Used in awards and achievements. The name is validated at runtime
 * (missing → an empty `<svg>`, no crash) and on the server when
 * saving an AwardType/AchievementType (FluentValidation → 400).
 *
 * Color is driven by the parent's `color` CSS variable
 * (via `fill="currentColor"`), size — via `font-size` or
 * direct width/height on the root svg.
 */
import { computed } from "vue";
import { GAME_ICONS, isGameIcon, type GameIconName } from "./gameIcons";

const props = defineProps<{
  /** Icon name (kebab-case as on game-icons.net). */
  name: string;
}>();

const entry = computed(() =>
  isGameIcon(props.name) ? GAME_ICONS[props.name as GameIconName] : null,
);
</script>

<template>
  <svg
    class="game-icon"
    viewBox="0 0 512 512"
    fill="currentColor"
    role="img"
    :aria-label="name"
  >
    <path v-if="entry" :d="entry.path" />
  </svg>
</template>

<style scoped lang="sass">
.game-icon
  width: 1em
  height: 1em
  display: inline-block
  vertical-align: middle
  flex-shrink: 0
</style>
