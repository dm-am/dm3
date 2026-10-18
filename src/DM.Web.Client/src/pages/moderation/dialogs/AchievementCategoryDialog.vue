<script setup lang="ts">
/**
 * AchievementCategoryDialog — edit an achievement category (title /
 * description / icon / sortOrder / isActive; metric is immutable).
 * Dialog twin of the former hand-rolled modal in
 * ModerationAchievements.vue.
 *
 * The window runs the request (Dialog `action`): it waits for the answer,
 * prints a refusal in itself and closes on success alone. An edited
 * description is typed text, so the window asks before it is dropped.
 */
import { ref, computed } from "vue";
import { achievementApi } from "@/entities/achievement";
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

const canSubmit = computed(
  () =>
    title.value.trim().length > 0 &&
    description.value.trim().length > 0 &&
    iconName.value.trim().length > 0,
);

/** The description differs from what the window opened with. */
const dirty = computed(() => description.value !== props.category.description);

async function send() {
  const result = await achievementApi.updateAchievementCategory(
    props.category.id,
    {
      title: title.value,
      description: description.value,
      iconName: iconName.value,
      sortOrder: sortOrder.value,
      isActive: isActive.value,
    },
    { ownsRefusal: true, ownsRateLimit: true },
  );
  if (!result.error) emit("success");
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось сохранить категорию"
    :dirty="dirty"
    @close="emit('cancel')"
  >
    <DialogTitle>Категория: {{ category.code }}</DialogTitle>

    <Form :valid="canSubmit" action="Сохранить" cancel="Отмена">
      <FormField label="Название" name="achievement-cat-title" field="title">
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
        field="description"
      >
        <textarea
          id="achievement-cat-description"
          v-model="description"
          rows="3"
          maxlength="1000"
          required
        ></textarea>
      </FormField>

      <FormField
        label="Имя иконки"
        name="achievement-cat-icon"
        field="iconName"
      >
        <input
          id="achievement-cat-icon"
          v-model="iconName"
          type="text"
          required
          maxlength="80"
        />
      </FormField>

      <FormField label="Порядок" name="achievement-cat-sort" field="sortOrder">
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
