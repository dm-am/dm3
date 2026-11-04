<script setup lang="ts">
import { computed } from "vue";
import { useAuthStore, userIsSeniorModerator } from "@/entities/user";
import { PollTypeField } from "@/entities/poll";
import { RemoveCross } from "@/shared/ui/RemoveButton";
import { useCreatePoll } from "../model";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { DateTimeInput } from "@/shared/ui/DatePicker";

const userStore = useAuthStore();
const canCreatePoll = computed(() => userIsSeniorModerator(userStore.user));

const {
  formExpanded,
  pollTitle,
  pollDetails,
  pollStartsUtc,
  pollEndsUtc,
  startsMin,
  endsMin,
  endsMax,
  startsReason,
  endsReason,
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
  <template v-if="canCreatePoll">
    <button
      v-if="!formExpanded"
      type="button"
      class="toggle-link"
      @click="toggleForm"
    >
      + Создать опрос
    </button>
    <div class="expand-fold" :class="{ open: formExpanded }">
      <div class="expand-fold-clip" :inert="!formExpanded">
        <div class="poll-form">
          <form-field label="Название" name="pollTitle">
            <input
              id="poll-title"
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

          <form-field
            label="Начало"
            name="pollStartsUtc"
            :errors="startsReason ? [startsReason] : []"
          >
            <DateTimeInput
              id="poll-starts"
              v-model="pollStartsUtc"
              :min="startsMin"
              @reason="startsReason = $event"
            />
          </form-field>

          <form-field
            label="Окончание"
            name="pollEndsUtc"
            :errors="endsReason ? [endsReason] : []"
          >
            <DateTimeInput
              id="poll-ends"
              v-model="pollEndsUtc"
              :min="endsMin"
              :max="endsMax"
              @reason="endsReason = $event"
            />
          </form-field>

          <PollTypeField v-model="pollIsAnonymous" />

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
                :aria-label="`Вариант ${index + 1}`"
              />
              <RemoveCross
                v-if="pollOptions.length > 2"
                label="Удалить вариант"
                @click="removeOption(option.id)"
              />
            </div>
            <button type="button" class="add-option-btn" @click="addOption">
              + Добавить вариант
            </button>
          </form-field>

          <div class="form-actions">
            <Button
              variant="primary"
              :loading="isSubmitting"
              @click="submitPoll"
            >
              Создать опрос
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
@use "@/assets/styles/Links" as *

.toggle-link
  display: inline-block
  margin-bottom: $medium
  +inline-link-button


.poll-form
  margin-bottom: $medium
  padding: $medium
  background-color: $bg-element-overlay
  border: 1px solid $border

.option-row
  display: flex
  gap: $small
  margin-bottom: $small

  input
    flex: 1

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
</style>
