<script setup lang="ts">
/**
 * The staff of the game. Inviting and removing an assistant are the master's
 * alone; everyone else the page admits reads the roster. A sent invitation is
 * answered by its row under the roster, not by a plate.
 */
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi, type Invitation } from "@/entities/game";
import { UserAutocomplete, UserLink } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

defineProps<{
  /** Whether the viewer may invite and remove assistants (the master alone). */
  canManageRoles: boolean;
}>();

const store = useGameDetailsStore();
const { game } = storeToRefs(store);

const assistants = computed(() => game.value?.fullAssistants ?? []);
const mentor = computed(() => game.value?.mentor ?? null);

const inviteUsername = ref("");
const inviting = ref(false);
const pendingRemove = ref<{ username: string } | null>(null);

const pending = ref<Invitation[]>([]);

const sentAt = (invitation: Invitation) =>
  new Date(invitation.createdUtc).getTime();

const sentLabel = (invitation: Invitation) =>
  `отправлено ${formatDateFull(invitation.createdUtc)}`;

// Not the game's pendingAssistant, which holds one name of several.
async function loadPending() {
  if (!game.value) return;
  const { data, error } = await gameApi.getGameInvitations(game.value.publicId);
  if (error) return;
  pending.value = (data?.resources ?? [])
    .filter((invitation) => invitation.type === "assistant")
    .sort((a, b) => sentAt(a) - sentAt(b));
}

onMounted(loadPending);

const inviteResult = useActionResult();

async function invite() {
  if (!game.value || !inviteUsername.value.trim()) return;
  inviting.value = true;
  inviteResult.clear();
  const { data, error } = await gameApi.inviteAssistant(
    game.value.id,
    inviteUsername.value.trim(),
  );
  inviting.value = false;
  if (error) {
    inviteResult.refuse(error, "Не удалось пригласить ассистента");
    return;
  }
  // A second invitation replaces the first on the server.
  if (data) {
    pending.value = [
      ...pending.value.filter(
        (i) => i.invitedUser.username !== data.invitedUser.username,
      ),
      data,
    ];
  }
  inviteUsername.value = "";
  await loadPending();
}

// The window prints its own refusal, so the interceptor keeps quiet.
async function confirmRemove() {
  const target = pendingRemove.value;
  if (!game.value || !target) return;
  const result = await gameApi.removeAssistant(
    game.value.id,
    target.username,
    OWNS_REFUSAL,
  );
  if (!result.error) void store.loadGame(game.value.publicId);
  return result;
}
</script>

<template>
  <SettingsSection title="Управление ролями">
    <div class="role-block">
      <div class="role-label">Ассистенты</div>
      <ul v-if="assistants.length" class="staff-list">
        <li v-for="a in assistants" :key="a.id" class="staff-item">
          <UserLink :user="a" hide-badge />
          <RemoveButton
            v-if="canManageRoles"
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

      <div v-if="canManageRoles" class="invite-row">
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
      <ActionResult v-if="canManageRoles" :result="inviteResult" />
    </div>

    <div class="role-block">
      <div class="role-label">Наставник (премодерация)</div>
      <UserLink v-if="mentor" :user="mentor" hide-badge />
      <SecondaryText v-else>Наставник не назначен</SecondaryText>
    </div>

    <ConfirmDialog
      :show="!!pendingRemove"
      title="Удалить ассистента?"
      message="Ассистент потеряет права в игре"
      confirm-label="Удалить"
      failure="Не удалось удалить ассистента"
      :action="confirmRemove"
      @update:show="(shown) => !shown && (pendingRemove = null)"
    />
  </SettingsSection>
</template>

<style scoped lang="sass">
.role-block
  & + &
    margin-top: $medium
    padding-top: $medium
    border-top: 1px solid $border

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
