<script setup lang="ts">
/**
 * The staff of the game. Inviting and removing an assistant are the master's
 * alone; everyone else the page admits reads the roster.
 */
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import { UserLink } from "@/entities/user";
import { AssistantsEditor } from "@/features/module-staff";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

defineProps<{
  /** Whether the viewer may invite and remove assistants (the master alone). */
  canManageRoles: boolean;
}>();

const store = useGameDetailsStore();
const { game } = storeToRefs(store);

const assistants = computed(() => game.value?.fullAssistants ?? []);
const mentor = computed(() => game.value?.mentor ?? null);

// Not the game's pendingAssistant, which holds one name of several.
const loadInvitations = () =>
  game.value ? gameApi.getGameInvitations(game.value.publicId) : null;

const sendInvitation = (username: string) =>
  game.value ? gameApi.inviteAssistant(game.value.id, username) : null;

async function removeAssistant(username: string) {
  if (!game.value) return;
  const result = await gameApi.removeAssistant(
    game.value.id,
    username,
    OWNS_REFUSAL,
  );
  if (!result.error) void store.loadGame(game.value.publicId);
  return result;
}
</script>

<template>
  <SettingsSection title="Управление ролями">
    <AssistantsEditor
      :assistants="assistants"
      :can-invite="canManageRoles"
      :can-remove="canManageRoles"
      removal-message="Ассистент потеряет права в игре"
      :load-invitations="loadInvitations"
      :send-invitation="sendInvitation"
      :remove-assistant="removeAssistant"
    />

    <div class="mentor-block">
      <div class="mentor-label">Наставник (премодерация)</div>
      <UserLink v-if="mentor" :user="mentor" hide-badge />
      <SecondaryText v-else>Наставник не назначен</SecondaryText>
    </div>
  </SettingsSection>
</template>

<style scoped lang="sass">
// Under the assistants, parted from them by a rule.
.mentor-block
  margin-top: $medium
  padding-top: $medium
  border-top: 1px solid $border

.mentor-label
  color: $text-muted
  font-size: $secondary-font-size
  margin-bottom: $small
</style>
