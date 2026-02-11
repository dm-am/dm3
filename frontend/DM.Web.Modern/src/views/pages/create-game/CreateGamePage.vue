<script setup lang="ts">
import { ref, computed } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/stores";
import PageTitle from "@/components/layout/PageTitle.vue";
import BlockTitle from "@/components/layout/BlockTitle.vue";
import TheButton from "@/components/inputs/TheButton.vue";
import FormField from "@/components/inputs/form/FormField.vue";
import BBCodeEditor from "@/components/inputs/BBCodeEditor.vue";
import SchemaSelector from "./SchemaSelector.vue";
import TagSelector from "./TagSelector.vue";
import AssistantSelector from "./AssistantSelector.vue";
import gameApi from "@/api/requests/gameApi";
import { CommentariesAccessMode, type Game } from "@/api/models/game";

const router = useRouter();
const { user } = storeToRefs(useUserStore());

// Form state
const title = ref("");
const systemName = ref("");
const settingName = ref("");
const information = ref("");
const hideTemper = ref(false);
const hideDiceResult = ref(false);
const showPrivateMessages = ref(false);
const commentariesAccessMode = ref(CommentariesAccessMode.Public);
const disableAlignment = ref(false);
const attributeSchemaId = ref<string | null>(null);
const assistantLogin = ref<string | null>(null);
const selectedTags = ref<string[]>([]);

const isSubmitting = ref(false);
const error = ref<string | null>(null);

const canCreate = computed(() => {
  return user.value && title.value.trim().length > 0;
});

async function handleSubmit() {
  if (!canCreate.value || isSubmitting.value) return;

  isSubmitting.value = true;
  error.value = null;

  try {
    const gameData = {
      title: title.value.trim(),
      systemName: systemName.value.trim() || undefined,
      settingName: settingName.value.trim() || undefined,
      information: information.value,
      hideTemper: hideTemper.value,
      hideDiceResult: hideDiceResult.value,
      showPrivateMessages: showPrivateMessages.value,
      commentariesAccessMode: commentariesAccessMode.value,
      disableAlignment: disableAlignment.value,
      attributeSchemaId: attributeSchemaId.value || undefined,
      assistantLogin: assistantLogin.value || undefined,
      tagIds: selectedTags.value,
    } as unknown as Game;

    const { data, error: apiError } = await gameApi.createGame(gameData);

    if (apiError) {
      error.value = apiError.title || "Failed to create game";
      return;
    }

    if (data?.resource) {
      router.push({ name: "game", params: { id: data.resource.id } });
    }
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <page-title>Создать игру</page-title>

  <div v-if="!user" class="login-required">
    <p>Для создания игры необходимо <router-link to="/login">войти</router-link></p>
  </div>

  <form v-else class="create-game-form" @submit.prevent="handleSubmit">
    <!-- Basic info -->
    <section class="form-section">
      <block-title>Основная информация</block-title>

      <form-field label="Название игры" required>
        <input
          v-model="title"
          type="text"
          class="form-input"
          placeholder="Введите название игры"
          maxlength="200"
        />
      </form-field>

      <form-field label="Система">
        <input
          v-model="systemName"
          type="text"
          class="form-input"
          placeholder="D&D, GURPS, Savage Worlds..."
          maxlength="100"
        />
      </form-field>

      <form-field label="Сеттинг">
        <input
          v-model="settingName"
          type="text"
          class="form-input"
          placeholder="Forgotten Realms, авторский мир..."
          maxlength="100"
        />
      </form-field>
    </section>

    <!-- Description -->
    <section class="form-section">
      <block-title>Описание</block-title>
      <BBCodeEditor
        v-model="information"
        context="common"
        placeholder="Расскажите о вашей игре..."
        :min-height="200"
        :max-height="500"
        :resizable="true"
      />
    </section>

    <!-- Tags -->
    <section class="form-section">
      <block-title>Теги</block-title>
      <tag-selector v-model="selectedTags" />
    </section>

    <!-- Attribute Schema -->
    <section class="form-section">
      <block-title>Система атрибутов</block-title>
      <schema-selector v-model="attributeSchemaId" />
    </section>

    <!-- Assistant -->
    <section class="form-section">
      <block-title>Ассистент</block-title>
      <assistant-selector v-model="assistantLogin" />
    </section>

    <!-- Privacy settings -->
    <section class="form-section">
      <block-title>Настройки приватности</block-title>

      <div class="checkbox-group">
        <label class="checkbox-label">
          <input v-model="hideTemper" type="checkbox" />
          Скрыть характер персонажей
        </label>

        <label class="checkbox-label">
          <input v-model="hideDiceResult" type="checkbox" />
          Скрыть результаты бросков кубиков
        </label>

        <label class="checkbox-label">
          <input v-model="showPrivateMessages" type="checkbox" />
          Показывать приватные сообщения
        </label>

        <label class="checkbox-label">
          <input v-model="disableAlignment" type="checkbox" />
          Отключить мировоззрение
        </label>
      </div>

      <form-field label="Доступ к комментариям">
        <select v-model="commentariesAccessMode" class="form-select">
          <option value="Public">Публичные</option>
          <option value="Readonly">Только для чтения</option>
          <option value="Private">Только для участников</option>
        </select>
      </form-field>
    </section>

    <!-- Error -->
    <div v-if="error" class="form-error">
      {{ error }}
    </div>

    <!-- Submit -->
    <div class="form-actions">
      <the-button
        type="submit"
        :loading="isSubmitting"
        :disabled="!canCreate"
      >
        Создать игру
      </the-button>
    </div>
  </form>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.login-required
  padding: $big
  text-align: center

  a
    color: $link

.create-game-form
  max-width: $grid-step * 150

.form-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.form-input,
.form-select
  +input()
  width: 100%
  max-width: $grid-step * 100

.checkbox-group
  display: flex
  flex-direction: column
  gap: $small
  margin-bottom: $medium

.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

.form-error
  padding: $small
  margin-bottom: $medium
  background-color: rgba($accent-red, 0.1)
  border-radius: $border-radius
  color: $accent-red

.form-actions
  display: flex
  gap: $small
</style>
