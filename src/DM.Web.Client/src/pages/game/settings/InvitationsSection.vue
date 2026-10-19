<script setup lang="ts">
/**
 * The pending invitations of the game. The read asks EditSettings, inviting
 * and cancelling the narrower `canManage`. A sent invitation is
 * answered by its row in the list, not by a plate.
 */
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  gameApi,
  INVITATION_TYPE_LABELS,
  type Invitation,
} from "@/entities/game";
import { UserAutocomplete, UserLink } from "@/entities/user";
import { Select } from "@/shared/ui/Select";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { refusalHere } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ErrorState } from "@/shared/ui/ErrorState";
import { shownOnPage } from "@/shared/lib/composables/useGuardedRequest";

defineProps<{
  /** Whether the viewer may send and cancel invitations. */
  canManage: boolean;
}>();

const store = useGameDetailsStore();
const { game } = storeToRefs(store);

const invitations = ref<Invitation[]>([]);
/** The list has been read once: until then there is nothing to keep or to call empty. */
const answered = ref(false);
const loadFailed = ref(false);
const LOAD_FAILED = "Не удалось загрузить приглашения";

const username = ref("");
const kind = ref<"player" | "reader">("player");
const inviting = ref(false);

const kindOptions = (["player", "reader"] as const).map((value) => ({
  value,
  label: INVITATION_TYPE_LABELS[value],
}));

// Oldest first, the one just sent next to the controls; the server sends no
// order at all (GameInvitationRepository.GetPendingInvitations).
const sentAt = (invitation: Invitation) =>
  new Date(invitation.createdUtc).getTime();

const sorted = computed(() =>
  [...invitations.value].sort((a, b) => sentAt(a) - sentAt(b)),
);

const sentLabel = (invitation: Invitation) =>
  `отправлено ${formatDateFull(invitation.createdUtc)}`;

const gameGuid = computed(() => game.value?.id ?? "");

async function load() {
  if (!game.value) return;
  const answer = await gameApi.getGameInvitations(game.value.publicId);
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

// A second invitation of a kind replaces the first on the server
// (GameInvitationRepository.InvalidateAndCreateInvitation).
function remember(invitation: Invitation) {
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
  inviting.value = true;
  inviteResult.clear();
  const { data, error } =
    kind.value === "player"
      ? await gameApi.invitePlayer(gameGuid.value, username.value.trim())
      : await gameApi.inviteReader(gameGuid.value, username.value.trim());
  inviting.value = false;
  if (error) {
    inviteResult.refuse(error, "Не удалось отправить приглашение");
    return;
  }
  if (data) remember(data);
  username.value = "";
  await load();
}

async function cancel(invitation: Invitation) {
  cancelRefusal.value = null;
  const { error } = await gameApi.cancelInvitation(
    gameGuid.value,
    invitation.id,
  );
  if (error) {
    const text = refusalHere(error, "Не удалось отменить приглашение");
    if (text) cancelRefusal.value = { id: invitation.id, text };
    return;
  }
  await load();
}
</script>

<template>
  <SettingsSection title="Приглашения">
    <ul v-if="sorted.length" class="inv-list">
      <li v-for="inv in sorted" :key="inv.id" class="inv-item">
        <div class="inv-row">
          <UserLink :user="inv.invitedUser" hide-badge />
          <span class="inv-type">{{ INVITATION_TYPE_LABELS[inv.type] }}</span>
          <span class="inv-sent">{{ sentLabel(inv) }}</span>
          <RemoveButton v-if="canManage" @click="cancel(inv)">
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

    <div v-if="canManage" class="invite-row">
      <UserAutocomplete
        v-model="username"
        class="invite-row__who"
        placeholder="Имя пользователя"
      />
      <Select
        :model-value="kind"
        :options="kindOptions"
        aria-label="Тип приглашения"
        @update:model-value="(v) => (kind = v as 'player' | 'reader')"
      />
      <Button
        type="button"
        variant="primary"
        :loading="inviting"
        :disabled="!username.trim()"
        @click="invite"
      >
        Пригласить
      </Button>
    </div>
    <ActionResult v-if="canManage" :result="inviteResult" />
  </SettingsSection>
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
  display: grid
  grid-template-columns: minmax(0, 1fr) auto auto
  align-items: start
  gap: $small
</style>
