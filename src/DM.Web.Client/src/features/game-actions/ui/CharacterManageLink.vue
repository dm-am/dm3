<script setup lang="ts">
/**
 * CharacterManageLink — the roster's way into a character's own page, where the
 * whole lifecycle lives: accept or decline an application, mark a character
 * dead, exile it, bring it back, delete an NPC.
 *
 * The roster had no way there at all, and the two links that did exist were
 * each conditional on something the master of a young game does not have: the
 * sidebar item appears once an NPC exists, and the join actions point at the
 * viewer's own character and are never drawn for the master. An application
 * could be read on this page and answered nowhere.
 *
 * Who sees the link follows the backend rule (CharacterIntentionResolver:
 * accept, decline, kill and exile ask for the master or an assistant), not the
 * game's canManage — the game mentor manages the game, not its characters, and
 * a link that ends in a refusal is worse than no link.
 */
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useRoute } from "vue-router";
import { useGameDetailsStore, type Character } from "@/entities/game";

const props = defineProps<{ character: Character }>();

const store = useGameDetailsStore();
const { game, isMaster, isAssistant } = storeToRefs(store);
const route = useRoute();

const canManageCharacters = computed(() => isMaster.value || isAssistant.value);

// Routes carry the game's public id; until the game resolves, the route param
// is that same value.
const publicId = computed(
  () => game.value?.publicId ?? (route.params.id as string),
);

// A character who left the game or was exiled leaves staff nothing to do on
// that page, and a link into a page that only says so is the same dead end one
// screen further in.
const hasActions = computed<boolean>(() => {
  const c = props.character;
  if (c.isNpc) return true;
  if (c.status === "UnderReview" || c.status === "Active") return true;
  return c.status === "Retired" && !!c.isDead;
});

// An application is reviewed; a character already in the game is managed.
const label = computed(() => {
  const c = props.character;
  if (c.status === "UnderReview") return "Рассмотреть заявку";
  return c.isNpc ? "Управление NPC" : "Управление персонажем";
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
