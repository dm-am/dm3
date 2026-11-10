<script setup lang="ts">
// No "Количество игроков": the creation request has no field for it (GameInfoSection sets it).
import { ref, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/entities/user";
import {
  CommentariesAccessMode,
  TagSelector,
  cloneSpecsWithNewIds,
  gameApi,
  useGamesStore,
  type AttributeSchemaDraft,
  type CreateGameInput,
} from "@/entities/game";
import Button from "@/shared/ui/Button/Button.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import {
  AttributeSchemaEditor,
  useSchemaSources,
} from "@/features/attribute-schema-editor/@x/create-game";
import AssistantSelector from "./AssistantSelector.vue";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { pluralize } from "@/shared/lib/utils/pluralize";
import {
  GAME_INFO_MIN_LENGTH,
  GAME_TITLE_MAX_LENGTH,
} from "@/shared/lib/constants/game";

const router = useRouter();
const { user } = storeToRefs(useAuthStore());
const gamesStore = useGamesStore();

// "Оформляется" is `draft: true`; "Оформлен" creates the game started, recruitment open.
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

const title = ref("");
const state = ref<GameState>("draft");
const systemName = ref("");
const settingName = ref("");
const information = ref("");
const viewDice = ref(false);
const showPrivateMessages = ref(false);
const commentariesAccessMode = ref(CommentariesAccessMode.Public);
const schemaDraft = ref<AttributeSchemaDraft | null>(null);
const assistantUsername = ref<string | null>(null);
const selectedTags = ref<number[]>([]);

const {
  sources,
  defaultSpecifications,
  refusal: sourcesRefusal,
  load: loadSchemaSources,
} = useSchemaSources();

onMounted(async () => {
  await loadSchemaSources();
  if (!defaultSpecifications.value) return;
  // A table the master has already started is theirs: the template does not land on it.
  if (schemaDraft.value?.specifications.length) return;
  schemaDraft.value = {
    title: "",
    specifications: cloneSpecsWithNewIds(defaultSpecifications.value),
  };
});

// Released once the game exists: the navigation that follows is this form's own.
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

const canCreate = computed(() => user.value && title.value.trim().length > 0);

const createResult = useActionResult();

function clearErrors() {
  titleError.value = "";
  systemError.value = "";
  settingError.value = "";
  infoError.value = "";
  assistantError.value = "";
  createResult.clear();
}

// One request: a refused sheet is a refused game.
function createRequest(): CreateGameInput {
  return {
    title: title.value,
    system: systemName.value.trim() ? systemName.value : undefined,
    setting: settingName.value.trim() ? settingName.value : undefined,
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
}

/** The refusal under its fields, or as one line when it names none of them. */
function showRefusal(apiError: GeneralError | BadRequestError) {
  const errors = parseApiErrors(apiError as BadRequestError);
  // By the request's property names, which key the server's dictionary.
  titleError.value = readFieldError(errors, "title") || "";
  systemError.value = readFieldError(errors, "systemName") || "";
  settingError.value = readFieldError(errors, "narrativeSetting") || "";
  infoError.value = readFieldError(errors, "info") || "";
  assistantError.value = readFieldError(errors, "assistantUsername") || "";

  if (
    !titleError.value &&
    !systemError.value &&
    !settingError.value &&
    !infoError.value &&
    !assistantError.value
  ) {
    createResult.refuse(apiError, "Не удалось создать игру");
  }
}

async function handleSubmit() {
  if (!canCreate.value || isSubmitting.value) return;

  isSubmitting.value = true;
  clearErrors();

  try {
    const { data, error: apiError } = await gameApi.createGame(createRequest());

    if (apiError) {
      showRefusal(apiError);
      return;
    }

    if (data) {
      // The lists are cached: without a refresh the new game is missing until they expire.
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
        :maxlength="GAME_TITLE_MAX_LENGTH"
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
        @input="settingError = ''"
      />
    </form-field>

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
        Показывать всем скрытые блоки
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
      <template #hint
        >Не менее {{ GAME_INFO_MIN_LENGTH }}
        {{
          pluralize(GAME_INFO_MIN_LENGTH, "символа", "символов", "символов")
        }}</template
      >
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
