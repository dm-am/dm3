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
import { computed, onMounted, watch } from "vue";
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

// NPC authoring is lead-only. The server does not refuse the request — it
// quietly creates a plain character instead (CharacterService drops the flag
// for anyone below master/assistant) — so the page refuses up front rather
// than let the form promise an NPC it would not make. Nothing offers this page
// to anybody else ("Создать NPC" is a row for the leads alone), so an address
// typed by hand leads back to the roster, as on the blacklist below. Asked
// once the game is in: the roles arrive with it, and a master must not be sent
// away before his own role has.
const canAuthorNpc = computed(() => isMaster.value || isAssistant.value);
const npcRefused = computed(
  () => isNpc.value && game.value != null && !canAuthorNpc.value,
);

// Ordinary applications need the game active and its recruitment open
// (GameIntention.CreateCharacter). A pending player invitation bypasses closed
// recruitment, and that flag never reaches the client, so this stays a notice
// over the form rather than a refusal: an invited player must get through. The
// notice says who applies to such a game, so everyone else learns it too.
const recruitmentClosed = computed(
  () =>
    game.value != null &&
    !(game.value.status === "Active" && game.value.recruitment?.isOpen),
);

// The game's blacklist closes the application whatever the recruitment says,
// an invitation included. Nothing offers this page to the viewer on the list
// ("Подать заявку" is not in the menu), so an address typed by hand leads back
// to the roster rather than to a form, or to a sentence saying there is none.
const applicationRefused = computed(
  () => !isNpc.value && !!game.value?.viewerBlacklisted,
);

const refused = computed(() => npcRefused.value || applicationRefused.value);

watch(
  refused,
  (isRefused) => {
    if (isRefused)
      router.replace({ name: "game-characters", params: { id: gameId.value } });
  },
  { immediate: true },
);

// The backend character id is a GUID; the game's own id (not publicId) is
// what the create endpoint binds. Fall back to the route param before resolve.
const gameGuid = computed(() => game.value?.id ?? gameId.value);

// An empty set until the game answers: the sheet arrives with the game.
const schema = computed(() => game.value?.schema ?? { specifications: [] });

// The section of this page is the `?npc` flag, which a static meta.section
// cannot spell. Announced to the shell instead, which composes the heading and
// the tab out of it exactly as it does for every other sub-page.
useZoneSection(() => (isNpc.value ? "Новый NPC" : "Новый персонаж"));

onMounted(() => {
  if (!game.value) store.loadGame(gameId.value);
});

/**
 * No plate: the roster this page leaves for is the answer. The new sheet is
 * the first card of its section there — an application under "На рассмотрении"
 * (the applicant sees their own), an NPC under "Неигровые персонажи" — and the
 * section heading says what "Заявка отправлена" and "NPC создан" used to say
 * in the corner, next to the card itself rather than away from it.
 *
 * That only holds if the roster is fetched again first. It is shared state,
 * the sidebar fills it on the way into the game, and the roster page asks for
 * it only when it is empty — so a page that navigated without this would land
 * on exactly the list that was loaded before the character existed, and the
 * reader would look for their sheet on a screen that has every reason to be
 * trusted and is simply out of date. Awaited, so the list is right at its
 * first paint instead of correcting itself a moment later.
 */
async function onSaved() {
  await store.loadCharacters(gameId.value);
  router.push({ name: "game-characters", params: { id: gameId.value } });
}

function onCancel() {
  router.push({ name: "game-characters", params: { id: gameId.value } });
}
</script>

<template>
  <div class="character-create">
    <!-- An NPC waits for the game, which says whether the viewer leads it. -->
    <template v-if="!refused && !(isNpc && !game)">
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
  display: block
  margin-bottom: $medium
</style>
