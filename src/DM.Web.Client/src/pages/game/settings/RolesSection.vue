<script setup lang="ts">
/**
 * RolesSection — manage game staff. Both staff operations are the master's
 * alone: GameIntention.InviteAssistant and GameIntention.RemoveUser ask for
 * GameRole.Master and admit nobody else, not even a senior moderator. The host
 * page computes that one flag and passes it in; everyone else the settings page
 * admits reads the roster without controls, which is what the roster costs —
 * a plain GameIntention.Read on the game the page already loaded. The mentor
 * (premoderation curator) is shown read-only in either case — there is no
 * per-game mentor-assignment endpoint.
 *
 * Inviting an assistant says nothing in the corner and cannot say it with the
 * roster either: the invited person joins the list of assistants only once the
 * invitation is accepted. What it changes is the block below the roster, which
 * names everyone whose invitation is out and when it was sent (see
 * `loadPending`). The blog RolesSection is built the same way.
 */
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi, type Invitation } from "@/entities/game";
import { UserLink } from "@/entities/user";
import { UserAutocomplete } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

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

/** When the invitation went out, in the row that stands for it. */
const sentLabel = (invitation: Invitation) =>
  `отправлено ${formatDateFull(invitation.createdUtc)}`;

/**
 * The assistant invitations that are still out, oldest first, so the one just
 * sent is the row next to the control that sent it.
 *
 * Read from the invitation endpoint rather than from the game, although the
 * game DTO carries a pending assistant of its own: that field holds one name,
 * picked with FirstOrDefault out of an unordered token read
 * (GameRepository), so a master who invites a second assistant while the
 * first invitation is out sees the line keep the other person's name - the
 * invitation he has just sent, silently absent. The read asks
 * GameIntention.EditSettings, which is also what admits a viewer to the
 * settings page, so everyone who is shown this section may make it.
 */
async function loadPending() {
  if (!game.value) return;
  const { data, error } = await gameApi.getGameInvitations(
    game.value.publicId ?? game.value.id,
  );
  // A read that failed leaves the block as it is rather than emptying it over
  // an invitation that is out.
  if (error) return;
  pending.value = (data?.resources ?? [])
    .filter((invitation) => invitation.type === "assistant")
    .sort((a, b) => sentAt(a) - sentAt(b));
}

onMounted(loadPending);

/** A refused invitation, under the row it was typed in, the name kept. */
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
  // The row from the server's own answer, in place of the invitation it has
  // just replaced: a second invitation to the same person invalidates the
  // first (GameInvitationRepository.InvalidateAndCreateInvitation), so what
  // changes in the block is the moment it was sent. Put in before the reload,
  // which is then free to fail without taking the row with it.
  if (data) {
    pending.value = [
      ...pending.value.filter(
        (i) => i.invitedUser.username !== data.invitedUser.username,
      ),
      data,
    ];
  }
  inviteUsername.value = "";
  // Not the game: an invitation adds nobody to the roster, and the roster is
  // what the game carries.
  await loadPending();
}

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmRemove() {
  const target = pendingRemove.value;
  if (!game.value || !target) return;
  const result = await gameApi.removeAssistant(game.value.id, target.username, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (!result.error) void store.loadGame(game.value.publicId ?? game.value.id);
  return result;
}
</script>

<template>
  <SettingsSection title="Управление ролями">
    <div class="role-block">
      <div class="role-label">Ассистенты</div>
      <ul v-if="assistants.length" class="staff-list">
        <li v-for="a in assistants" :key="a.id" class="staff-item">
          <UserLink :user="a" />
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
            <UserLink :user="inv.invitedUser" />
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
      <UserLink v-if="mentor" :user="mentor" />
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
