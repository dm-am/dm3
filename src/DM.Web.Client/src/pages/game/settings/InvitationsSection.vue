<script setup lang="ts">
/**
 * The pending invitations of the game. The read asks EditSettings, inviting
 * and cancelling the narrower `canManage`.
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  gameApi,
  INVITATION_TYPE_LABELS,
} from "@/entities/game";
import { InvitationsEditor } from "@/features/module-staff";
import { Select } from "@/shared/ui/Select";
import { SettingsSection } from "@/shared/ui/SettingsSection";

defineProps<{
  /** Whether the viewer may send and cancel invitations. */
  canManage: boolean;
}>();

const store = useGameDetailsStore();
const { game } = storeToRefs(store);

const kind = ref<"player" | "reader">("player");

const kindOptions = (["player", "reader"] as const).map((value) => ({
  value,
  label: INVITATION_TYPE_LABELS[value],
}));

const gameGuid = computed(() => game.value?.id ?? "");

// The server sends no order at all (GameInvitationRepository.GetPendingInvitations).
const loadInvitations = () =>
  game.value ? gameApi.getGameInvitations(game.value.publicId) : null;

// A second invitation of a kind replaces the first on the server
// (GameInvitationRepository.InvalidateAndCreateInvitation).
const sendInvitation = (username: string) =>
  kind.value === "player"
    ? gameApi.invitePlayer(gameGuid.value, username)
    : gameApi.inviteReader(gameGuid.value, username);

const cancelInvitation = (id: string) =>
  gameApi.cancelInvitation(gameGuid.value, id);
</script>

<template>
  <SettingsSection title="Приглашения">
    <InvitationsEditor
      :can-invite="canManage"
      :can-cancel="canManage"
      invite-label="Пригласить"
      :load-invitations="loadInvitations"
      :send-invitation="sendInvitation"
      :cancel-invitation="cancelInvitation"
    >
      <template #kind>
        <Select
          :model-value="kind"
          :options="kindOptions"
          aria-label="Тип приглашения"
          @update:model-value="(v) => (kind = v as 'player' | 'reader')"
        />
      </template>
    </InvitationsEditor>
  </SettingsSection>
</template>
