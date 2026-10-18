<script setup lang="ts">
/**
 * GameSettings — the game management page. Composes, by the headings on it:
 *  - "Общие настройки" (title, players and recruitment, link, system,
 *    setting, the discussion, what everyone sees of the play, tags)
 *  - "Система атрибутов" (the character sheet of the game)
 *  - "Создать новую комнату" and "Настройки комнат" (room CRUD, access
 *    grants, per-room settings)
 *  - "Управление ролями" (assistant invite / removeAssistant)
 *  - "Черный список"
 *  - "Приглашения"
 *  - "Опасная зона" (master-only game delete)
 *
 * The sections do not share one audience, so the page does not gate them with
 * one flag: each section is drawn only for the viewers whose saves the server
 * would actually accept. The four widths are spelled out at the computeds
 * below, each named after the intention that enforces it.
 *
 * A viewer the page admits to nothing, a guest included, has no page here: the
 * address opens the site's own "not found", as a room closed to the reader does
 * (GameRoom), and not a page with a refusal on it. The links to it are drawn
 * only for those it admits.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import {
  restoreSession,
  useAuthStore,
  userIsSeniorModerator,
} from "@/entities/user";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import Button from "@/shared/ui/Button/Button.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import GameInfoSection from "./settings/GameInfoSection.vue";
import AttributeSchemaSection from "./settings/AttributeSchemaSection.vue";
import RoomsSection from "./settings/RoomsSection.vue";
import RolesSection from "./settings/RolesSection.vue";
import BlacklistSection from "./settings/BlacklistSection.vue";
import InvitationsSection from "./settings/InvitationsSection.vue";

const route = useRoute();
const router = useRouter();
const gameStore = useGameDetailsStore();
const { isMaster, isAssistant, isMentor, gameLoading } = storeToRefs(gameStore);
const { user } = storeToRefs(useAuthStore());

// GameIntention.EditSettings — the information form (GameService.UpdateAsync)
// and the invitation list (GameInvitationService.GetPendingInvitations). The
// leads, the mentor curating THIS game and senior moderation: helping a newbie
// shape the game is what the curator is for. This is also what admits a viewer
// to the page at all, so nobody sees a page whose every save answers 403.
// Never a guest: every arm asks for a signed-in user, and the page asks it
// itself rather than rest on the roles a guest's read of the game carries.
const canEditSettings = computed(
  () =>
    !!user.value &&
    (isMaster.value ||
      isAssistant.value ||
      isMentor.value ||
      userIsSeniorModerator(user.value)),
);

// GameIntention.Edit — rooms (RoomService, RoomAccessService), the character
// sheet of the game (GameAttributeSchemaService) and the whole blacklist, its
// READ included (GameBlacklistService asks Edit on Get too, so the section is
// hidden rather than merely read-only). The leads and senior moderation; the
// curator is outside it, because the game's bans, its rooms and the sheet its
// players fill in are not the curator's to run.
const canEditGame = computed(
  () =>
    isMaster.value || isAssistant.value || userIsSeniorModerator(user.value),
);

// GameIntention.InvitePlayer / InviteReader / CancelInvitation — the game roles
// alone. Those three arms carry no rank clause, so a senior moderator reads the
// invitation list without being able to write it.
const canManageInvitations = computed(
  () => isMaster.value || isAssistant.value,
);

// GameIntention.InviteAssistant / RemoveUser — the master alone, no rank arm.
// The roster itself is a plain Read on the game the page already holds, so
// "Управление ролями" is drawn for everyone the page admits and only its two
// controls answer to this flag: the assistant and the senior moderator would
// otherwise get a section with nothing in it to press, and the curator, who may
// read the roster, would be shown nothing at all.
const canManageRoles = computed(() => isMaster.value);

// Who the viewer is, settled: the start of the page asks the server once
// (restoreSession), and a session the browser kept no copy of is named by that
// answer alone. Deciding before it would send a master away from his own game.
const sessionSettled = ref(false);
const settle = () => (sessionSettled.value = true);
restoreSession().then(settle, settle);

// The page admits this viewer to nothing. Asked of the game read for him: the
// shell reads the game again when the viewer changes, and the roles of the one
// before stand until that answer lands.
const refused = computed(
  () => sessionSettled.value && !gameLoading.value && !canEditSettings.value,
);

watch(
  refused,
  (value) => {
    if (!value) return;
    void router.replace({
      name: "not-found",
      params: { pathMatch: route.path.slice(1).split("/") },
      query: route.query,
      hash: route.hash,
    });
  },
  { immediate: true, flush: "post" },
);

// --- Danger zone (master-only game delete) ---
const confirmDelete = ref(false);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function deleteGame() {
  const error = await gameStore.deleteGame({
    ownsRefusal: true,
    ownsRateLimit: true,
  });
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

      <section v-if="isMaster" class="settings-section danger-zone">
        <block-title>Опасная зона</block-title>
        <secondary-text class="danger-hint">
          Удаление игры необратимо
        </secondary-text>
        <Button type="button" class="delete-game" @click="confirmDelete = true">
          Удалить игру
        </Button>
      </section>
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

.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element

// Framed, and in the neutral line of the page: the danger is said by the
// heading and the sentence under it, and the confirmation that follows the
// press carries no red either.
.danger-zone
  border: 1px solid $border

.danger-hint
  display: block
  margin-bottom: $small
</style>
