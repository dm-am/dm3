<script setup lang="ts">
/**
 * The settings of a game. Each section is drawn only for the viewers whose
 * saves the server accepts; a viewer admitted to none, a guest included, gets
 * the site's "not found" at this address.
 */
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import {
  restoreSession,
  useAuthStore,
  userIsSeniorModerator,
} from "@/entities/user";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import Button from "@/shared/ui/Button/Button.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import GameInfoSection from "./settings/GameInfoSection.vue";
import AttributeSchemaSection from "./settings/AttributeSchemaSection.vue";
import RoomsSection from "./settings/RoomsSection.vue";
import RolesSection from "./settings/RolesSection.vue";
import BlacklistSection from "./settings/BlacklistSection.vue";
import InvitationsSection from "./settings/InvitationsSection.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { useNotFoundWhen } from "@/shared/lib/composables/useNotFoundWhen";

const router = useRouter();
const gameStore = useGameDetailsStore();
const { isMaster, isAssistant, isMentor, gameLoading } = storeToRefs(gameStore);
const { user } = storeToRefs(useAuthStore());

// GameIntention.EditSettings, which also admits a viewer to the page at all.
const canEditSettings = computed(
  () =>
    !!user.value &&
    (isMaster.value ||
      isAssistant.value ||
      isMentor.value ||
      userIsSeniorModerator(user.value)),
);

// GameIntention.Edit; the blacklist asks it of the read too.
const canEditGame = computed(
  () =>
    isMaster.value || isAssistant.value || userIsSeniorModerator(user.value),
);

// GameIntention.InvitePlayer, InviteReader and CancelInvitation: no rank arm.
const canManageInvitations = computed(
  () => isMaster.value || isAssistant.value,
);

// GameIntention.InviteAssistant and RemoveUser; the roster itself is a Read.
const canManageRoles = computed(() => isMaster.value);

// Deciding before the session is restored would send a master away.
const sessionSettled = ref(false);
const settle = () => (sessionSettled.value = true);
restoreSession().then(settle, settle);

// Not while the game is read again for a new viewer.
const refused = computed(
  () => sessionSettled.value && !gameLoading.value && !canEditSettings.value,
);

useNotFoundWhen(refused);

const confirmDelete = ref(false);

// The window prints its own refusal, so the interceptor keeps quiet.
async function deleteGame() {
  const error = await gameStore.deleteGame(OWNS_REFUSAL);
  if (error) return { error };
  router.push({ name: "games" });
  return { error: null };
}
</script>

<template>
  <div class="game-settings">
    <template v-if="canEditSettings">
      <GameInfoSection />

      <AttributeSchemaSection v-if="canEditGame" />
      <RoomsSection v-if="canEditGame" />
      <RolesSection :can-manage-roles="canManageRoles" />
      <BlacklistSection v-if="canEditGame" />
      <InvitationsSection :can-manage="canManageInvitations" />

      <SettingsSection v-if="isMaster" class="danger-zone" title="Опасная зона">
        <secondary-text class="danger-hint">
          Удаление игры необратимо
        </secondary-text>
        <Button type="button" class="delete-game" @click="confirmDelete = true">
          Удалить игру
        </Button>
      </SettingsSection>
    </template>

    <ConfirmDialog
      v-model:show="confirmDelete"
      title="Удалить игру?"
      message="Игра будет удалена без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить игру"
      :action="deleteGame"
    />
  </div>
</template>

<style scoped lang="sass">
.game-settings
  max-width: $grid-step * 200

// Neutral: the heading and the sentence say the danger, not a red.
.danger-zone
  border: 1px solid $border

.danger-hint
  margin-bottom: $small
</style>
