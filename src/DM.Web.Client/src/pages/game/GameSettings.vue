<script setup lang="ts">
/**
 * GameSettings — the game management page. Composes:
 *  - "Информация игры" (title/system/setting, recruitment, privacy)
 *  - "Система атрибутов" (the shared AttributeSchemaEditor feature — same
 *    editor as before, no duplicated logic; the dedicated /schema page was
 *    removed)
 *  - "Управление комнатами" (room CRUD, access grants, per-room settings)
 *  - "Управление ролями" (assistant invite / removeAssistant)
 *  - "Черный список"
 *  - "Приглашения"
 *  - "Опасная зона" (master-only game delete)
 *
 * The sections do not share one audience, so the page does not gate them with
 * one flag: each section is drawn only for the viewers whose saves the server
 * would actually accept. The four widths are spelled out at the computeds
 * below, each named after the intention that enforces it.
 */
import { computed, ref, watch, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { gameApi, type AttributeSchema } from "@/entities/game";
import { UserLink, useAuthStore, userIsSeniorModerator } from "@/entities/user";
import { AttributeSchemaEditor } from "@/features/attribute-schema-editor";
import { createEmptySchema } from "@/entities/game";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { LoginPrompt } from "@/features/auth";
import { useToast } from "@/shared/lib/composables/useToast";
import GameInfoSection from "./settings/GameInfoSection.vue";
import RoomsSection from "./settings/RoomsSection.vue";
import RolesSection from "./settings/RolesSection.vue";
import BlacklistSection from "./settings/BlacklistSection.vue";
import InvitationsSection from "./settings/InvitationsSection.vue";
import { notifyFailure } from "@/shared/lib/errors";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const gameStore = useGameDetailsStore();
const { game, characters, isMaster, isAssistant, isMentor } =
  storeToRefs(gameStore);
const { user } = storeToRefs(useAuthStore());

const gameId = computed(() => route.params.id as string);

// GameIntention.EditSettings — the information form (GameService.UpdateAsync)
// and the invitation list (GameInvitationService.GetPendingInvitations). The
// leads, the mentor curating THIS game and senior moderation: helping a newbie
// shape the game is what the curator is for. This is also what admits a viewer
// to the page at all, so nobody sees a page whose every save answers 403.
const canEditSettings = computed(
  () =>
    isMaster.value ||
    isAssistant.value ||
    isMentor.value ||
    userIsSeniorModerator(user.value),
);

// GameIntention.Edit — rooms (RoomService, RoomAccessService) and the whole
// blacklist, its READ included (GameBlacklistService asks Edit on Get too, so
// the section is hidden rather than merely read-only). The leads and senior
// moderation; the curator is outside it, because the game's bans and its rooms
// are not the curator's to run.
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

// AttributeSchemaIntention.Edit — the schema's author and nobody else. A schema
// is its own resource, not a part of the game: one public schema backs many
// games, so no game role widens it and delegating it here would hand a curator
// the schema every other game is built on.
//
// A game with no schema at all gets no editor either, and that is not about
// permission: POST /v1/schemas would be accepted (GameIntention.Create), but
// nothing would attach the result to THIS game — the update contract carries no
// schema field — so the form would report a save that left the game exactly as
// schema-less as it was. A schema is picked when the game is created
// (CreateGameForm), and until a game-scoped attach exists this page says so
// instead of offering a control that lies.
const gameSchema = computed(() => game.value?.schema ?? null);
const schemaAuthor = computed(() => gameSchema.value?.author ?? null);
const hasSchema = computed(() => !!gameSchema.value?.id);
const canEditSchema = computed(() => {
  if (!hasSchema.value) return false;
  const author = schemaAuthor.value?.username;
  return !!author && author === user.value?.username;
});

const hasCharacters = computed(() =>
  characters.value.some((c) => c.status !== "Declined"),
);

const schemaDraft = ref<AttributeSchema>(createEmptySchema());
const savingSchema = ref(false);

watch(
  game,
  (g) => {
    if (g?.schema) schemaDraft.value = g.schema;
  },
  { immediate: true },
);

onMounted(() => {
  if (!characters.value.length) gameStore.loadCharacters(gameId.value);
});

// Only ever an update: the editor is drawn for the author of a schema the game
// already has, so there is no create branch to fall to.
async function saveSchema(schema: AttributeSchema) {
  if (!schema.id) return;
  savingSchema.value = true;
  const payload: AttributeSchema = { ...schema, title: schema.title.trim() };
  const { error } = await gameApi.updateSchema(schema.id, payload);
  savingSchema.value = false;

  if (error) {
    notifyFailure(error, "Не удалось сохранить систему атрибутов");
    return;
  }
  toast.success("Система атрибутов сохранена");
  await gameStore.loadGame(gameId.value);
}

function resetSchema() {
  schemaDraft.value = game.value?.schema
    ? { ...game.value.schema }
    : createEmptySchema();
}

// --- Danger zone (master-only game delete) ---
const confirmDelete = ref(false);

async function deleteGame() {
  confirmDelete.value = false;
  const error = await gameStore.deleteGame();
  if (error) {
    notifyFailure(error, "Не удалось удалить игру");
    return;
  }
  toast.success("Игра удалена");
  router.push({ name: "games" });
}
</script>

<template>
  <div class="game-settings">
    <LoginPrompt v-if="!user" action="управлять игрой" />

    <secondary-text v-else-if="!canEditSettings">
      Настройки игры доступны мастеру, ассистенту и наставнику игры.
    </secondary-text>

    <template v-else>
      <GameInfoSection />

      <section class="settings-section">
        <block-title>Система атрибутов</block-title>
        <AttributeSchemaEditor
          v-if="canEditSchema"
          v-model="schemaDraft"
          :has-characters="hasCharacters"
          :saving="savingSchema"
          @save="saveSchema"
          @cancel="resetSchema"
        />
        <secondary-text v-else class="schema-note">
          <template v-if="!hasSchema">
            У игры нет системы атрибутов. Ее выбирают при создании игры.
          </template>
          <template v-else-if="schemaAuthor">
            Систему атрибутов правит ее автор:
            <UserLink :user="schemaAuthor" />
          </template>
          <template v-else>Систему атрибутов правит ее автор.</template>
        </secondary-text>
      </section>

      <RoomsSection v-if="canEditGame" />
      <RolesSection :can-manage-roles="canManageRoles" />
      <BlacklistSection v-if="canEditGame" />
      <InvitationsSection :can-manage="canManageInvitations" />

      <section v-if="isMaster" class="settings-section danger-zone">
        <block-title>Опасная зона</block-title>
        <secondary-text class="danger-hint">
          Удаление игры необратимо.
        </secondary-text>
        <button type="button" class="delete-game" @click="confirmDelete = true">
          Удалить игру
        </button>
      </section>
    </template>

    <ConfirmDialog
      :show="confirmDelete"
      title="Удаление игры"
      message="Удалить эту игру? Действие необратимо."
      confirm-label="Удалить игру"
      danger
      @confirm="deleteGame"
      @cancel="confirmDelete = false"
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
  border-radius: $border-radius

.schema-note
  display: block

.danger-zone
  border: 1px solid $accent-red

.danger-hint
  display: block
  margin-bottom: $small

.delete-game
  padding: $small $medium
  border: 1px solid $accent-red
  border-radius: $border-radius
  background: none
  color: $accent-red
  cursor: pointer
  font: inherit

  &:hover
    +tint($accent-red, 10%)
</style>
