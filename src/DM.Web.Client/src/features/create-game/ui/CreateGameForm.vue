<script setup lang="ts">
/**
 * CreateGameForm — "Создание новой игры", one form of captioned rows in the
 * order of DM2's CreateModule: the fields of one line beside their captions,
 * the sheet of attributes, the description and the tags over the whole width,
 * the boxes in a column of their own.
 *
 * DM2's "Количество игроков" is not here: the creation request has no field
 * for it, and the number and the recruitment are set in the settings of the
 * game once it exists (GameInfoSection).
 */
import { ref, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/entities/user";
import { useGamesStore } from "@/entities/game";
import Button from "@/shared/ui/Button/Button.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import {
  AttributeSchemaEditor,
  useSchemaSources,
} from "@/features/attribute-schema-editor/@x/create-game";
import AssistantSelector from "./AssistantSelector.vue";
import { gameApi, TagSelector, cloneSpecsWithNewIds } from "@/entities/game";
import {
  CommentariesAccessMode,
  type CreateGameInput,
  type AttributeSchemaDraft,
} from "@/entities/game";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import type { BadRequestError } from "@/shared/api/models/common";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const router = useRouter();
const { user } = storeToRefs(useAuthStore());
const gamesStore = useGamesStore();

// DM2's two states of a game being made. "Оформляется" is a game still in the
// making (`draft: true`), out of sight of everyone without a role in it, until
// the master starts it from the game's menu; "Оформлен" creates it started,
// recruitment open, which is what `draft: false` does on the server. DM2
// starts the choice on the first.
type GameState = "draft" | "ready";
const stateOptions: { value: GameState; label: string }[] = [
  { value: "draft", label: "Оформляется" },
  { value: "ready", label: "Оформлен" },
];

const commentariesAccessOptions = [
  {
    value: CommentariesAccessMode.Public,
    label: "Доступно всем без ограничений",
  },
  {
    value: CommentariesAccessMode.Readonly,
    label: "Доступно посторонним только для чтения",
  },
];

// Form state
const title = ref("");
const state = ref<GameState>("draft");
const systemName = ref("");
const settingName = ref("");
const information = ref("");
// Both boxes open clear, as on DM2's form.
const viewDice = ref(false);
const showPrivateMessages = ref(false);
const commentariesAccessMode = ref(CommentariesAccessMode.Public);
const schemaDraft = ref<AttributeSchemaDraft | null>(null);
const assistantUsername = ref<string | null>(null);
const selectedTags = ref<number[]>([]);

// The sheet the game starts with. The form opens on the site's default
// template, so a master who never touches the editor still gets the sheet the
// site offers; with no default template on the site there is nothing to open
// on, and the editor starts empty.
const {
  sources,
  defaultSpecifications,
  refusal: sourcesRefusal,
  load: loadSchemaSources,
} = useSchemaSources();

onMounted(async () => {
  await loadSchemaSources();
  if (!defaultSpecifications.value) return;
  // The editor is on screen and taking edits while the catalogue is still on
  // the wire. A table the master has already started is theirs, and the
  // template does not land on top of it.
  if (schemaDraft.value?.specifications.length) return;
  schemaDraft.value = {
    title: "",
    specifications: cloneSpecsWithNewIds(defaultSpecifications.value),
  };
});

// Released the moment the game exists: the navigation that follows is this
// form's own, and asking the author whether they meant to leave their new game
// would be theatre.
const saved = ref(false);
const dirty = computed(
  () =>
    !saved.value &&
    !!(
      title.value.trim() ||
      systemName.value.trim() ||
      settingName.value.trim() ||
      information.value.trim()
    ),
);

const isSubmitting = ref(false);
const titleError = ref("");
const systemError = ref("");
const settingError = ref("");
const infoError = ref("");
const assistantError = ref("");

const canCreate = computed(() => {
  return user.value && title.value.trim().length > 0;
});

/** What the server said that no field of the form shows, under the button. */
const createResult = useActionResult();

function clearErrors() {
  titleError.value = "";
  systemError.value = "";
  settingError.value = "";
  infoError.value = "";
  assistantError.value = "";
  createResult.clear();
}

