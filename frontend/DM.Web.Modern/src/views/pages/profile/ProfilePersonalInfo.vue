<script setup lang="ts">
import { computed } from "vue";
import type { User } from "@/api/models/community";
import { Gender } from "@/api/models/community";
import EditableField from "@/components/inputs/EditableField.vue";
import dayjs from "dayjs";

const props = defineProps<{
  user: User;
  isEditMode: boolean;
}>();

const emit = defineEmits<{
  (e: "updateField", field: string, value: string): void;
}>();

const genderNames: Record<Gender, string> = {
  [Gender.Unknown]: "",
  [Gender.Male]: "Мужской",
  [Gender.Female]: "Женский",
};

const displayGender = computed(() =>
  props.user.gender ? genderNames[props.user.gender as Gender] : "",
);

const displayBirthday = computed(() => {
  if (!props.user.birthdayDate) return "";
  return dayjs(props.user.birthdayDate).format("DD.MM");
});
</script>

<template>
  <section class="profile-personal-info">
    <h3 class="section-title">Личная информация</h3>

    <div class="info-grid">
      <editable-field
        :modelValue="user.name"
        :editing="isEditMode"
        label="Имя"
        placeholder="Введите имя"
        @update:modelValue="emit('updateField', 'name', $event)"
      />

      <editable-field
        :modelValue="user.location"
        :editing="isEditMode"
        label="Местоположение"
        placeholder="Город, страна"
        @update:modelValue="emit('updateField', 'location', $event)"
      />

      <div class="field-group">
        <span class="field-label">Пол</span>
        <span class="field-value" :class="{ 'is-empty': !displayGender }">
          {{ displayGender || "Не указан" }}
        </span>
      </div>

      <div class="field-group">
        <span class="field-label">День рождения</span>
        <span class="field-value" :class="{ 'is-empty': !displayBirthday }">
          {{ displayBirthday || "Не указан" }}
        </span>
      </div>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-personal-info
  background: $bg-element
  border-radius: $border-radius
  padding: $medium
  margin-bottom: $medium

.section-title
  color: $text
  margin: 0 0 $medium
  font-size: 1rem

.info-grid
  display: grid
  grid-template-columns: repeat(2, 1fr)
  gap: $medium

.field-group
  display: flex
  flex-direction: column

.field-label
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $tiny

.field-value
  color: $text

  &.is-empty
    color: $text-meta
    font-style: italic
</style>
