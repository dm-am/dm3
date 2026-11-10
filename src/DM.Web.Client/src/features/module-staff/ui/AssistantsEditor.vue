<script setup lang="ts">
/**
 * The assistants of a game or a blog: the roster, the invitations still out
 * and the field that sends one. An invited assistant joins the roster only on
 * acceptance, so a sent invitation is answered by its row, not by a plate.
 */
import { onMounted, ref } from "vue";
import { UserAutocomplete, UserLink } from "@/entities/user";
import type { ListEnvelope, User, UserRef } from "@/shared/api/models/common";
import type { DialogActionResult } from "@/shared/lib/composables/useDialogAction";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { ActionResult } from "@/shared/ui/ActionResult";
import {
  bySentTime,
  sentLabel,
  type ModuleInvitation,
  type ModuleRequest,
} from "../model/invitations";

const props = defineProps<{
  assistants: readonly (User | UserRef)[];
  canInvite: boolean;
  canRemove: boolean;
  /** The confirmation's line on what the removed assistant loses. */
  removalMessage: string;
  loadInvitations: () => ModuleRequest<ListEnvelope<ModuleInvitation>>;
  sendInvitation: (username: string) => ModuleRequest<ModuleInvitation>;
  /** The window waits for it, prints a refusal in itself and closes on success alone. */
  removeAssistant: (username: string) => Promise<DialogActionResult>;
}>();

const inviteUsername = ref("");
const inviting = ref(false);
const pendingRemove = ref<{ username: string } | null>(null);

const pending = ref<ModuleInvitation[]>([]);

// The module's own record stops at accepted assistants.
async function loadPending() {
  const request = props.loadInvitations();
  if (!request) return;
  const { data, error } = await request;
  // A failed read leaves the block as it is.
  if (error) return;
  pending.value = (data?.resources ?? [])
    .filter((invitation) => invitation.type === "assistant")
    .sort(bySentTime);
}

onMounted(loadPending);

const inviteResult = useActionResult();

async function invite() {
  if (!inviteUsername.value.trim()) return;
  const request = props.sendInvitation(inviteUsername.value.trim());
  if (!request) return;
  inviting.value = true;
  inviteResult.clear();
  const { data, error } = await request;
  inviting.value = false;
  if (error) {
    inviteResult.refuse(error, "Не удалось пригласить ассистента");
    return;
  }
  // A second invitation replaces the first on the server; the row from the
  // answer is put in before the reload, which may then fail.
  if (data) {
    pending.value = [
      ...pending.value.filter(
        (i) => i.invitedUser.username !== data.invitedUser.username,
      ),
      data,
    ];
  }
  inviteUsername.value = "";
  // Not the module: an invitation adds nobody to the roster.
  await loadPending();
}

async function confirmRemove() {
  const target = pendingRemove.value;
  if (!target) return;
  return props.removeAssistant(target.username);
}
</script>

<template>
  <div>
    <div class="role-label">Ассистенты</div>
    <ul v-if="assistants.length" class="staff-list">
      <li v-for="a in assistants" :key="a.id" class="staff-item">
        <UserLink :user="a" hide-badge />
        <RemoveButton
          v-if="canRemove"
          @click="pendingRemove = { username: a.username }"
        >
          Удалить
        </RemoveButton>
      </li>
    </ul>
    <SecondaryText v-else>Ассистентов нет</SecondaryText>

    <div v-if="pending.length" class="pending">
      <div class="role-label">Ожидают подтверждения</div>
      <ul class="staff-list">
        <li v-for="inv in pending" :key="inv.id" class="staff-item">
          <UserLink :user="inv.invitedUser" hide-badge />
          <span class="sent">{{ sentLabel(inv) }}</span>
        </li>
      </ul>
    </div>

    <div v-if="canInvite" class="invite-row">
      <UserAutocomplete
        v-model="inviteUsername"
        placeholder="Имя пользователя"
      />
      <Button
        type="button"
        variant="primary"
        :loading="inviting"
        :disabled="!inviteUsername.trim()"
        @click="invite"
      >
        Пригласить
      </Button>
    </div>
    <ActionResult v-if="canInvite" :result="inviteResult" />
  </div>

  <ConfirmDialog
    :show="!!pendingRemove"
    title="Удалить ассистента?"
    :message="removalMessage"
    confirm-label="Удалить"
    failure="Не удалось удалить ассистента"
    :action="confirmRemove"
    @update:show="(shown) => !shown && (pendingRemove = null)"
  />
</template>

<style scoped lang="sass">
.role-label
  color: $text-muted
  font-size: $secondary-font-size
  margin-bottom: $small

.staff-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny

.staff-item
  display: flex
  align-items: center
  gap: $small

.pending
  margin-top: $small

.sent
  color: $text-muted
  font-size: $secondary-font-size

.invite-row
  display: flex
  align-items: flex-start
  gap: $small
  margin-top: $small
</style>
