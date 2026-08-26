<script setup lang="ts">
/**
 * GameJoinActions — the player-facing "actions with the game" group:
 *  - subscribe / unsubscribe (reader role, store.subscribe/unsubscribe)
 *  - apply to join (create a character → it enters review), shown while the
 *    game is recruiting.
 *
 * Gating (only shown for an authenticated non-master) is the caller's
 * responsibility. `variant` mirrors GameStatusButtons ("strip" for the sidebar,
 * "button" for page surfaces).
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { notifyFailure } from "@/shared/lib/errors";

withDefaults(defineProps<{ variant?: "strip" | "button" }>(), {
  variant: "strip",
});

const store = useGameDetailsStore();
const { game, isSubscribed, characters } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());
const router = useRouter();

const publicId = computed(() => game.value?.publicId ?? "");
const isRecruiting = computed(() => game.value?.recruitment?.isOpen ?? false);

// The current user's own (non-NPC) character, if any. Drives the
// "Редактировать персонажа" action (doc 4.2.1.3, shown when the user has at
// least one character; the edit page preselects the first one). Characters
// are loaded by GamePanel into the shared store.
const myCharacter = computed(() =>
  characters.value.find(
    (c) =>
      !c.isNpc && c.author?.id === user.value?.id && c.status !== "Declined",
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
    <li v-if="isRecruiting" class="link">
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
    <button
      v-if="isRecruiting"
      type="button"
      class="join-btn"
      @click="applyToJoin"
    >
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

.strip-action
  padding: 0
  border: none
  background: none
  font: inherit
  color: $link
  cursor: pointer

  &:hover:not(:disabled)
    color: $link-hover
    text-decoration: underline

  &:disabled
    opacity: 0.6
    cursor: default

.join-buttons
  display: flex
  flex-wrap: wrap
  gap: $small

.join-btn
  font-size: $secondary-font-size
  +button
</style>
