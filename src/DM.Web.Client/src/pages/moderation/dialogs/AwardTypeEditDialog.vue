<script setup lang="ts">
/**
 * Edits an award type. The window runs the request, prints a refusal in
 * itself and closes on success alone; an edited description is typed text,
 * so the window asks before it is dropped.
 */
import { ref, computed } from "vue";
import {
  achievementApi,
  CATALOG_TITLE_MAX_LENGTH,
  CATALOG_DESCRIPTION_MAX_LENGTH,
  CATALOG_ICON_NAME_MAX_LENGTH,
  MIN_TIER,
  MAX_TIER,
  TIER_SCALE_HINT,
} from "@/entities/achievement";
import type { AwardType } from "@/shared/api/models/achievements";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

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

const canSubmit = computed(
  () =>
    title.value.trim().length > 0 &&
    description.value.trim().length > 0 &&
    iconName.value.trim().length > 0,
);

/** The description differs from what the window opened with. */
const dirty = computed(() => description.value !== props.awardType.description);

async function send() {
  const result = await achievementApi.updateAwardType(
    props.awardType.id,
    {
      title: title.value,
      description: description.value,
      iconName: iconName.value,
      tier: tier.value,
      sortOrder: sortOrder.value,
    },
    OWNS_REFUSAL,
  );
  if (!result.error) emit("success");
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось сохранить"
    :dirty="dirty"
    @close="emit('cancel')"
  >
    <DialogTitle>Редактировать тип: {{ awardType.code }}</DialogTitle>

    <Form :valid="canSubmit" action="Сохранить" cancel="Отмена">
      <FormField label="Название" name="award-type-title" field="title">
        <input
          id="award-type-title"
          v-model="title"
          type="text"
          required
          :maxlength="CATALOG_TITLE_MAX_LENGTH"
        />
      </FormField>

      <FormField
        label="Описание"
        name="award-type-description"
        field="description"
      >
        <textarea
          id="award-type-description"
          v-model="description"
          rows="3"
          :maxlength="CATALOG_DESCRIPTION_MAX_LENGTH"
          required
        ></textarea>
      </FormField>

      <FormField label="Имя иконки" name="award-type-icon" field="iconName">
        <input
          id="award-type-icon"
          v-model="iconName"
          type="text"
          required
          :maxlength="CATALOG_ICON_NAME_MAX_LENGTH"
        />
        <template #hint>Из набора иконок</template>
      </FormField>

      <FormField label="Тир" name="award-type-tier" field="tier">
        <input
          id="award-type-tier"
          v-model.number="tier"
          type="number"
          :min="MIN_TIER"
          :max="MAX_TIER"
        />
        <template #hint>{{ TIER_SCALE_HINT }}</template>
      </FormField>

      <FormField
        label="Порядок сортировки"
        name="award-type-sort"
        field="sortOrder"
      >
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
