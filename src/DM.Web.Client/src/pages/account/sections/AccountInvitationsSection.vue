<template>
  <section
    v-if="invitations.length > 0 || loading || loadError"
    class="section"
  >
    <h2 class="section-title">Приглашения</h2>

    <div class="invitations-content">
      <AccountSectionSkeleton v-if="loading" shape="rows" :count="1" />

      <ErrorState
        v-else-if="loadError"
        :message="loadError"
        :retry="loadInvitations"
      />

      <div v-else class="invitations-list">
        <template v-for="invitation in invitations" :key="invitation.id">
          <div class="invitation-item">
            <div class="invitation-info">
              <div class="invitation-header">
                <RouterLink
                  :to="targetRoute(invitation)"
                  class="invitation-target-link"
                >
                  {{ invitation.entityTitle }}
                </RouterLink>
              </div>
              <!-- A caption line of the site: it wraps between its parts, each whole. -->
              <CaptionLine size="secondary" class="invitation-details">
                <span class="invitation-from"
                  >От:{{ " "
                  }}<RouterLink
                    :to="inviterRoute(invitation.inviterUsername)"
                    class="invitation-user-link"
                    >{{ invitation.inviterUsername }}</RouterLink
                  ></span
                >
                <span class="invitation-role">{{
                  INVITATION_TYPE_LABELS[invitation.type]
                }}</span>
                <span class="invitation-date">{{
                  formatDate(invitation.createdUtc)
                }}</span>
              </CaptionLine>
            </div>

            <!-- The answer stands where the buttons stood: the server grants the role and
                 answers 204, so the row names the role, the title still a link into it. -->
            <p
              v-if="acceptedIds.includes(invitation.id)"
              class="invitation-accepted"
              role="status"
            >
              {{ acceptedLabel(invitation) }}
            </p>

            <div v-else class="invitation-actions">
              <Button
                type="button"
                :disabled="processingId === invitation.id"
                @click="accept(invitation.id)"
              >
                Принять
              </Button>
              <Button
                type="button"
                :disabled="processingId === invitation.id"
                @click="reject(invitation.id)"
              >
                Отклонить
              </Button>
            </div>
          </div>
          <!-- A refusal, under the row it was given in. -->
          <ActionResult
            v-if="rowRefusal?.id === invitation.id"
            :text="rowRefusal.text"
            class="invitation-refusal"
          />
        </template>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { RouterLink, type RouteLocationRaw } from "vue-router";
import { INVITATION_TYPE_LABELS } from "@/entities/game";
import { personalApi } from "@/entities/user";
import { formatDate } from "@/shared/lib/utils/datetime";
import type { ReceivedInvitation } from "@/shared/api/models/personal";
import { refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { CaptionLine } from "@/shared/ui/CaptionLine";
import { ErrorState } from "@/shared/ui/ErrorState";
import AccountSectionSkeleton from "./AccountSectionSkeleton.vue";
import { OWNED_BY_CARD } from "./loadFailure";

const LOAD_FAILED = "Не удалось загрузить приглашения";

// The shape the endpoint answers (entityId, entityType, entityTitle).
const invitations = ref<ReceivedInvitation[]>([]);
const loading = ref(true);
const loadError = ref<string | null>(null);
const processingId = ref<string | null>(null);

/** Invitations accepted here, by id: their rows stay and show what came of it. */
const acceptedIds = ref<string[]>([]);

/** The last refused answer, under the row of the invitation it was about. */
const rowRefusal = ref<{ id: string; text: string } | null>(null);

function refuse(id: string, text: string | null) {
  rowRefusal.value = text ? { id, text } : null;
}

onMounted(async () => {
  await loadInvitations();
});

async function loadInvitations() {
  loading.value = true;
  const { data, error } = await personalApi.getMyInvitations(OWNED_BY_CARD);
  loading.value = false;

  loadError.value = !error && data ? null : LOAD_FAILED;
  if (!error && data) {
    invitations.value = data.resources;
  }
}

// A named route per entity: the endpoint answers game and blog invitations together.
function targetRoute(invitation: ReceivedInvitation): RouteLocationRaw {
  return invitation.entityType === "blog"
    ? { name: "blog", params: { id: invitation.entityId } }
    : { name: "game", params: { id: invitation.entityId } };
}

// The inviter's profile by route name, not by a written path.
function inviterRoute(username: string): RouteLocationRaw {
  return { name: "profile", params: { username } };
}

/** What accepting did, in the word the row uses for the role. */
function acceptedLabel(invitation: ReceivedInvitation): string {
  return `Вы приняты как ${INVITATION_TYPE_LABELS[invitation.type].toLowerCase()}`;
}

async function accept(invitationId: string) {
  processingId.value = invitationId;
  rowRefusal.value = null;
  const { error } = await personalApi.acceptInvitation(invitationId);
  processingId.value = null;

  if (error) {
    refuse(invitationId, refusalHere(error, "Не удалось принять приглашение"));
  } else {
    // No toast, and the row stays: it says the role received and links to it.
    acceptedIds.value = [...acceptedIds.value, invitationId];
  }
}

async function reject(invitationId: string) {
  processingId.value = invitationId;
  rowRefusal.value = null;
  const { error } = await personalApi.rejectInvitation(invitationId);
  processingId.value = null;

  if (error) {
    refuse(
      invitationId,
      refusalHere(error, "Не удалось отклонить приглашение"),
    );
  } else {
    // No toast: the row leaves under the pressed button.
    invitations.value = invitations.value.filter((i) => i.id !== invitationId);
  }
}
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *

.invitations-content
  padding: $medium
  background-color: $bg-element

.invitations-list
  display: flex
  flex-direction: column
  gap: $small

// Spaced by the gap of the list.
.invitation-refusal
  --action-result-margin: 0

.invitation-item
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small $medium
  background-color: $bg-element
  border: 1px solid $border
  gap: $medium

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

// The site's bold link; a long title wraps whole, and min-width 0 lets a one-word title break inside the card.
.invitation-target-link
  min-width: 0
  font-weight: 700

.invitation-details
  display: block

// Where the buttons stood, so the answer is where the press was.
.invitation-accepted
  margin: 0
  flex-shrink: 0
  font-size: $secondary-font-size
  color: $accent-green

.invitation-actions
  display: flex
  gap: $small
  flex-shrink: 0
</style>
