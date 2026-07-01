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
                :to="`/games/${invitation.gameId}`"
                class="invitation-game-link"
              >
                {{ invitation.gameTitle }}
              </RouterLink>
            </div>
            <div class="invitation-details">
              <span class="invitation-from">
                От:
                <RouterLink
                  :to="`/users/${invitation.inviterUsername}`"
                  class="invitation-user-link"
                >
                  {{ invitation.inviterUsername }}
                </RouterLink>
              </span>
              <span class="invitation-role">{{
                typeLabel(invitation.type)
              }}</span>
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
import dayjs from "dayjs";
import { RouterLink } from "vue-router";
import { AccountApi } from "@/shared/api";
import { useToast } from "@/shared/lib/composables/useToast";
import type { Invitation, InvitationType } from "@/entities/game";

const toast = useToast();

const invitations = ref<Invitation[]>([]);
const loading = ref(true);
const processingId = ref<string | null>(null);

onMounted(async () => {
  await loadInvitations();
});

async function loadInvitations() {
  loading.value = true;
  const { data, error } = await AccountApi.getMyInvitations();
  loading.value = false;

  if (!error && data) {
    invitations.value = data.resources;
  }
}

function typeLabel(type: InvitationType): string {
  switch (type) {
    case "player":
      return "Игрок";
    case "assistant":
      return "Помощник";
    case "reader":
      return "Читатель";
  }
}

function formatDate(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY");
}

async function accept(invitationId: string) {
  processingId.value = invitationId;
  const { error } = await AccountApi.acceptInvitation(invitationId);
  processingId.value = null;

  if (error) {
    toast.error("Не удалось принять приглашение");
  } else {
    invitations.value = invitations.value.filter((i) => i.id !== invitationId);
    toast.success("Приглашение принято");
  }
}

async function reject(invitationId: string) {
  processingId.value = invitationId;
  const { error } = await AccountApi.rejectInvitation(invitationId);
  processingId.value = null;

  if (error) {
    toast.error("Не удалось отклонить приглашение");
  } else {
    invitations.value = invitations.value.filter((i) => i.id !== invitationId);
    toast.success("Приглашение отклонено");
  }
}
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "../AccountPage.styles"

.invitations-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.loading-state
  color: $text-muted
  text-align: center
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

  @media (max-width: 768px)
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
  gap: $small
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

.invitation-role
  &::before
    content: "\2022"
    margin-right: $small

.invitation-date
  &::before
    content: "\2022"
    margin-right: $small

.invitation-actions
  display: flex
  gap: $small
  flex-shrink: 0

  @media (max-width: 768px)
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
      background-color: rgba($accent-green, 0.1)

  &--reject
    border: 1px solid $border
    color: $text-muted

    &:hover:not(:disabled)
      background-color: rgba($text-muted, 0.1)
</style>
