<script setup lang="ts">
import { ref } from "vue";
import type { SubmitResult } from "@/shared/api/models/common";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import Button from "@/shared/ui/Button/Button.vue";

const props = defineProps<{
  commentId: string;
  source: string;
  isModerator: boolean;
  /** Only its answer may close the edit. */
  submitEdit?: (id: string, text: string) => Promise<SubmitResult>;
}>();

const emit = defineEmits<{ saved: []; cancel: [] }>();

const editText = ref(props.source);

const saving = ref(false);

const editResult = useActionResult();

async function saveEdit() {
  const submitEdit = props.submitEdit;
  if (!submitEdit || !editText.value.trim() || saving.value) return;
  saving.value = true;
  editResult.clear();
  const { error } = await submitEdit(props.commentId, editText.value);
  saving.value = false;
  if (error) {
    // The server names the command's field, not what it is: the line names it.
    editResult.refuse(error, "Не удалось сохранить комментарий", {
      text: "Текст комментария",
    });
    return;
  }
  emit("saved");
}

function handleEditKeydown(e: KeyboardEvent) {
  // Ctrl+Enter is the editor's own submit; Escape cancels, as in the create editor.
  if (e.key === "Escape") {
    emit("cancel");
  }
}
</script>

<template>
  <div class="edit-container">
    <BBCodeEditor
      v-model="editText"
      context="common"
      placeholder="Редактирование комментария..."
      :min-height="100"
      :max-height="300"
      :is-moderator="isModerator"
      @submit="saveEdit"
      @keydown="handleEditKeydown"
    />
    <div class="edit-actions">
      <Button
        type="button"
        variant="primary"
        :loading="saving"
        @click="saveEdit"
      >
        Сохранить
      </Button>
      <Button type="button" @click="emit('cancel')"> Отмена </Button>
    </div>
    <ActionResult :result="editResult" />
  </div>
</template>

<style scoped lang="sass">
.edit-container
  margin-bottom: $small

.edit-actions
  display: flex
  gap: $small
  margin-top: $small
</style>
