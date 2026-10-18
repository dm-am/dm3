<script setup lang="ts">
/**
 * TagGroupDialog — create or edit a game tag group (title / description /
 * sortOrder). Dialog twin of the former hand-rolled modal in
 * ModerationTags.vue.
 *
 * The window runs the request (Dialog `action`): it waits for the answer,
 * prints a refusal in itself and closes on success alone. A typed description
 * is multi-line text, so the window asks before it is dropped.
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
// Held as text, not as a number: the field is optional, and an empty number
// input is not zero - it is "this group limits nothing", which the API spells
// as null.
const maxTagsPerGame = ref(
  props.group?.maxTagsPerGame != null ? String(props.group.maxTagsPerGame) : "",
);

const maxTagsPerGameValue = computed<number | null>(() => {
  const raw = maxTagsPerGame.value.trim();
  if (raw === "") return null;
  const parsed = Number(raw);
  return Number.isInteger(parsed) ? parsed : null;
});

const canSubmit = computed(
  () =>
    title.value.trim().length > 0 &&
    (maxTagsPerGame.value.trim() === "" ||
      (maxTagsPerGameValue.value !== null && maxTagsPerGameValue.value >= 1)),
);

/** The description differs from what the window opened with. */
const dirty = computed(
  () => description.value !== (props.group?.description ?? ""),
);

async function send() {
  const body = {
    title: title.value,
    description: description.value || undefined,
    sortOrder: sortOrder.value,
    maxTagsPerGame: maxTagsPerGameValue.value,
  };
  const flags = { ownsRefusal: true, ownsRateLimit: true };
  const result = props.group
    ? await gameTagApi.updateTagGroup(props.group.id, body, flags)
    : await gameTagApi.createTagGroup(body, flags);
  if (!result.error) emit("success");
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось сохранить группу"
    :dirty="dirty"
    @close="emit('cancel')"
  >
    <DialogTitle>{{
      group ? "Редактировать группу" : "Новая группа"
    }}</DialogTitle>

    <Form :valid="canSubmit" action="Сохранить" cancel="Отмена">
      <FormField label="Название" name="tag-group-title" field="title">
        <input id="tag-group-title" v-model="title" type="text" required />
      </FormField>

      <FormField
        label="Описание"
        name="tag-group-description"
        field="description"
        optional
      >
        <textarea
          id="tag-group-description"
          v-model="description"
          rows="3"
        ></textarea>
      </FormField>

      <FormField
        label="Порядок сортировки"
        name="tag-group-sort"
        field="sortOrder"
      >
        <input
          id="tag-group-sort"
          v-model.number="sortOrder"
          type="number"
          min="0"
        />
      </FormField>

      <FormField
        label="Максимум тегов в игре"
        name="tag-group-max"
        field="maxTagsPerGame"
        optional
      >
        <input
          id="tag-group-max"
          v-model="maxTagsPerGame"
          type="number"
          min="1"
        />
        <template #hint>Пусто - выбор не ограничен</template>
      </FormField>
    </Form>
  </Dialog>
</template>
