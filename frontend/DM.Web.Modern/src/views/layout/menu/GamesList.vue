<script setup lang="ts">
import { computed } from "vue";
import MenuBlock from "@/views/layout/MenuBlock.vue";
import { GameStatus } from "@/api/models/gaming";

const props = defineProps<{
  token: string;
  title: string;
  gameStatus: GameStatus;
  linkText: string;
  showCreateLink?: boolean;
}>();

// Map GameStatus to route name
const routeName = computed(() => {
  switch (props.gameStatus) {
    case GameStatus.Active:
      return "games-active";
    case GameStatus.Recruiting:
    case GameStatus.Requirement:
      return "games-recruiting";
    case GameStatus.Finished:
      return "games-finished";
    case GameStatus.Moderation:
    case GameStatus.RequiresModeration:
      return "games-moderation";
    default:
      return "games-active";
  }
});
</script>

<template>
  <menu-block :token="token">
    <template #title>{{ title }}</template>
    <slot />
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div v-if="showCreateLink">
      <span class="muted">- </span
      ><router-link class="forward" :to="{ name: 'create-game' }"
        >Создать новую игру</router-link
      >
    </div>
    <div>
      <span class="muted">- </span
      ><router-link class="forward" :to="{ name: routeName }">{{
        linkText
      }}</router-link>
    </div>
  </menu-block>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.forward
  font-weight: bold

.muted
  color: $text-muted

.separator
  color: $text-muted
</style>
