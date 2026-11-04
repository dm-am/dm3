<script setup lang="ts">
/** The edit of a published post, in its place in the card. */
import { ref } from "vue";
import { postApi } from "@/entities/game";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { Button } from "@/shared/ui/Button";

const props = defineProps<{
  postId: string;
  /** The author's texts as typed (author_edit), the editors' seed: the host opens the edit on them alone. */
  gameText: string;
  metagameText: string;
  isModerator: boolean;
  /**
   * A prop, not an emit: Vue drops the emit of an edit "Отмена" closed mid-save.
   * Awaited, so "Сохранить" waits for the files the host sends after the text.
   */
  onSaved: (edit: {
    gameText: string;
    metagameText: string;
    editedAt: string;
  }) => void | Promise<void>;
}>();

const emit = defineEmits<{ cancel: [] }>();

const editGameText = ref(props.gameText);
const editMetaText = ref(props.metagameText);
const savingPost = ref(false);

/** A refused save, under the buttons of the edit; the text stays in it. */
const editResult = useActionResult();

async function saveEditPost() {
  if (!editGameText.value.trim() || savingPost.value) return;
  savingPost.value = true;
  editResult.clear();
  const { data, error } = await postApi.updatePost(props.postId, {
    gameText: editGameText.value,
    // Always sent: an empty one is how the edit erases it.
    metagameText: editMetaText.value,
  });
  if (error) {
    savingPost.value = false;
    // The server names the game text by its field: the line names it by its label.
    editResult.refuse(error, "Не удалось сохранить пост", {
      gameText: "Игровой текст",
    });
    return;
  }
  await props.onSaved({
    gameText: data?.resource?.gameText ?? editGameText.value,
    metagameText: data?.resource?.metagameText ?? "",
    editedAt:
      data?.resource?.edits?.[0]?.modifiedUtc ?? new Date().toISOString(),
  });
  savingPost.value = false;
}
</script>

<template>
  <div class="post-edit">
    <span class="edit-label">Игровой текст</span>
    <BBCodeEditor
      v-model="editGameText"
      context="post"
      placeholder="Игровой текст поста..."
      :min-height="120"
      :is-moderator="isModerator"
    />
    <span class="edit-label">Метаигровой текст</span>
    <!-- Checked as the comment surface the metagame text is saved on -->
    <BBCodeEditor
      v-model="editMetaText"
      context="post"
      check-context="common"
      placeholder="Метаигровой комментарий (необязательно)..."
      :min-height="60"
      :max-height="200"
      :is-moderator="isModerator"
    />
    <!-- Files are requests of their own, not part of the text's patch -->
    <div class="edit-attachments">
      <span class="edit-label">Вложения</span>
      <slot name="attachments" />
    </div>

    <div class="edit-actions">
      <Button
        type="button"
        variant="primary"
        class="edit-btn save"
        :disabled="savingPost || !editGameText.trim()"
        @click="saveEditPost"
      >
        Сохранить
      </Button>
      <Button type="button" class="edit-btn" @click="emit('cancel')">
        Отмена
      </Button>
    </div>
    <ActionResult :result="editResult" />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.post-edit
  display: flex
  flex-direction: column
  gap: $tiny
  margin: 11px 10px

// A caption of a field of the page: +field-caption, as FormField draws it.
.edit-label
  +field-caption

// The files of the post, a column of their own under the editors.
.edit-attachments
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $tiny
  margin-top: $small

.edit-actions
  display: flex
  gap: $small
  margin-top: $small
</style>
