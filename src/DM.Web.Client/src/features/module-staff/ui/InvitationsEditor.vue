<script setup lang="ts">
/**
 * The outstanding invitations of a game or a blog, the field that sends one
 * and the cancel of each. A sent invitation says nothing in the corner: its row
 * stands right above the control.
 */
import { computed, onMounted, ref } from "vue";
import { INVITATION_TYPE_LABELS } from "@/entities/game";
import { UserAutocomplete, UserLink } from "@/entities/user";
import type { ListEnvelope } from "@/shared/api/models/common";
import { refusalHere } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { shownOnPage } from "@/shared/lib/composables/useGuardedRequest";
import Button from "@/shared/ui/Button/Button.vue";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ErrorState } from "@/shared/ui/ErrorState";
import {
  bySentTime,
  sentLabel,
  type ModuleInvitation,
  type ModuleRequest,
} from "../model/invitations";

const props = defineProps<{
  canInvite: boolean;
  canCancel: boolean;
  inviteLabel: string;
  loadInvitations: () => ModuleRequest<ListEnvelope<ModuleInvitation>>;
  sendInvitation: (username: string) => ModuleRequest<ModuleInvitation>;
  cancelInvitation: (id: string) => ModuleRequest<unknown>;
}>();

defineSlots<{
  /** A picker of the kind sent, between the field and the button. */
  kind?: () => unknown;
}>();

const invitations = ref<ModuleInvitation[]>([]);
/** The list has been read once: until then there is nothing to keep or to call empty. */
const answered = ref(false);
const loadFailed = ref(false);
const LOAD_FAILED = "Не удалось загрузить приглашения";

const username = ref("");
const inviting = ref(false);

const sorted = computed(() => [...invitations.value].sort(bySentTime));

async function load() {
  const request = props.loadInvitations();
  if (!request) return;
  const answer = await request;
  if (answer.error) {
    // A failed reload leaves the list alone, so a blip keeps a sent invitation;
    // a failed first read says so in the list's place.
    if (!answered.value) {
      shownOnPage(answer);
      loadFailed.value = true;
    }
    return;
  }
  answered.value = true;
  loadFailed.value = false;
  invitations.value = answer.data?.resources ?? [];
}

onMounted(load);

/**
 * The server kills the live invitation of the same kind to the same person
 * before issuing this one, so a second invitation replaces the row. Taken from
 * the answer, the row survives a failed reload.
 */
function remember(invitation: ModuleInvitation) {
  invitations.value = [
    ...invitations.value.filter(
      (i) =>
        !(
          i.invitedUser.username === invitation.invitedUser.username &&
          i.type === invitation.type
        ),
    ),
    invitation,
  ];
}

const inviteResult = useActionResult();

const cancelRefusal = ref<{ id: string; text: string } | null>(null);

async function invite() {
  if (!username.value.trim()) return;
  const request = props.sendInvitation(username.value.trim());
  if (!request) return;
  inviting.value = true;
  inviteResult.clear();
  const { data, error } = await request;
  inviting.value = false;
  if (error) {
    inviteResult.refuse(error, "Не удалось отправить приглашение");
    return;
  }
  if (data) remember(data);
  username.value = "";
  await load();
}

async function cancel(invitation: ModuleInvitation) {
  const request = props.cancelInvitation(invitation.id);
  if (!request) return;
  cancelRefusal.value = null;
  const { error } = await request;
  if (error) {
    const text = refusalHere(error, "Не удалось отменить приглашение");
    if (text) cancelRefusal.value = { id: invitation.id, text };
    return;
  }
  await load();
}
</script>

<template>
  <ul v-if="sorted.length" class="inv-list">
    <li v-for="inv in sorted" :key="inv.id" class="inv-item">
      <div class="inv-row">
        <UserLink :user="inv.invitedUser" hide-badge />
        <span class="inv-type">{{ INVITATION_TYPE_LABELS[inv.type] }}</span>
        <span class="inv-sent">{{ sentLabel(inv) }}</span>
        <RemoveButton v-if="canCancel" @click="cancel(inv)">
          Отменить
        </RemoveButton>
      </div>
      <ActionResult
        v-if="cancelRefusal?.id === inv.id"
        :text="cancelRefusal.text"
        class="inv-refusal"
      />
    </li>
  </ul>
  <div
    v-else-if="!answered && !loadFailed"
    class="inv-waiting"
    aria-hidden="true"
  />
  <ErrorState v-else-if="!answered" :message="LOAD_FAILED" :retry="load" />
  <SecondaryText v-else>Активных приглашений нет</SecondaryText>

  <div
    v-if="canInvite"
    class="invite-row"
    :class="$slots.kind ? 'invite-row--kind' : 'invite-row--alone'"
  >
    <UserAutocomplete
      v-model="username"
      class="invite-row__who"
      placeholder="Имя пользователя"
    />
    <slot name="kind" />
    <Button
      type="button"
      variant="primary"
      :loading="inviting"
      :disabled="!username.trim()"
      @click="invite"
    >
      {{ inviteLabel }}
    </Button>
  </div>
  <ActionResult v-if="canInvite" :result="inviteResult" />
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.inv-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

// The line of "Активных приглашений нет", the answer most sections get.
.inv-waiting
  width: 40%
  height: 1.3 * $secondary-font-size
  +skeleton-shimmer

.inv-row
  display: flex
  align-items: center
  gap: $small

.inv-refusal
  --action-result-margin: #{$tiny} 0 0

.inv-type,
.inv-sent
  color: $text-muted
  font-size: $secondary-font-size

.invite-row
  gap: $small

// The field takes what the button leaves, down to 80.
.invite-row--alone
  display: flex
  align-items: flex-start

  .invite-row__who
    flex: 1 1 0
    min-width: $grid-step * 20

.invite-row--kind
  display: grid
  grid-template-columns: minmax(0, 1fr) auto auto
  align-items: start
</style>
