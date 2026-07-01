<script setup lang="ts">
/**
 * GameIcon — рендерит иконку из game-icons.net спрайта.
 *
 * Используется в наградах и достижениях. Имя валидируется в рантайме
 * (отсутствует → пустой `<svg>`, без падения) и на сервере при
 * сохранении AwardType/AchievementType (FluentValidation → 400).
 *
 * Цвет управляется через CSS-переменную `color` родителя
 * (через `fill="currentColor"`), размер — через `font-size` или
 * прямой width/height на корневом svg.
 */
import { computed } from "vue";
import { GAME_ICONS, isGameIcon, type GameIconName } from "./gameIcons";

const props = defineProps<{
  /** Имя иконки (kebab-case как на game-icons.net). */
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
