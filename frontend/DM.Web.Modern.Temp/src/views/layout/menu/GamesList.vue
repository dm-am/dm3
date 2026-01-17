<script setup lang="ts">
import MenuBlock from "@/views/layout/MenuBlock.vue";
import { GameStatus } from "@/api/models/gaming";

defineProps<{
  token: string;
  title: string;
  gameStatus: GameStatus;
  linkText: string;
  showCreateLink?: boolean;
}>();
</script>

<template>
  <menu-block :token="token">
    <template #title>{{ title }}</template>
    <slot />
    <div class="separator">- - - - - - - - - - - - - - - - - - - - - - - - - -</div>
    <div v-if="showCreateLink">
      <span class="muted">-</span> <router-link class="forward" :to="{ name: 'create-game' }">Создать новую игру</router-link>
    </div>
    <div>
      <span class="muted">-</span> <router-link class="forward" :to="{ name: 'games', params: { status: gameStatus.toLowerCase() } }">{{ linkText }}</router-link>
    </div>
  </menu-block>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.forward
  font-weight: bold

.muted
  +theme(color, $secondary-text)

.separator
  +theme(color, $secondary-text)
</style>
