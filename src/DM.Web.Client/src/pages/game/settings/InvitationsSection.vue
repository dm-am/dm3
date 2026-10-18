<script setup lang="ts">
/**
 * InvitationsSection — outstanding invitations for the game plus the controls
 * to invite a player or a reader and to cancel a pending invitation. Backed by
 * the invitation gameApi; the list is held locally (not part of the shared
 * game store).
 *
 * Reading the list and writing it part company on the server: the read asks
 * GameIntention.EditSettings (the leads, the curating mentor and senior
 * moderation), while invite and cancel ask InvitePlayer / InviteReader /
 * CancelInvitation — the game roles alone. `canManage` carries that narrower
 * set, and the host page is the one that computes it.
 *
 * Sending an invitation says nothing in the corner: the row for it is in the
 * list right above the controls, carrying the moment it was sent. That the row
 * is there at all takes three things, and each of them is a way the list used
 * to lose it - see `sorted`, `load` and `invite` below. The blog twin of this
 * section is built the same way.
 */
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi, type Invitation } from "@/entities/game";
import { UserLink } from "@/entities/user";
import { UserAutocomplete } from "@/entities/user";
import { Select } from "@/shared/ui/Select";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { refusalHere } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

defineProps<{
  /** Whether the viewer may send and cancel invitations. */
  canManage: boolean;
}>();

const store = useGameDetailsStore();
const { game } = storeToRefs(store);

const invitations = ref<Invitation[]>([]);
const loading = ref(false);

const username = ref("");
const kind = ref<"player" | "reader">("player");
const inviting = ref(false);

const kindOptions = [
  { value: "player", label: "Игрок" },
  { value: "reader", label: "Читатель" },
];

const typeLabels: Record<string, string> = {
  player: "Игрок",
  reader: "Читатель",
  assistant: "Ассистент",
};

/**
 * Oldest first, so the invitation just sent is the last row - the one next to
 * the controls that sent it.
 *
 * The order is the section's own, and here it is also the only one there is:
 * the endpoint reads the tokens of the game with no ORDER BY at all
 * (GameInvitationRepository.GetPendingInvitations), so served as they come the
 * rows land wherever the database happened to keep them.
 */
const sentAt = (invitation: Invitation) =>
  new Date(invitation.createdUtc).getTime();

const sorted = computed(() =>
  [...invitations.value].sort((a, b) => sentAt(a) - sentAt(b)),
);

/** When the invitation went out, in the row that stands for it. */
const sentLabel = (invitation: Invitation) =>
  `отправлено ${formatDateFull(invitation.createdUtc)}`;

const gameGuid = computed(() => game.value?.id ?? "");

async function load() {
  if (!game.value) return;
  loading.value = true;
  const { data, error } = await gameApi.getGameInvitations(
    game.value.publicId ?? game.value.id,
  );
  loading.value = false;
  // A read that failed leaves the list alone. Emptying it on an error meant
  // that a blip in the reload below answered a freshly sent invitation with
  // "Активных приглашений нет".
  if (error) return;
  invitations.value = data?.resources ?? [];
}

onMounted(load);

/**
 * The invitation the server has just answered with, put into the list in
 * place of the one it replaced.
 *
 * The server kills the live invitation of the same kind to the same person
 * before issuing this one (GameInvitationRepository.InvalidateAndCreate-
 * Invitation), so inviting somebody a second time is a replacement, not an
 * addition: what changes in the row is the moment it was sent. Taking the row
 * from the answer also means the reload that follows is free to fail.
 */
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

/** A refused invitation, under the row it was typed in, the name kept. */
const inviteResult = useActionResult();

/** The last refused "Отменить", under the row of the invitation it was about. */
const cancelRefusal = ref<{ id: string; text: string } | null>(null);

async function invite() {
  if (!gameGuid.value || !username.value.trim()) return;
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
  if (!gameGuid.value) return;
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
          <UserLink :user="inv.invitedUser" />
          <span class="inv-type">{{ typeLabels[inv.type] ?? inv.type }}</span>
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
    <SecondaryText v-else-if="!loading">Активных приглашений нет</SecondaryText>

    <!-- The field, the list and the button in one line, the field taking
         what the other two leave. -->
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
.inv-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

.inv-row
  display: flex
  align-items: center
  gap: $small

// Under the invitation it is about, one list step from it.
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
