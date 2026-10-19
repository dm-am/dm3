<script setup lang="ts">
/**
 * Creates or edits a game tag. The window runs the request; a typed
 * description is asked about before it is dropped. The description is the
 * tag's tooltip.
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
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = defineProps<{
  /** Tag to edit, its description as typed (author_edit); null creates a new tag. */
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

const canSubmit = computed(
  () => title.value.trim().length > 0 && !!groupId.value,
);

/** The description differs from what the window opened with. */
const dirty = computed(
  () => description.value !== (props.tag?.description ?? ""),
);

async function send() {
  const body = {
    groupId: groupId.value,
    title: title.value,
    description: description.value || undefined,
    sortOrder: sortOrder.value,
  };
  const flags = OWNS_REFUSAL;
  const result = props.tag
    ? await gameTagApi.updateTag(props.tag.id, body, flags)
    : await gameTagApi.createTag(body, flags);
  if (!result.error) emit("success");
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось сохранить тег"
    :dirty="dirty"
    @close="emit('cancel')"
  >
    <DialogTitle>{{ tag ? "Редактировать тег" : "Новый тег" }}</DialogTitle>

    <Form :valid="canSubmit" action="Сохранить" cancel="Отмена">
      <FormField label="Группа" name="tag-group" field="groupId">
        <Select
          id="tag-group"
          :model-value="groupId"
          :options="groupOptions"
          @update:model-value="(v) => (groupId = v)"
        />
      </FormField>

      <FormField label="Название" name="tag-title" field="title">
        <input id="tag-title" v-model="title" type="text" required />
      </FormField>

      <FormField
        label="Описание"
        name="tag-description"
        field="description"
        optional
      >
        <textarea
          id="tag-description"
          v-model="description"
          rows="3"
        ></textarea>
      </FormField>

      <FormField label="Порядок сортировки" name="tag-sort" field="sortOrder">
        <input id="tag-sort" v-model.number="sortOrder" type="number" min="0" />
      </FormField>
    </Form>
  </Dialog>
</template>
