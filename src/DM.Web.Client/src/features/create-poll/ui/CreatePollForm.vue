<script setup lang="ts">
import { computed } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import { useUserStore, userIsSeniorModerator } from "@/entities/user";
import { useCreatePoll } from "../model";
import Button from "@/shared/ui/Button/Button.vue";

const userStore = useUserStore();
const canCreatePoll = computed(() => userIsSeniorModerator(userStore.user));

const {
  formExpanded,
  formHovered,
  formContent,
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
    <a
      v-if="!formExpanded"
      class="toggle-link"
      @click="toggleForm"
      @mouseenter="formHovered = true"
      @mouseleave="formHovered = false"
    >
      + Создать опрос
    </a>
    <div
      ref="formContent"
      class="poll-form-wrapper"
      :class="{ collapsed: !formExpanded }"
    >
      <div class="poll-form">
        <div class="form-field">
          <label class="form-label"><strong>Название</strong></label>
          <input
            v-model="pollTitle"
            type="text"
            class="form-input"
            placeholder="Вопрос опроса"
          />
        </div>
        <div class="form-field">
          <label class="form-label"><strong>Описание</strong> (опционально)</label>
          <textarea
            v-model="pollDetails"
            class="form-input form-textarea"
            placeholder="Дополнительная информация"
            rows="2"
          />
        </div>
        <div class="form-field">
          <label class="form-label"><strong>Начало</strong></label>
          <input v-model="pollStartsUtc" type="datetime-local" class="form-input" />
        </div>
        <div class="form-field">
          <label class="form-label"><strong>Окончание</strong></label>
          <input v-model="pollEndsUtc" type="datetime-local" class="form-input" />
        </div>
        <div class="form-field">
          <label class="form-label"><strong>Тип опроса</strong></label>
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
        </div>
        <div class="form-field">
          <label class="form-label"><strong>Варианты ответа</strong></label>
          <div v-for="(option, index) in pollOptions" :key="option.id" class="option-row">
            <input
              v-model="option.text"
              type="text"
              class="form-input"
              :placeholder="`Вариант ${index + 1}`"
            />
            <button
              v-if="pollOptions.length > 2"
              type="button"
              class="remove-option-btn"
              @click="removeOption(option.id)"
            >
              {{ symbols.close }}
            </button>
          </div>
          <button type="button" class="add-option-btn" @click="addOption">
            + Добавить вариант
          </button>
        </div>
        <div class="form-actions">
          <Button :disabled="isSubmitting" @click="submitPoll">
            {{ isSubmitting ? "Создание..." : "Создать опрос" }}
          </Button>
          <span v-if="errorMessage" class="error-message">{{ errorMessage }}</span>
        </div>
      </div>
    </div>
  </template>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.toggle-link
  display: inline-block
  margin-bottom: $medium
  color: $link
  cursor: pointer
  &:hover
    color: $link-hover

// Collapsible form wrapper
.poll-form-wrapper
  overflow: hidden
  transition: height 0.2s ease

  &.collapsed
    height: 0

// Poll form styling
.poll-form
  margin-bottom: $medium
  padding: $medium
  background-color: $bg-element-overlay
  border: 1px dashed $border

.form-field
  margin-bottom: $medium

.form-label
  display: block
  margin-bottom: $tiny
  color: $text
  font-size: $font-size

.form-input
  display: block
  width: 100%
  box-sizing: border-box
  +input()

.form-textarea
  resize: vertical
  min-height: 60px
  font-family: inherit

.option-row
  display: flex
  gap: $small
  margin-bottom: $small

  .form-input
    flex: 1

.remove-option-btn
  padding: 0 $small
  border: 1px dashed $border
  background-color: $bg-element
  color: $text-muted
  cursor: pointer
  font-size: 1.2em

  &:hover
    color: $accent-red

.add-option-btn
  padding: $tiny $small
  border: none
  background: transparent
  color: $link
  cursor: pointer
  font-size: $secondary-font-size

  &:hover
    text-decoration: underline

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

  input
    margin: 0

.hint
  margin-left: $small
  font-size: $secondary-font-size
  color: $text-muted

  &.warning
    color: $accent-red
</style>
