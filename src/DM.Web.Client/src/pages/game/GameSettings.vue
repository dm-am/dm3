<script setup lang="ts">
/**
 * GameSettings — the game management page for master and assistant. Composes:
 *  - "Информация игры" (title/system/setting, recruitment, privacy)
 *  - "Система атрибутов" (the shared AttributeSchemaEditor feature — same
 *    editor as before, no duplicated logic; the dedicated /schema page was
 *    removed)
 *  - "Управление комнатами" (room CRUD, access grants, per-room settings)
 *  - "Управление ролями" (assistant invite / removeAssistant)
 *  - "Черный список"
 *  - "Приглашения"
 *  - "Опасная зона" (master-only game delete)
 */
import { computed, ref, watch, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { gameApi, type AttributeSchema } from "@/entities/game";
import { AttributeSchemaEditor } from "@/features/attribute-schema-editor";
import { createEmptySchema } from "@/entities/game";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
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
const { game, characters, isMaster, isAssistant } = storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);

// Settings are open to the game leads (master and assistant).
const canEdit = computed(() => isMaster.value || isAssistant.value);

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

async function saveSchema(schema: AttributeSchema) {
  savingSchema.value = true;
  const payload: AttributeSchema = { ...schema, title: schema.title.trim() };
  const { error } = schema.id
    ? await gameApi.updateSchema(schema.id, payload)
    : await gameApi.createSchema(payload);
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
    <secondary-text v-if="!canEdit">
      Настройки игры доступны мастеру и ассистенту.
    </secondary-text>

    <template v-else>
      <GameInfoSection />

      <section class="settings-section">
        <block-title>Система атрибутов</block-title>
        <AttributeSchemaEditor
          v-model="schemaDraft"
          :has-characters="hasCharacters"
          :saving="savingSchema"
          @save="saveSchema"
          @cancel="resetSchema"
        />
      </section>

      <RoomsSection />
      <RolesSection />
      <BlacklistSection />
      <InvitationsSection />

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