async function handleSubmit() {
  if (!canCreate.value || isSubmitting.value) return;

  isSubmitting.value = true;
  clearErrors();

  try {
    // The sheet is saved with the game, in one request: a refused sheet is a
    // refused game, and no half-made schema is left behind.
    const gameData: CreateGameInput = {
      title: title.value.trim(),
      system: systemName.value.trim() || undefined,
      setting: settingName.value.trim() || undefined,
      info: information.value,
      draft: state.value === "draft",
      tags: selectedTags.value.length > 0 ? [...selectedTags.value] : undefined,
      attributeSpecifications: schemaDraft.value?.specifications ?? [],
      assistantUsername: assistantUsername.value || undefined,
      privacySettings: {
        viewPrivates: showPrivateMessages.value,
        viewDice: viewDice.value,
        viewPostStats: true,
        commentariesAccess: commentariesAccessMode.value,
      },
    };

    const { data, error: apiError } = await gameApi.createGame(gameData);

    if (apiError) {
      const errors = parseApiErrors(apiError as BadRequestError);
      // By the names the server answers with, not by the names the fields carry
      // here: the dictionary comes back keyed by the properties of the request,
      // so asking for "system" found nothing and the reader got the bare toast
      // instead of a message under the field that caused it.
      titleError.value = getFieldError(errors, "title") || "";
      systemError.value = getFieldError(errors, "systemName") || "";
      settingError.value = getFieldError(errors, "narrativeSetting") || "";
      infoError.value = getFieldError(errors, "info") || "";
      assistantError.value = getFieldError(errors, "assistantUsername") || "";

      if (
        !titleError.value &&
        !systemError.value &&
        !settingError.value &&
        !infoError.value &&
        !assistantError.value
      ) {
        // Whatever the server refused outside the fields - a refusal, a rate
        // limit, a broken server, a field this form does not draw - stands
        // under the button that was pressed.
        createResult.refuse(apiError, "Не удалось создать игру");
      }
      return;
    }

    if (data) {
      // The lists are cached; without refreshing them the game the user just
      // created is missing from /games and from the sidebar until they expire.
      saved.value = true;
      await gamesStore.invalidateGameLists();
      router.push({ name: "game", params: { id: data.resource.id } });
    }
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <form class="create-game-form" @submit.prevent="handleSubmit">
    <form-field
      class="row"
      label="Название:"
      name="title"
      :errors="titleError ? [titleError] : []"
    >
      <input
        v-model="title"
        type="text"
        id="title"
        maxlength="200"
        @input="titleError = ''"
      />
    </form-field>

    <form-field class="row" label="Состояние:" name="state">
      <Select
        id="game-state"
        :model-value="state"
        :options="stateOptions"
        @update:model-value="(v) => (state = v as GameState)"
      />
    </form-field>

    <form-field
      class="row"
      label="Ассистент:"
      name="assistantUsername"
      :errors="assistantError ? [assistantError] : []"
    >
      <assistant-selector
        v-model="assistantUsername"
        @update:model-value="assistantError = ''"
      />
    </form-field>

    <form-field
      class="row"
      label="Система:"
      name="system"
      :errors="systemError ? [systemError] : []"
    >
      <input
        v-model="systemName"
        type="text"
        id="system"
        maxlength="100"
        @input="systemError = ''"
      />
    </form-field>

    <form-field
      class="row"
      label="Сеттинг:"
      name="setting"
      :errors="settingError ? [settingError] : []"
    >
      <input
        v-model="settingName"
        type="text"
        id="setting"
        maxlength="100"
        @input="settingError = ''"
      />
    </form-field>

    <!-- The sheet is a table with its own list of templates, too wide for the
         column of the fields: its caption stands over it, as the captions of
         the description and the tags do. -->
    <div class="sheet">
      <p class="sheet-caption">Характеристики персонажей:</p>
      <AttributeSchemaEditor
        v-model="schemaDraft"
        :sources="sources"
        :sources-refusal="sourcesRefusal"
        :show-actions="false"
      />
    </div>

    <form-field class="row" label="Обсуждение игры:" name="commentaries">
      <Select
        id="game-commentaries-access"
        :model-value="commentariesAccessMode"
        :options="commentariesAccessOptions"
        @update:model-value="
          (v) => (commentariesAccessMode = v as CommentariesAccessMode)
        "
      />
    </form-field>

    <div class="boxes">
      <label class="checkbox-label">
        <input v-model="viewDice" type="checkbox" />
        Показывать всем результаты бросков
      </label>
      <label class="checkbox-label">
        <input v-model="showPrivateMessages" type="checkbox" />
        Показывать всем приватные сообщения
      </label>
    </div>

    <form-field
      label="Описание:"
      name="info"
      :errors="infoError ? [infoError] : []"
    >
      <BBCodeEditor
        v-model="information"
        context="common"
        :min-height="200"
        :max-height="500"
        :resizable="true"
        @update:model-value="infoError = ''"
      />
      <!-- The server's least length (GameFieldLimits.InfoMinLength), said
           before the press and not only in the refusal after it. -->
      <template #hint>Не менее 200 символов</template>
    </form-field>

    <form-field label="Теги:" name="tags">
      <tag-selector v-model="selectedTags" />
    </form-field>

    <div class="form-actions">
      <Button
        type="submit"
        variant="primary"
        :loading="isSubmitting"
        :disabled="!canCreate || createResult.waiting.value"
      >
        Создать
      </Button>
    </div>
    <ActionResult :result="createResult" />

    <UnsavedChangesGuard :dirty="dirty" />
  </form>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/FormRows" as *

.create-game-form
  max-width: $grid-step * 150

.create-game-form .row
  +form-row

// A caption of a page over a block it names (+field-caption), at the step a
// field keeps from the rows around it.
.sheet
  margin: $medium 0

.sheet-caption
  +field-caption
  margin-bottom: $minor

.boxes
  +form-row-boxes

.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

.form-actions
  display: flex
  gap: $small
  margin-top: $medium
</style>
