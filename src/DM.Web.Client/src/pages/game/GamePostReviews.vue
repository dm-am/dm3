<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { game } = storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);
</script>

<template>
  <div class="game-post-reviews">
    <SecondaryText>
      Посты с отзывами в игре "{{ game?.title }}" — в разработке
    </SecondaryText>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.game-post-reviews
  padding: $medium
</style>
