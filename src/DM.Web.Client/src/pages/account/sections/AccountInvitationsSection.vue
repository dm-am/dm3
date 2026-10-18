<template>
  <section v-if="invitations.length > 0 || loading" class="section">
    <h2 class="section-title">Приглашения</h2>

    <div class="invitations-content">
      <div v-if="loading" class="loading-state">Загрузка...</div>

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
              <!-- A caption line of the site: where it is short it wraps
                   between its parts, and every part stands whole, the name
                   of the inviter too. -->
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
                  typeLabel(invitation.type)
                }}</span>
                <span class="invitation-date">{{
                  formatDate(invitation.createdUtc)
                }}</span>
              </CaptionLine>
            </div>

            <!--
            The answer to the press stands where the buttons stood, and the row
            stays put. Accepting has a half the screen never showed: the server
            hands over the role in the game or the blog and answers 204 with no
            body (Personal/Invitations/InvitationController.AcceptInvitation),
            so the row says which role that was, while the title above it stays
            a link into what the viewer has just joined.
          -->
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
                class="action-btn--accept"
                :disabled="processingId === invitation.id"
                @click="accept(invitation.id)"
              >
                Принять
              </Button>
              <Button
                type="button"
                class="action-btn--reject"
                :disabled="processingId === invitation.id"
                @click="reject(invitation.id)"
              >
                Отклонить
              </Button>
            </div>
          </div>
          <!-- A refused answer, under the row it was given in. -->
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
import { personalApi } from "@/entities/user";
import { formatDate } from "@/shared/lib/utils/datetime";
import type { ReceivedInvitation } from "@/shared/api/models/personal";
import { refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { CaptionLine } from "@/shared/ui/CaptionLine";

// The shape the endpoint actually answers. The section used to type it as the
// game-side Invitation (gameId / gameTitle), so every row rendered an empty
// link — a zero-width anchor with no name in it — pointing at /games/undefined.
const invitations = ref<ReceivedInvitation[]>([]);
const loading = ref(true);
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

/**
 * What accepting did, in the row it was done in.
 *
 * The role is named with the same word the row itself uses for it, lowered to
 * sit inside a sentence: a second table of names here would be a table that
 * drifts from the one above.
 */
function acceptedLabel(invitation: ReceivedInvitation): string {
  return `Вы приняты как ${typeLabel(invitation.type).toLowerCase()}`;
}

async function accept(invitationId: string) {
  processingId.value = invitationId;
  rowRefusal.value = null;
  const { error } = await personalApi.acceptInvitation(invitationId);
  processingId.value = null;

  if (error) {
    refuse(invitationId, refusalHere(error, "Не удалось принять приглашение"));
  } else {
    // No toast, and the row is not dropped either. Dropping it showed only the
    // half that undoes itself — the invitation is gone — and said nothing about
    // the half that lasts, so the message had to say it instead. The row now
    // carries both: the role received, and the link to where it is held.
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
    // No toast: the row leaves the list under the button that was pressed.
    invitations.value = invitations.value.filter((i) => i.id !== invitationId);
  }
}
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *

.invitations-content
  padding: $medium
  background-color: $bg-element

.loading-state
  padding: $medium

.invitations-list
  display: flex
  flex-direction: column
  gap: $small

// Spaced by the gap of the list, at the left edge of the row above it.
.invitation-refusal
  --action-result-margin: 0

.invitation-item
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small $medium
  background-color: $bg
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

.invitation-icon
  font-size: 1.2em

// The one bold link weight of the site; colour and hover are the global link.
// A long title wraps, whole, rather than losing its end under "...". As an item
// of the flex header the link is never narrower than its longest word, and
// break-word does not lower that, so a title of one word wider than the card
// went past its edge: min-width 0 lets the base rule break the word instead.
.invitation-target-link
  min-width: 0
  font-weight: 700

.invitation-details
  display: block

// Sits in the place the buttons held, so the answer is where the press was.
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
