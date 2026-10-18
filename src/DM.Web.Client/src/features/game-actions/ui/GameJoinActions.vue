<script setup lang="ts">
/**
 * GameJoinActions — the player-facing "actions with the game" group:
 *  - subscribe / unsubscribe (reader role, store.subscribe/unsubscribe)
 *  - apply to join (create a character → it enters review), shown while the
 *    game is recruiting, and to a viewer invited to play while it is not.
 *
 * Gating (only shown for an authenticated non-master) is the caller's
 * responsibility. `variant` mirrors GameStatusButtons ("strip" for the sidebar,
 * "button" for page surfaces).
 */
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import { useGameDetailsStore } from "@/entities/game";
import { personalApi, useAuthStore } from "@/entities/user";
import { notifyFailure } from "@/shared/lib/errors";

withDefaults(defineProps<{ variant?: "strip" | "button" }>(), {
  variant: "strip",
});

const store = useGameDetailsStore();
const { game, isSubscribed, characters } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());
const router = useRouter();

const publicId = computed(() => game.value?.publicId ?? "");

// The game's blacklist closes the application, so "Подать заявку" is not
// offered to the viewer on it, recruiting or not.
const viewerBlacklisted = computed(() => !!game.value?.viewerBlacklisted);
const isRecruiting = computed(
  () => (game.value?.recruitment?.isOpen ?? false) && !viewerBlacklisted.value,
);

// A pending invitation to play opens the application of an active game whose
// recruitment is shut (GameIntention.CreateCharacter), and the game does not
// say whether the viewer holds one: his own list of invitations does, the one
// "Настройки аккаунта" shows. Asked only where it decides the row, and taken
// as the answer only for the game and the viewer it was asked for.
const invitationQuestion = computed(() => {
  const current = game.value;
  if (!current || !user.value || viewerBlacklisted.value) return null;
  if (current.status !== "Active" || current.recruitment?.isOpen) return null;
  return `${current.id}:${user.value.id}`;
});
const invitedToPlay = ref(false);

watch(
  invitationQuestion,
  async (question) => {
    invitedToPlay.value = false;
    if (!question) return;
    const gameId = game.value?.id;
    const { data } = await personalApi.getMyInvitations();
    if (question !== invitationQuestion.value) return;
    invitedToPlay.value = (data?.resources ?? []).some(
      (invitation) =>
        invitation.entityType === "game" &&
        invitation.entityId === gameId &&
        invitation.type === "player",
    );
  },
  { immediate: true },
);

const canApply = computed(() => isRecruiting.value || invitedToPlay.value);

// The current user's own (non-NPC) character, if any. Drives the
// "Редактировать персонажа" action (doc 4.2.1.3, shown when the user has at
// least one character; the edit page preselects the first one). Characters
// are loaded by GamePanel into the shared store.
//
// On the game's blacklist an application is not offered for editing: the
// list closes it. The sheet of a character in play stays the owner's.
const myCharacter = computed(() =>
  characters.value.find(
    (c) =>
      !c.isNpc &&
      c.author?.id === user.value?.id &&
      c.status !== "Declined" &&
      !(viewerBlacklisted.value && c.status === "UnderReview"),
  ),
);

const busy = ref(false);

async function toggleSubscribe() {
  busy.value = true;
  const error = isSubscribed.value
    ? await store.unsubscribe()
    : await store.subscribe();
  busy.value = false;
  if (error) notifyFailure(error, "Не удалось изменить подписку");
}

function applyToJoin() {
  router.push({
    name: "game-character-create",
    params: { id: publicId.value },
  });
}
</script>

<template>
  <!-- Sidebar strip variant -->
  <template v-if="variant === 'strip'">
    <li class="link">
      <span class="muted" aria-hidden="true">- </span>
      <button
        type="button"
        class="strip-action"
        :disabled="busy"
        @click="toggleSubscribe"
      >
        {{ isSubscribed ? "Отписаться" : "Подписаться" }}
      </button>
    </li>
    <li v-if="canApply" class="link">
      <span class="muted" aria-hidden="true">- </span>
      <button type="button" class="strip-action" @click="applyToJoin">
        Подать заявку
      </button>
    </li>
    <li v-if="myCharacter" class="link">
      <span class="muted" aria-hidden="true">- </span>
      <router-link
        :to="{
          name: 'game-character-edit',
          params: { id: publicId, characterId: myCharacter.id },
        }"
        >Редактировать персонажа</router-link
      >
    </li>
  </template>

  <!-- Page button-group variant -->
  <div v-else class="join-buttons">
    <button
      type="button"
      class="join-btn"
      :disabled="busy"
      @click="toggleSubscribe"
    >
      {{ isSubscribed ? "Отписаться" : "Подписаться" }}
    </button>
    <button v-if="canApply" type="button" class="join-btn" @click="applyToJoin">
      Подать заявку
    </button>
    <router-link
      v-if="myCharacter"
      class="join-btn"
      :to="{
        name: 'game-character-edit',
        params: { id: publicId, characterId: myCharacter.id },
      }"
    >
      Редактировать персонажа
    </router-link>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.link
  display: block

.muted
  color: $text-muted

// A word of the sidebar menu, drawn as every other link of it, out of reach
// included.
.strip-action
  +inline-link-button

.join-buttons
  display: flex
  flex-wrap: wrap
  gap: $small

.join-btn
  font-size: $secondary-font-size
  +button
</style>
