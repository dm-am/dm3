<script setup lang="ts">
/**
 * TagGroupDialog — create or edit a game tag group (title / description /
 * sortOrder). Dialog twin of the former hand-rolled modal in
 * ModerationTags.vue.
 */
import { ref, computed } from "vue";
import { gameTagApi, type ModerationTagGroup } from "@/entities/game";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

const props = defineProps<{
  /** Group to edit; null creates a new group. */
  group: ModerationTagGroup | null;
  /** Suggested sort order for a new group. */
  defaultSortOrder: number;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const title = ref(props.group?.title ?? "");
const description = ref(props.group?.description ?? "");
const sortOrder = ref(props.group?.sortOrder ?? props.defaultSortOrder);

const saving = ref(false);
const error = ref<string | null>(null);

const canSubmit = computed(() => title.value.trim().length > 0);

async function submit() {
  if (!canSubmit.value || saving.value) return;
  saving.value = true;
  error.value = null;
  try {
    if (props.group) {
      await gameTagApi.updateTagGroup(props.group.id, {
        title: title.value,
        description: description.value || undefined,
        sortOrder: sortOrder.value,
      });
    } else {
      await gameTagApi.createTagGroup({
        title: title.value,
        description: description.value || undefined,
        sortOrder: sortOrder.value,
      });
    }
    emit("success");
  } catch {
    error.value = "Не удалось сохранить группу";
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <Dialog narrow>
    <DialogTitle>{{
      group ? "Редактировать группу" : "Новая группа"
    }}</DialogTitle>

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
        name="tag-group-title"
        :errors="error ? [error] : []"
      >
        <input id="tag-group-title" v-model="title" type="text" required />
      </FormField>

      <FormField label="Описание" name="tag-group-description" optional>
        <textarea
          id="tag-group-description"
          v-model="description"
          rows="3"
        ></textarea>
      </FormField>

      <FormField label="Порядок сортировки" name="tag-group-sort">
        <input
          id="tag-group-sort"
          v-model.number="sortOrder"
          type="number"
          min="0"
        />
      </FormField>
    </Form>
  </Dialog>
</template>
