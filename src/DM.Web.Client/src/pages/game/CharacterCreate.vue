<script setup lang="ts">
/**
 * CharacterCreate — create a player character (an application, which enters
 * review) or, with `?npc`, a master-controlled NPC. Wires the schema-driven
 * CharacterForm (features/edit-character) to gameApi.createCharacter via the
 * form's own submit; on success it routes to the game's character list.
 *
 * The NPC surface is the same form gated only by the `?npc` query flag; NPC
 * authoring is master/assistant-only, enforced by the panel link and the
 * backend.
 */
import { computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { CharacterForm } from "@/features/edit-character";
import { createEmptySchema } from "@/entities/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useZoneSection } from "@/shared/lib/composables/useZoneSection";
import { useToast } from "@/shared/lib/composables/useToast";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const store = useGameDetailsStore();
const { game } = storeToRefs(store);

const gameId = computed(() => route.params.id as string);
const isNpc = computed(() => route.query.npc != null);

// The backend character id is a GUID; the game's own id (not publicId) is
// what the create endpoint binds. Fall back to the route param before resolve.
const gameGuid = computed(() => game.value?.id ?? gameId.value);

const schema = computed(() => game.value?.schema ?? createEmptySchema());

// The section of this page is the `?npc` flag, which a static meta.section
// cannot spell. Announced to the shell instead, which composes the heading and
// the tab out of it exactly as it does for every other sub-page.
useZoneSection(() => (isNpc.value ? "Новый NPC" : "Новый персонаж"));

onMounted(() => {
  if (!game.value) store.loadGame(gameId.value);
});

function onSaved() {
  toast.success(isNpc.value ? "NPC создан" : "Заявка отправлена");
  router.push({ name: "game-characters", params: { id: gameId.value } });
}

function onCancel() {
  router.push({ name: "game-characters", params: { id: gameId.value } });
}
</script>

<template>
  <div class="character-create">
    <secondary-text v-if="!isNpc" class="intro">
      Заполните анкету персонажа. После отправки мастер рассмотрит заявку.
    </secondary-text>

    <CharacterForm
      :schema="schema"
      :game-id="gameGuid"
      :is-npc="isNpc"
      @saved="onSaved"
      @cancel="onCancel"
    />
  </div>
</template>

<style scoped lang="sass">
.character-create
  max-width: $grid-step * 200

.intro
  display: block
  margin-bottom: $medium
</style>
