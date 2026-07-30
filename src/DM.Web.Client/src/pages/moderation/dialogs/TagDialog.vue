<script setup lang="ts">
/**
 * TagDialog — create or edit a game tag (group / title / description /
 * sortOrder). Dialog twin of the former hand-rolled modal in
 * ModerationTags.vue.
 */
import { ref, computed } from "vue";
import {
  gameTagApi,
  type ModerationTag,
  type ModerationTagGroup,
} from "@/entities/game";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";

const props = defineProps<{
  /** Tag to edit; null creates a new tag. */
  tag: ModerationTag | null;
  /** All groups for the group selector. */
  groups: ModerationTagGroup[];
  /** Preselected group for a new tag. */
  defaultGroupId: string;
  /** Suggested sort order for a new tag. */
  defaultSortOrder: number;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const groupId = ref(props.tag?.groupId ?? props.defaultGroupId);
const title = ref(props.tag?.title ?? "");
const description = ref(props.tag?.description ?? "");
const sortOrder = ref(props.tag?.sortOrder ?? props.defaultSortOrder);

const groupOptions = computed<SelectOption[]>(() =>
  props.groups.map((g) => ({ value: g.id, label: g.title })),
);

const saving = ref(false);
const error = ref<string | null>(null);

const canSubmit = computed(
  () => title.value.trim().length > 0 && !!groupId.value,
);

async function submit() {
  if (!canSubmit.value || saving.value) return;
  saving.value = true;
  error.value = null;
  try {
    if (props.tag) {
      await gameTagApi.updateTag(props.tag.id, {
        groupId: groupId.value,
        title: title.value,
        description: description.value || undefined,
        sortOrder: sortOrder.value,
      });
    } else {
      await gameTagApi.createTag({
        groupId: groupId.value,
        title: title.value,
        description: description.value || undefined,
        sortOrder: sortOrder.value,
      });
    }
    emit("success");
  } catch {
    error.value = "Не удалось сохранить тег";
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <Dialog narrow>
    <DialogTitle>{{ tag ? "Редактировать тег" : "Новый тег" }}</DialogTitle>

    <Form
      :valid="canSubmit"
      :loading="saving"
      action="Сохранить"
      cancel="Отмена"
      @submit="submit"
      @cancel="emit('cancel')"
    >
      <FormField label="Группа" name="tag-group" :errors="error ? [error] : []">
        <Select
          :model-value="groupId"
          :options="groupOptions"
          @update:model-value="(v) => (groupId = v)"
        />
      </FormField>

      <FormField label="Название" name="tag-title">
        <input id="tag-title" v-model="title" type="text" required />
      </FormField>

      <FormField label="Описание" name="tag-description" optional>
        <textarea
          id="tag-description"
          v-model="description"
          rows="3"
        ></textarea>
        <template #hint>
          Используйте [tipimg:URL]текст[/tipimg] для картинки в тултипе
        </template>
      </FormField>

      <FormField label="Порядок сортировки" name="tag-sort">
        <input id="tag-sort" v-model.number="sortOrder" type="number" min="0" />
      </FormField>
    </Form>
  </Dialog>
</template>
