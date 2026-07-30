<script setup lang="ts">
import { computed } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import { useAuthStore, userIsSeniorModerator } from "@/entities/user";
import { useCreatePoll } from "../model";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";
import Button from "@/shared/ui/Button/Button.vue";

const userStore = useAuthStore();
const canCreatePoll = computed(() => userIsSeniorModerator(userStore.user));

const {
  formExpanded,
  formHovered,
  pollTitle,
  pollDetails,
  pollStartsUtc,
  pollEndsUtc,
  pollIsAnonymous,
  pollOptions,
  isSubmitting,
  errorMessage,
  toggleForm,
  addOption,
  removeOption,
  submitPoll,
} = useCreatePoll();
</script>

<template>
  <!-- Create Poll (moderators only) -->
  <template v-if="canCreatePoll">
    <button
      v-if="!formExpanded"
      type="button"
      class="toggle-link"
      @click="toggleForm"
      @mouseenter="formHovered = true"
      @mouseleave="formHovered = false"
    >
      + Создать опрос
    </button>
    <div class="expand-fold" :class="{ open: formExpanded }">
      <div class="expand-fold-clip" :inert="!formExpanded">
        <div class="poll-form">
          <form-field label="Название" name="pollTitle">
            <input
              v-model="pollTitle"
              type="text"
              placeholder="Вопрос опроса"
            />
          </form-field>

          <form-field label="Описание" name="pollDetails" optional>
            <text-area
              v-model="pollDetails"
              placeholder="Дополнительная информация"
            />
          </form-field>

          <form-field label="Начало" name="pollStartsUtc">
            <input v-model="pollStartsUtc" type="datetime-local" />
          </form-field>

          <form-field label="Окончание" name="pollEndsUtc">
            <input v-model="pollEndsUtc" type="datetime-local" />
          </form-field>

          <form-field label="Тип опроса">
            <div class="poll-type-selector">
              <label class="radio-option">
                <input type="radio" v-model="pollIsAnonymous" :value="true" />
                <span>Анонимный опрос</span>
                <span class="hint">(рекомендуется)</span>
              </label>
              <label class="radio-option">
                <input type="radio" v-model="pollIsAnonymous" :value="false" />
                <span>Публичный опрос</span>
                <span class="hint warning">Все увидят кто как голосовал</span>
              </label>
            </div>
          </form-field>

          <form-field label="Варианты ответа" name="pollOptions">
            <template #hint>Минимум два варианта</template>
            <div
              v-for="(option, index) in pollOptions"
              :key="option.id"
              class="option-row"
            >
              <input
                v-model="option.text"
                type="text"
                :placeholder="`Вариант ${index + 1}`"
              />
              <button
                v-if="pollOptions.length > 2"
                type="button"
                class="remove-option-btn"
                aria-label="Удалить вариант"
                @click="removeOption(option.id)"
              >
                {{ symbols.close }}
              </button>
            </div>
            <button type="button" class="add-option-btn" @click="addOption">
              + Добавить вариант
            </button>
          </form-field>

          <div class="form-actions">
            <Button :disabled="isSubmitting" @click="submitPoll">
              {{ isSubmitting ? "Создание…" : "Создать опрос" }}
            </Button>
            <span v-if="errorMessage" class="error-message">{{
              errorMessage
            }}</span>
          </div>
        </div>
      </div>
    </div>
  </template>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.toggle-link
  display: inline-block
  margin-bottom: $medium
  +inline-link-button

// The collapse itself is the global CSS-only .expand-fold (Reset.sass).

// Poll form styling
.poll-form
  margin-bottom: $medium
  padding: $medium
  background-color: $bg-element-overlay
  border: 1px solid $border
  border-radius: $border-radius

.option-row
  display: flex
  gap: $small
  margin-bottom: $small

  input
    flex: 1

.remove-option-btn
  flex-shrink: 0
  padding: 0 $small
  background: none
  border: none
  color: $text-muted
  cursor: pointer
  font-size: 1.2em

  &:hover
    color: $accent-red

.add-option-btn
  margin-top: $tiny
  +inline-link-button

.form-actions
  display: flex
  align-items: center
  gap: $medium

.error-message
  color: $accent-red
  font-size: $secondary-font-size

.poll-type-selector
  display: flex
  flex-direction: column
  gap: $small

.radio-option
  display: flex
  align-items: center
  gap: $tiny
  cursor: pointer

  // FormField's shared `.form-field-row input { width: 100% }` rule would
  // otherwise stretch the native radio control — override with an extra
  // attribute selector so specificity wins regardless of style load order
  input[type="radio"]
    width: auto
    margin: 0

.hint
  margin-left: $small
  font-size: $secondary-font-size
  color: $text-muted

  &.warning
    color: $accent-red
</style>
