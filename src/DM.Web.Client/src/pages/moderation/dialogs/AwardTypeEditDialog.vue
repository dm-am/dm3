<script setup lang="ts">
/**
 * AwardTypeEditDialog — edit an award-type catalog entry (title /
 * description / icon / tier / sortOrder).
 *
 * The window runs the request (Dialog `action`): it waits for the answer,
 * prints a refusal in itself - a name the catalogue already carries as well as
 * a refusal the interceptor used to say in a plate over it - and closes on
 * success alone. An edited description is typed text, so the window asks
 * before it is dropped.
 */
import { ref, computed } from "vue";
import { achievementApi } from "@/entities/achievement";
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
    { ownsRefusal: true, ownsRateLimit: true },
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
          maxlength="200"
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
          maxlength="1000"
          required
        ></textarea>
      </FormField>

      <FormField
        label="Имя иконки (из game-icons sprite)"
        name="award-type-icon"
        field="iconName"
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
        field="tier"
      >
        <input
          id="award-type-tier"
          v-model.number="tier"
          type="number"
          min="0"
          max="9"
        />
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
