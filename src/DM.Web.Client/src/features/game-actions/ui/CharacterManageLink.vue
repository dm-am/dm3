<script setup lang="ts">
// Shown by CharacterIntentionResolver's rule (master or assistant), not by the game's canManage.
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useRoute } from "vue-router";
import { useGameDetailsStore, type Character } from "@/entities/game";

const props = defineProps<{ character: Character }>();

const store = useGameDetailsStore();
const { game, isMaster, isAssistant } = storeToRefs(store);
const route = useRoute();

const canManageCharacters = computed(() => isMaster.value || isAssistant.value);

const publicId = computed(
  () => game.value?.publicId ?? (route.params.id as string),
);

// A character who left or was exiled leaves staff nothing to do on that page.
const hasActions = computed<boolean>(() => {
  const c = props.character;
  if (c.isNpc) return true;
  if (c.status === "UnderReview" || c.status === "Active") return true;
  return c.status === "Retired" && !!c.isDead;
});

const label = computed(() => {
  const c = props.character;
  if (c.status === "UnderReview") return "Рассмотреть заявку";
  return c.isNpc ? "Управление неигровым персонажем" : "Управление персонажем";
});
</script>

<template>
  <router-link
    v-if="canManageCharacters && hasActions"
    class="manage-link"
    :to="{
      name: 'game-character-edit',
      params: { id: publicId, characterId: character.id },
    }"
  >
    {{ label }}
  </router-link>
</template>

<style scoped lang="sass">
.manage-link
  display: inline-block
  margin-top: $tiny
</style>
