<script setup lang="ts">
/**
 * CharacterEdit — edit an existing character and drive its lifecycle status
 * (accept / decline / retire-as-dead / exile / leave / return) plus soft-delete.
 *
 * The schema-driven CharacterForm (features/edit-character) handles the field
 * edits and persists via gameApi.updateCharacter. Status transitions and delete
 * are separate actions below the form: they call gameApi.changeCharacterStatus /
 * deleteCharacter and never round-trip the character's (server-rendered) BbCode
 * attribute values.
 *
 * When the acting user can edit more than one character in the game, a selector
 * switches between them; without a valid :characterId the first is preselected.
 *
 * Gating: master/assistant may manage any character (accept/decline/exile/dead/
 * return, delete NPCs); the owner may leave the game or delete their own
 * character. The backend is the final authority.
 */
import { computed, onMounted, ref, watch } from "vue";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  gameApi,
  GameStatus,
  type Character,
  type CharacterStatusTransition,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { CharacterForm } from "@/features/edit-character";
import { createEmptySchema } from "@/entities/game";
import { Select } from "@/shared/ui/Select";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const store = useGameDetailsStore();
const { game, characters, isMaster, isAssistant } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

const gameId = computed(() => route.params.id as string);
const routeCharacterId = computed(() => route.params.characterId as string);

const canManage = computed(() => isMaster.value || isAssistant.value);

const schema = computed(() => game.value?.schema ?? createEmptySchema());

onMounted(async () => {
  if (!game.value) await store.loadGame(gameId.value);
  if (!characters.value.length) await store.loadCharacters(gameId.value);
});

// Characters the acting user may edit: staff edit everything except declined
// applications; a player edits only their own.
const editableCharacters = computed<Character[]>(() =>
  characters.value.filter((c) => {
    if (c.status === "Declined") return false;
    return canManage.value || c.author?.id === user.value?.id;
  }),
);

const selectOptions = computed(() =>
  editableCharacters.value.map((c) => ({
    value: c.id as unknown as string,
    label: c.name + (c.isNpc ? " (NPC)" : ""),
  })),
);

const selected = computed<Character | undefined>(() => {
  const byRoute = editableCharacters.value.find(
    (c) => (c.id as unknown as string) === routeCharacterId.value,
  );
  return byRoute ?? editableCharacters.value[0];
});

const selectedId = computed(() => selected.value?.id as unknown as string);

// Keep the URL in sync with the effective selection (preselect-first case).
watch([selected, editableCharacters], () => {
  if (selected.value && selectedId.value !== routeCharacterId.value) {
    router.replace({
      name: "game-character-edit",
      params: { id: gameId.value, characterId: selectedId.value },
    });
  }
});

function switchCharacter(id: string) {
  router.replace({
    name: "game-character-edit",
    params: { id: gameId.value, characterId: id },
  });
}

const isOwner = computed(
  () => !!selected.value?.author && selected.value.author.id === user.value?.id,
);

// --- Full character for the form ----------------------------------------------
// The list endpoint returns characters without attributes, so the form is fed
// from the single-character AuthorEdit fetch: non-BBCode raw values in `value`,
// BBCode rendered round-trip HTML in `valueBbText`. Without this seed, saving
// would drop every attribute the user did not retype.
const detail = ref<Character | null>(null);
const detailLoading = ref(false);
const detailError = ref(false);

watch(
  selectedId,
  async (id) => {
    detail.value = null;
    detailError.value = false;
    if (!id) return;
    detailLoading.value = true;
    const { data, error } = await gameApi.getCharacterForEdit(id);
    // Ignore a late response for a character the user has switched away from.
    if (selectedId.value !== id) return;
    detailLoading.value = false;
    if (error || !data?.resource) {
      detailError.value = true;
      return;
    }
    detail.value = data.resource;
  },
  { immediate: true },
);

// Raw initial values keyed by specification id for CharacterForm.
//
// valueBbText comes from the AuthorEdit audience, which returns HTML carrying
// data-bb-* attributes rather than the source: BbConverter calls RenderHtml for
// every audience except plain text. The editor takes and returns BBCode, so it
// escapes incoming HTML and hands it back as the field's text on save.
// htmlToBbcode is the reverse half of that same round trip.
const initialValues = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {};
  for (const attr of detail.value?.attributes ?? []) {
    const bbText = attr.valueBbText as unknown as string | undefined;
    map[attr.id as unknown as string] =
      attr.value ?? (bbText ? htmlToBbcode(bbText) : "");
  }
  return map;
});

// Whether the acting user may edit the character's fields (mirrors the
// backend CharacterIntention.Edit rule): the owner while the game is active;
// master/assistant only for NPCs or characters that allow editByMaster.
// Everyone else on this page still gets the status actions below.
const canEditFields = computed(() => {
  const c = detail.value;
  if (!c) return false;
  const owned = !!c.author && c.author.id === user.value?.id;
  if (owned && game.value?.status === GameStatus.Active) return true;
  return canManage.value && (!!c.isNpc || !!c.privacy?.editByMaster);
});

// --- Status / delete actions -------------------------------------------------

interface CharacterAction {
  key: string;
  label: string;
  danger?: boolean;
  confirm?: string;
  run: () => Promise<void>;
}

const busy = ref(false);
const pending = ref<CharacterAction | null>(null);

async function applyStatus(transition: CharacterStatusTransition) {
  const c = selected.value;
  if (!c) return;
  busy.value = true;
  const { error } = await gameApi.changeCharacterStatus(
    c.id as unknown as string,
    transition,
  );
  busy.value = false;
  if (error) {
    notifyFailure(error, "Не удалось изменить статус персонажа");
    return;
  }
  toast.success("Статус персонажа изменен");
  await store.loadCharacters(gameId.value);
}

