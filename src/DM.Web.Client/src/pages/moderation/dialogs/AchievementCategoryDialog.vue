<script setup lang="ts">
/**
 * AchievementCategoryDialog — edit an achievement category (title /
 * description / icon / sortOrder / isActive; metric is immutable).
 * Dialog twin of the former hand-rolled modal in
 * ModerationAchievements.vue.
 */
import { ref, computed } from "vue";
import { achievementApi } from "@/shared/api";
import type { AchievementCategory } from "@/shared/api/models/achievements";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

const props = defineProps<{
  category: AchievementCategory;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const title = ref(props.category.title);
const description = ref(props.category.description);
const iconName = ref(props.category.iconName);
const sortOrder = ref(props.category.sortOrder);
const isActive = ref(props.category.isActive);

const saving = ref(false);
const error = ref<string | null>(null);

const canSubmit = computed(
  () =>
    title.value.trim().length > 0 &&
    description.value.trim().length > 0 &&
    iconName.value.trim().length > 0,
);

async function submit() {
  if (!canSubmit.value || saving.value) return;
  saving.value = true;
  error.value = null;
  try {
    await achievementApi.updateAchievementCategory(props.category.id, {
      title: title.value,
      description: description.value,
      iconName: iconName.value,
      sortOrder: sortOrder.value,
      isActive: isActive.value,
    });
    emit("success");
  } catch {
    error.value = "Не удалось сохранить категорию";
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <Dialog narrow>
    <DialogTitle>Категория: {{ category.code }}</DialogTitle>

    <Form
      :valid="canSubmit"
      :loading="saving"
      action="Сохранить"
      cancel="Отмена"
      @submit="submit"
      @cancel="emit('cancel')"
    >
      <FormField
        label="Название"
        name="achievement-cat-title"
        :errors="error ? [error] : []"
      >
        <input
          id="achievement-cat-title"
          v-model="title"
          type="text"
          required
          maxlength="200"
        />
      </FormField>

      <FormField
        label="Описание (показывается в popover)"
        name="achievement-cat-description"
      >
        <textarea
          id="achievement-cat-description"
          v-model="description"
          rows="3"
          maxlength="1000"
          required
        ></textarea>
      </FormField>

      <FormField label="Имя иконки" name="achievement-cat-icon">
        <input
          id="achievement-cat-icon"
          v-model="iconName"
          type="text"
          required
          maxlength="80"
        />
      </FormField>

      <FormField label="Порядок" name="achievement-cat-sort">
        <input
          id="achievement-cat-sort"
          v-model.number="sortOrder"
          type="number"
          min="0"
        />
      </FormField>

      <FormField name="achievement-cat-active">
        <label class="checkbox-label">
          <input v-model="isActive" type="checkbox" />
          Активна
        </label>
      </FormField>
    </Form>
  </Dialog>
</template>

<style scoped lang="sass">
.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer
</style>
