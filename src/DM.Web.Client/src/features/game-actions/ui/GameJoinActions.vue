<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import { useGameDetailsStore } from "@/entities/game";
import { personalApi, useAuthStore } from "@/entities/user";
import { notifyFailure } from "@/shared/lib/errors";

const store = useGameDetailsStore();
const { game, isSubscribed, characters } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());
const router = useRouter();

const publicId = computed(() => game.value?.publicId ?? "");

const viewerBlacklisted = computed(() => !!game.value?.viewerBlacklisted);
const isRecruiting = computed(
  () => (game.value?.recruitment?.isOpen ?? false) && !viewerBlacklisted.value,
);

// The game does not say whether the viewer holds an invitation; his own list does.
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

// On the game's blacklist an application is not offered for editing.
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

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.link
  display: block

.strip-action
  +inline-link-button
</style>
