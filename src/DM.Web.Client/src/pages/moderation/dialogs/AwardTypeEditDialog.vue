<script setup lang="ts">
/**
 * AwardTypeEditDialog — edit an award-type catalog entry (title /
 * description / icon / tier / sortOrder). Dialog twin of the former
 * hand-rolled modal in ModerationAwardTypes.vue.
 */
import { ref, computed } from "vue";
import { achievementApi } from "@/shared/api";
import type { AwardType } from "@/shared/api/models/achievements";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

const props = defineProps<{
  awardType: AwardType;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const title = ref(props.awardType.title);
const description = ref(props.awardType.description);
const iconName = ref(props.awardType.iconName);
const tier = ref<number | null>(props.awardType.tier ?? null);
const sortOrder = ref(props.awardType.sortOrder);

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
  const { error: e } = await achievementApi.updateAwardType(
    props.awardType.id,
    {
      title: title.value,
      description: description.value,
      iconName: iconName.value,
      tier: tier.value,
      sortOrder: sortOrder.value,
    },
  );
  saving.value = false;
  if (e) {
    error.value = e.title ?? "Не удалось сохранить";
    return;
  }
  emit("success");
}
</script>

<template>
  <Dialog narrow>
    <DialogTitle>Редактировать тип: {{ awardType.code }}</DialogTitle>

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
        name="award-type-title"
        :errors="error ? [error] : []"
      >
        <input
          id="award-type-title"
          v-model="title"
          type="text"
          required
          maxlength="200"
        />
      </FormField>

      <FormField label="Описание" name="award-type-description">
        <textarea
          id="award-type-description"
          v-model="description"
          rows="3"
          maxlength="1000"
          required
        ></textarea>
      </FormField>

      <FormField
        label="Имя иконки (из game-icons sprite)"
        name="award-type-icon"
      >
        <input
          id="award-type-icon"
          v-model="iconName"
          type="text"
          required
          maxlength="80"
        />
      </FormField>

      <FormField
        label="Tier (1 gold / 2 silver / 3 bronze / 5 diamond / null)"
        name="award-type-tier"
      >
        <input
          id="award-type-tier"
          v-model.number="tier"
          type="number"
          min="0"
          max="9"
        />
      </FormField>

      <FormField label="Порядок сортировки" name="award-type-sort">
        <input
          id="award-type-sort"
          v-model.number="sortOrder"
          type="number"
          min="0"
        />
      </FormField>
    </Form>
  </Dialog>
</template>
