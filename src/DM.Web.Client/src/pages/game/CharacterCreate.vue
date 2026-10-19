<script setup lang="ts">
// A character application, or with `?npc` a character of the master.
import { computed, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { CharacterForm } from "@/features/edit-character";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useZoneSection } from "@/shared/lib/composables/useZoneSection";

const route = useRoute();
const router = useRouter();
const store = useGameDetailsStore();
const { game, isMaster, isAssistant } = storeToRefs(store);

const gameId = computed(() => route.params.id as string);
const isNpc = computed(() => route.query.npc != null);

// The server does not refuse an NPC to a player, it drops the flag; so the page
// refuses. Asked with the game in: a failed reload empties it before unmount.
const canAuthorNpc = computed(() => isMaster.value || isAssistant.value);
const npcRefused = computed(
  () => isNpc.value && game.value != null && !canAuthorNpc.value,
);

// A notice and not a refusal: an invited player applies past a closed
// recruitment, and the invitation never reaches the client.
const recruitmentClosed = computed(
  () => !(game.value?.status === "Active" && game.value.recruitment?.isOpen),
);

const applicationRefused = computed(
  () => !isNpc.value && !!game.value?.viewerBlacklisted,
);

const refused = computed(() => npcRefused.value || applicationRefused.value);

const roster = computed(() => ({
  name: "game-characters",
  params: { id: gameId.value },
}));

watch(
  refused,
  (isRefused) => {
    if (isRefused) router.replace(roster.value);
  },
  { immediate: true },
);

// The create endpoint binds the guid, not the public id.
const gameGuid = computed(() => game.value?.id ?? gameId.value);

const schema = computed(() => game.value?.schema ?? { specifications: [] });

// The section depends on `?npc`, which meta.section cannot spell.
useZoneSection(() =>
  isNpc.value ? "Новый неигровой персонаж" : "Новый персонаж",
);

// No plate: the new card on the roster is the answer, so the roster is read
// again before the page leaves for it (the roster page reuses a loaded one).
async function onSaved() {
  await store.loadCharacters(gameId.value);
  router.push(roster.value);
}

function onCancel() {
  router.push(roster.value);
}
</script>

<template>
  <div class="character-create">
    <template v-if="!refused">
      <secondary-text v-if="!isNpc && recruitmentClosed" class="intro">
        Набор игроков закрыт: заявку подают по приглашению мастера
      </secondary-text>
      <secondary-text v-else-if="!isNpc" class="intro">
        Заполните анкету персонажа. После отправки мастер рассмотрит заявку.
      </secondary-text>

      <CharacterForm
        :schema="schema"
        :game-id="gameGuid"
        :is-npc="isNpc"
        @saved="onSaved"
        @cancel="onCancel"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.character-create
  max-width: $grid-step * 200

.intro
  margin-bottom: $medium
</style>