async function doDelete() {
  const c = selected.value;
  if (!c) return;
  busy.value = true;
  const { error } = await gameApi.deleteCharacter(c.id as unknown as string);
  busy.value = false;
  if (error) {
    notifyFailure(error, "Не удалось удалить персонажа");
    return;
  }
  toast.success(c.isNpc ? "NPC удален" : "Персонаж удален");
  await store.loadCharacters(gameId.value);
  router.push({ name: "game-characters", params: { id: gameId.value } });
}

const actions = computed<CharacterAction[]>(() => {
  const c = selected.value;
  if (!c) return [];
  const list: CharacterAction[] = [];

  if (c.status === "UnderReview" && canManage.value) {
    list.push({
      key: "accept",
      label: "Принять в игру",
      run: () => applyStatus("Accept"),
    });
    list.push({
      key: "decline",
      label: "Отклонить заявку",
      danger: true,
      confirm: "Отклонить заявку этого персонажа?",
      run: () => applyStatus("Decline"),
    });
  }

  if (c.status === "Active") {
    if (canManage.value) {
      list.push({
        key: "dead",
        label: "Отметить погибшим",
        danger: true,
        confirm: "Отметить персонажа погибшим и вывести из игры?",
        run: () => applyStatus("Kill"),
      });
      list.push({
        key: "exile",
        label: "Изгнать из игры",
        danger: true,
        confirm: "Изгнать персонажа из игры?",
        run: () => applyStatus("Exile"),
      });
    }
    if (isOwner.value && !c.isNpc) {
      list.push({
        key: "leave",
        label: "Покинуть игру",
        danger: true,
        confirm: "Вывести персонажа и покинуть игру?",
        run: () => applyStatus("Leave"),
      });
    }
  }

  // Mirrors the backend rules: staff resurrect the dead, the owner returns
  // after voluntarily leaving.
  if (c.status === "Retired") {
    if (canManage.value && c.isDead) {
      list.push({
        key: "resurrect",
        label: "Вернуть в игру",
        run: () => applyStatus("Resurrect"),
      });
    } else if (isOwner.value && c.isPlayerLeft) {
      list.push({
        key: "return",
        label: "Вернуться в игру",
        run: () => applyStatus("Return"),
      });
    }
  }

  // Delete: the owner deletes their own character; staff delete NPCs.
  if (isOwner.value || (canManage.value && c.isNpc)) {
    list.push({
      key: "delete",
      label: c.isNpc ? "Удалить NPC" : "Удалить персонажа",
      danger: true,
      confirm: c.isNpc
        ? "Удалить NPC безвозвратно?"
        : "Удалить персонажа безвозвратно?",
      run: doDelete,
    });
  }

  return list;
});

function trigger(action: CharacterAction) {
  if (action.confirm) {
    pending.value = action;
  } else {
    action.run();
  }
}

async function confirmPending() {
  const action = pending.value;
  pending.value = null;
  if (action) await action.run();
}

function onSaved() {
  toast.success("Персонаж сохранен");
  store.loadCharacters(gameId.value);
}

function onCancel() {
  router.push({ name: "game-characters", params: { id: gameId.value } });
}
</script>

<template>
  <div class="character-edit">
    <secondary-text v-if="!editableCharacters.length" class="empty">
      Нет персонажей, доступных для редактирования.
    </secondary-text>

    <template v-else-if="selected">
      <div v-if="selectOptions.length > 1" class="character-switch">
        <label class="switch-label" for="character-switch">Персонаж:</label>
        <Select
          id="character-switch"
          :model-value="selectedId"
          :options="selectOptions"
          @update:model-value="(v) => switchCharacter(v as string)"
        />
      </div>

      <secondary-text v-if="detailLoading"
        >Загрузка персонажа...</secondary-text
      >
      <secondary-text v-else-if="detailError" class="error">
        Не удалось загрузить персонажа
      </secondary-text>
      <CharacterForm
        v-else-if="detail && canEditFields"
        :key="selectedId"
        :schema="schema"
        :character="detail"
        :character-id="selectedId"
        :game-id="game?.id ?? gameId"
        :initial-name="detail.name"
        :initial-values="initialValues"
        @saved="onSaved"
        @cancel="onCancel"
      />
      <secondary-text v-else-if="detail" class="empty">
        Анкету этого персонажа может редактировать только его владелец.
      </secondary-text>

      <section v-if="actions.length" class="status-section">
        <block-title>Статус персонажа</block-title>
        <div class="status-actions">
          <button
            v-for="action in actions"
            :key="action.key"
            type="button"
            class="status-btn"
            :class="{ danger: action.danger }"
            :disabled="busy"
            @click="trigger(action)"
          >
            {{ action.label }}
          </button>
        </div>
      </section>
    </template>

    <ConfirmDialog
      :show="!!pending"
      :title="pending?.label ?? ''"
      :message="pending?.confirm ?? ''"
      :confirm-label="pending?.label"
      :loading="busy"
      danger
      @confirm="confirmPending"
      @cancel="pending = null"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.character-edit
  max-width: $grid-step * 200

.empty
  display: block

.error
  color: $accent-red

.character-switch
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $medium

.switch-label
  color: $text-muted
  font-size: $secondary-font-size

.status-section
  margin-top: $big
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.status-actions
  display: flex
  flex-wrap: wrap
  gap: $small
  margin-top: $small

.status-btn
  font-size: $secondary-font-size
  +button

  &.danger
    +button-danger
</style>
