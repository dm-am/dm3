<script setup lang="ts">
/**
 * TagDialog — create or edit a game tag (group / title / description /
 * sortOrder). Dialog twin of the former hand-rolled modal in
 * ModerationTags.vue.
 *
 * The window runs the request (Dialog `action`): it waits for the answer,
 * prints a refusal in itself and closes on success alone. A typed description
 * is multi-line text, so the window asks before it is dropped.
 *
 * The description is the tag's tooltip, whose one tag is [tip]: the field
 * writes it with two buttons and checks it as the page will read it
 * (HintField), and the preview under it draws the tooltip a reader will get
 * (HintPreview).
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
import HintField from "./HintField.vue";
import HintPreview from "./HintPreview.vue";

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
  const flags = { ownsRefusal: true, ownsRateLimit: true };
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
        <HintField id="tag-description" v-model="description" />
      </FormField>

      <FormField label="Предпросмотр" name="tag-preview">
        <HintPreview :description="description" :title="title" />
      </FormField>

      <FormField label="Порядок сортировки" name="tag-sort" field="sortOrder">
        <input id="tag-sort" v-model.number="sortOrder" type="number" min="0" />
      </FormField>
    </Form>
  </Dialog>
</template>
