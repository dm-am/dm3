<template>
  <section v-if="invitations.length > 0 || loading" class="section">
    <h2 class="section-title">Приглашения в игры</h2>

    <div class="invitations-content">
      <div v-if="loading" class="loading-state">Загрузка...</div>

      <div v-else class="invitations-list">
        <div
          v-for="invitation in invitations"
          :key="invitation.id"
          class="invitation-item"
        >
          <div class="invitation-info">
            <div class="invitation-header">
              <RouterLink
                :to="targetRoute(invitation)"
                class="invitation-game-link"
              >
                {{ invitation.entityTitle }}
              </RouterLink>
            </div>
            <div class="invitation-details">
              <span class="invitation-from">
                От:
                <RouterLink
                  :to="inviterRoute(invitation.inviterUsername)"
                  class="invitation-user-link"
                >
                  {{ invitation.inviterUsername }}
                </RouterLink>
              </span>
              <span class="meta-sep" aria-hidden="true">{{ " | " }}</span>
              <span class="invitation-role">{{
                typeLabel(invitation.type)
              }}</span>
              <span class="meta-sep" aria-hidden="true">{{ " | " }}</span>
              <span class="invitation-date">{{
                formatDate(invitation.createdUtc)
              }}</span>
            </div>
          </div>

          <div class="invitation-actions">
            <button
              class="action-btn action-btn--accept"
              :disabled="processingId === invitation.id"
              @click="accept(invitation.id)"
            >
              {{ processingId === invitation.id ? "..." : "Принять" }}
            </button>
            <button
              class="action-btn action-btn--reject"
              :disabled="processingId === invitation.id"
              @click="reject(invitation.id)"
            >
              Отклонить
            </button>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { RouterLink, type RouteLocationRaw } from "vue-router";
import { personalApi } from "@/entities/user";
import { formatDate } from "@/shared/lib/utils/datetime";
import { useToast } from "@/shared/lib/composables/useToast";
import type { ReceivedInvitation } from "@/shared/api/models/personal";
import { notifyFailure } from "@/shared/lib/errors";

const toast = useToast();

// The shape the endpoint actually answers. The section used to type it as the
// game-side Invitation (gameId / gameTitle), so every row rendered an empty
// link — a zero-width anchor with no name in it — pointing at /games/undefined.
const invitations = ref<ReceivedInvitation[]>([]);
const loading = ref(true);
const processingId = ref<string | null>(null);

onMounted(async () => {
  await loadInvitations();
});

async function loadInvitations() {
  loading.value = true;
  const { data, error } = await personalApi.getMyInvitations();
  loading.value = false;

  if (!error && data) {
    invitations.value = data.resources;
  }
}

// Named routes, and the right one per entity: the endpoint answers game and
// blog invitations together, and /games/<id> is not a route at all (the game
// lives at /game/:id), so the hand-written path fell through to the catch-all.
function targetRoute(invitation: ReceivedInvitation): RouteLocationRaw {
  return invitation.entityType === "blog"
    ? { name: "blog", params: { id: invitation.entityId } }
    : { name: "game", params: { id: invitation.entityId } };
}

// The inviter's profile, by name for the same reason: /users/<name> is the
// path the "profile" route happens to have today, and a hand-written copy of a
// path goes stale silently — the row above it is what that looks like.
function inviterRoute(username: string): RouteLocationRaw {
  return { name: "profile", params: { username } };
}

function typeLabel(type: ReceivedInvitation["type"]): string {
  switch (type) {
    case "player":
      return "Игрок";
    case "assistant":
      return "Помощник";
    case "reader":
      return "Читатель";
  }
}

async function accept(invitationId: string) {
  processingId.value = invitationId;
  const { error } = await personalApi.acceptInvitation(invitationId);
  processingId.value = null;

  if (error) {
    notifyFailure(error, "Не удалось принять приглашение");
  } else {
    invitations.value = invitations.value.filter((i) => i.id !== invitationId);
    toast.success("Приглашение принято");
  }
}

async function reject(invitationId: string) {
  processingId.value = invitationId;
  const { error } = await personalApi.rejectInvitation(invitationId);
  processingId.value = null;

  if (error) {
    notifyFailure(error, "Не удалось отклонить приглашение");
  } else {
    invitations.value = invitations.value.filter((i) => i.id !== invitationId);
    toast.success("Приглашение отклонено");
  }
}
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *

.invitations-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.loading-state
  padding: $medium

.invitations-list
  display: flex
  flex-direction: column
  gap: $small

.invitation-item
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small $medium
  background-color: $bg
  border-radius: $border-radius
  border: 1px solid $border
  gap: $medium

  @media (max-width: $bp-tablet)
    flex-direction: column
    align-items: stretch

.invitation-info
  display: flex
  flex-direction: column
  gap: $tiny
  flex: 1
  min-width: 0

.invitation-header
  display: flex
  align-items: center
  gap: $small

.invitation-icon
  font-size: 1.2em

.invitation-game-link
  font-weight: 500
  color: $link
  text-decoration: none
  overflow: hidden
  text-overflow: ellipsis
  white-space: nowrap

  &:hover
    text-decoration: underline

.invitation-details
  display: flex
  flex-wrap: wrap
  font-size: $secondary-font-size
  color: $text-muted

.invitation-from
  display: flex
  gap: 4px

.invitation-user-link
  color: $link
  text-decoration: none

  &:hover
    text-decoration: underline

.meta-sep
  color: $text-muted

.invitation-actions
  display: flex
  gap: $small
  flex-shrink: 0

  @media (max-width: $bp-tablet)
    justify-content: flex-end
    margin-top: $small

.action-btn
  background: none
  padding: $tiny $small
  border-radius: $border-radius
  cursor: pointer
  font-size: $secondary-font-size
  transition: opacity 0.15s ease

  &:disabled
    opacity: 0.5
    cursor: default

  &--accept
    border: 1px solid $accent-green
    color: $accent-green

    &:hover:not(:disabled)
      +tint($accent-green, 15%)

  &--reject
    border: 1px solid $border
    color: $text

    &:hover:not(:disabled)
      +tint($text-muted, 15%)
</style>
