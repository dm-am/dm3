<script setup lang="ts">
/**
 * CharacterCreate — create a player character (an application, which enters
 * review) or, with `?npc`, a master-controlled NPC. Wires the schema-driven
 * CharacterForm (features/edit-character) to gameApi.createCharacter via the
 * form's own submit; on success it routes to the game's character list.
 *
 * The NPC surface is the same form gated by the `?npc` query flag and the
 * in-page lead check below; the backend enforces the same rule.
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
const { game, isMaster, isAssistant } = storeToRefs(store);

const gameId = computed(() => route.params.id as string);
const isNpc = computed(() => route.query.npc != null);

// NPC authoring is lead-only. The server does not refuse the request — it
// quietly creates a plain character instead (CharacterService drops the flag
// for anyone below master/assistant) — so the page refuses up front rather
// than let the form promise an NPC it would not make.
const canAuthorNpc = computed(() => isMaster.value || isAssistant.value);

// Ordinary applications need the game active and its recruitment open
// (GameIntention.CreateCharacter). A pending player invitation bypasses closed
// recruitment, and that flag never reaches the client, so this stays a notice
// over the form rather than a refusal: an invited player must get through, and
// everyone else learns here what the server will answer.
const recruitmentClosed = computed(
  () =>
    game.value != null &&
    !(game.value.status === "Active" && game.value.recruitment?.isOpen),
);

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
    <secondary-text v-if="isNpc && !canAuthorNpc">
      Создание NPC доступно мастеру и ассистентам.
    </secondary-text>

    <template v-else>
      <secondary-text v-if="!isNpc && recruitmentClosed" class="intro">
        Набор игроков закрыт: без приглашения мастера заявка не будет принята.
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
  display: block
  margin-bottom: $medium
</style>
