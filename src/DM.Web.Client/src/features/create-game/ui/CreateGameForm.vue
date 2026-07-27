<script setup lang="ts">
import { ref, computed } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/entities/user";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import Button from "@/shared/ui/Button/Button.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { AttributeSchemaEditor } from "@/features/attribute-schema-editor";
import TagSelector from "./TagSelector.vue";
import AssistantSelector from "./AssistantSelector.vue";
import { gameApi } from "@/entities/game";
import {
  CommentariesAccessMode,
  type CreateGameInput,
  type AttributeSchema,
} from "@/entities/game";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import type { BadRequestError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";

const router = useRouter();
const { user } = storeToRefs(useUserStore());
const toast = useToast();

const commentariesAccessOptions = [
  { value: CommentariesAccessMode.Public, label: "Публичные" },
  { value: CommentariesAccessMode.Readonly, label: "Только для чтения" },
  { value: CommentariesAccessMode.Private, label: "Только для участников" },
];

// Form state
const title = ref("");
const systemName = ref("");
const settingName = ref("");
const information = ref("");
const hideDiceResult = ref(false);
const showPrivateMessages = ref(false);
const commentariesAccessMode = ref(CommentariesAccessMode.Public);
const schemaDraft = ref<AttributeSchema | null>(null);
const assistantUsername = ref<string | null>(null);
const selectedTags = ref<number[]>([]);

const isSubmitting = ref(false);
const titleError = ref("");
const systemError = ref("");
const settingError = ref("");
const infoError = ref("");

const canCreate = computed(() => {
  return user.value && title.value.trim().length > 0;
});

function clearErrors() {
  titleError.value = "";
  systemError.value = "";
  settingError.value = "";
  infoError.value = "";
}

async function handleSubmit() {
  if (!canCreate.value || isSubmitting.value) return;

  isSubmitting.value = true;
  clearErrors();

  try {
    // Persist the attribute schema first (only when the master actually
    // defined attributes), then attach its id to the new game.
    let schemaId: string | undefined;
    if (schemaDraft.value && schemaDraft.value.specifications.length > 0) {
      const schemaPayload: AttributeSchema = {
        ...schemaDraft.value,
        title: schemaDraft.value.title.trim() || title.value.trim(),
      };
      const { data: schemaData, error: schemaError } =
        await gameApi.createSchema(schemaPayload);
      if (schemaError || !schemaData) {
        toast.error("Не удалось создать систему атрибутов");
        return;
      }
      schemaId = schemaData.resource.id ?? undefined;
    }

    // Note: selected tags are not submitted — CreateGameRequest.Tags expects
    // backend Guids, but /games/tags exposes only numeric short ids.
    const gameData: CreateGameInput = {
      title: title.value.trim(),
      system: systemName.value.trim() || undefined,
      setting: settingName.value.trim() || undefined,
      info: information.value,
      schemaId,
      assistantUsername: assistantUsername.value || undefined,
      privacySettings: {
        viewPrivates: showPrivateMessages.value,
        viewDice: !hideDiceResult.value,
        viewPostStats: true,
        commentariesAccess: commentariesAccessMode.value,
      },
    };

    const { data, error: apiError } = await gameApi.createGame(gameData);

    if (apiError) {
      const errors = parseApiErrors(apiError as BadRequestError);
      titleError.value = getFieldError(errors, "title") || "";
      systemError.value = getFieldError(errors, "system") || "";
      settingError.value = getFieldError(errors, "setting") || "";
      infoError.value = getFieldError(errors, "info") || "";

      if (
        !titleError.value &&
        !systemError.value &&
        !settingError.value &&
        !infoError.value
      ) {
        toast.error(apiError.title || "Не удалось создать игру");
      }
      return;
    }

    if (data) {
      router.push({ name: "game", params: { id: data.resource.id } });
    }
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <form class="create-game-form" @submit.prevent="handleSubmit">
    <!-- Basic info -->
    <section class="form-section">
      <block-title>Основная информация</block-title>

      <form-field
        label="Название игры *"
        name="title"
        :errors="titleError ? [titleError] : []"
      >
        <input
          v-model="title"
          type="text"
          id="title"
          placeholder="Введите название игры"
          maxlength="200"
          @input="titleError = ''"
        />
      </form-field>

      <form-field
        label="Система"
        name="system"
        optional
        :errors="systemError ? [systemError] : []"
      >
        <input
          v-model="systemName"
          type="text"
          id="system"
          placeholder="D&D, GURPS, Savage Worlds..."
          maxlength="100"
          @input="systemError = ''"
        />
      </form-field>

      <form-field
        label="Сеттинг"
        name="setting"
        optional
        :errors="settingError ? [settingError] : []"
      >
        <input
          v-model="settingName"
          type="text"
          id="setting"
          placeholder="Forgotten Realms, авторский мир..."
          maxlength="100"
          @input="settingError = ''"
        />
      </form-field>
    </section>

    <!-- Description -->
    <section class="form-section">
      <block-title>Описание</block-title>
      <form-field name="info" :errors="infoError ? [infoError] : []">
        <BBCodeEditor
          v-model="information"
          context="common"
          placeholder="Расскажите о вашей игре..."
          :min-height="200"
          :max-height="500"
          :resizable="true"
          @update:model-value="infoError = ''"
        />
      </form-field>
    </section>

    <!-- Tags -->
    <section class="form-section">
      <block-title>Теги</block-title>
      <tag-selector v-model="selectedTags" />
    </section>

    <!-- Attribute Schema -->
    <section class="form-section">
      <block-title>Система атрибутов</block-title>
      <AttributeSchemaEditor v-model="schemaDraft" :show-actions="false" />
    </section>

    <!-- Assistant -->
    <section class="form-section">
      <block-title>Ассистент</block-title>
      <assistant-selector v-model="assistantUsername" />
    </section>

    <!-- Privacy settings -->
    <section class="form-section">
      <block-title>Настройки приватности</block-title>

      <div class="checkbox-group">
        <div class="privacy-option">
          <label class="checkbox-label">
            <input v-model="hideDiceResult" type="checkbox" />
            Скрыть результаты бросков кубиков
          </label>
          <p class="option-hint">Броски видит только мастер</p>
        </div>

        <div class="privacy-option">
          <label class="checkbox-label">
            <input v-model="showPrivateMessages" type="checkbox" />
            Показывать приватные сообщения
          </label>
          <p class="option-hint">
            Приватные посты станут видны всем участникам
          </p>
        </div>
      </div>

      <form-field label="Доступ к комментариям">
        <Select
          :model-value="commentariesAccessMode"
          :options="commentariesAccessOptions"
          @update:model-value="
            (v) => (commentariesAccessMode = v as CommentariesAccessMode)
          "
        />
      </form-field>
    </section>

    <!-- Submit -->
    <div class="form-actions">
      <Button type="submit" :loading="isSubmitting" :disabled="!canCreate">
        Создать игру
      </Button>
    </div>
  </form>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

.create-game-form
  max-width: $grid-step * 150

.form-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.checkbox-group
  display: flex
  flex-direction: column
  gap: $medium
  margin-bottom: $medium

.privacy-option
  display: flex
  flex-direction: column

.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

.option-hint
  margin: $minor 0 0
  padding-left: 16px + $small
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4

.form-actions
  display: flex
  gap: $small
</style>
