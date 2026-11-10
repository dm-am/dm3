<script setup lang="ts">
// The BBCode editor, because the text is BBCode and a plain field would show raw tags.
import { computed, ref } from "vue";
import { GameReviewCard, gameReviewApi } from "@/entities/game";
import type { GameReview } from "@/shared/api/models/game/reviews";
import { unwrapResource } from "@/shared/api";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { BubbleAction } from "@/shared/ui/SpeechBubble";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { symbols } from "@/shared/lib/utils/icons";

const props = withDefaults(
  defineProps<{
    review: GameReview;
    showGame?: boolean;
    /** The reader may warn the author for the text: decided by the page, which knows the ranks. */
    canWarn?: boolean;
  }>(),
  { showGame: true, canWarn: false },
);

const emit = defineEmits<{
  updated: [review: GameReview];
  deleted: [id: string];
  /** "Оставить предупреждение" was pressed. */
  warn: [review: GameReview];
}>();

const isEditing = ref(false);
const editText = ref("");
const opening = ref(false);
const saving = ref(false);

const canSave = computed(() => !!editText.value.trim() && !saving.value);

// A refusal of "Редактировать" is printed under the card, where the reader looks.
const openResult = useActionResult();

async function startEdit() {
  if (opening.value || isEditing.value) return;
  openResult.clear();
  opening.value = true;
  // The card's text has the markup drawn; the author_edit answer is the text as typed.
  const { data, error } = await gameReviewApi.getGameReviewForEdit(
    props.review.gameId,
    props.review.id,
  );
  opening.value = false;
  if (error) {
    openResult.refuse(error, "Не удалось загрузить рецензию");
    return;
  }
  editText.value = unwrapResource<GameReview>(data)?.text ?? "";
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
  saveResult.clear();
}

const saveResult = useActionResult();

async function saveEdit() {
  if (!canSave.value) return;
  saving.value = true;
  saveResult.clear();
  const { data, error } = await gameReviewApi.updateGameReview(
    props.review.gameId,
    props.review.id,
    { text: editText.value },
  );
  saving.value = false;
  if (error) {
    saveResult.refuse(error, "Не удалось сохранить рецензию");
    return;
  }
  isEditing.value = false;
  editText.value = "";
  const updated = unwrapResource<GameReview>(data);
  if (updated) emit("updated", updated);
}

function handleEditKeydown(event: KeyboardEvent) {
  if (event.key === "Escape") cancelEdit();
}

const showDeleteConfirm = ref(false);

async function deleteReview() {
  const result = await gameReviewApi.deleteGameReview(
    props.review.gameId,
    props.review.id,
    OWNS_REFUSAL,
  );
  if (!result.error) emit("deleted", props.review.id);
  return result;
}
</script>

<template>
  <div v-if="isEditing" class="review-edit">
    <BBCodeEditor
      v-model="editText"
      context="common"
      placeholder="Текст рецензии"
      :disabled="saving"
      :min-height="120"
      :max-height="400"
      @submit="saveEdit"
      @keydown="handleEditKeydown"
    />
    <div class="edit-actions">
      <Button
        type="button"
        variant="primary"
        :loading="saving"
        :disabled="!canSave"
        @click="saveEdit"
      >
        Сохранить
      </Button>
      <Button type="button" @click="cancelEdit"> Отмена </Button>
    </div>
    <ActionResult :result="saveResult" class="edit-refusal" />
  </div>

  <GameReviewCard v-else :review="review" :show-game="showGame">
    <template
      v-if="review.viewer.canEdit || review.viewer.canDelete || canWarn"
      #actions
    >
      <BubbleAction
        v-if="review.viewer.canEdit"
        icon="pencil"
        label="Редактировать"
        @click="startEdit"
      />
      <BubbleAction
        v-if="review.viewer.canDelete"
        icon="trash"
        label="Удалить"
        @click="showDeleteConfirm = true"
      />
      <BubbleAction
        v-if="canWarn"
        :glyph="symbols.warning"
        label="Оставить предупреждение"
        @click="emit('warn', review)"
      />
    </template>
  </GameReviewCard>
  <ActionResult v-if="!isEditing" :result="openResult" class="edit-refusal" />

  <!-- Outside the branches, so the answer still has a window to close. -->
  <ConfirmDialog
    v-model:show="showDeleteConfirm"
    title="Удалить рецензию?"
    message="Рецензия будет удалена без возможности восстановления"
    confirm-label="Удалить"
    failure="Не удалось удалить рецензию"
    :action="deleteReview"
  />
</template>

<style scoped lang="sass">
.review-edit
  display: flex
  flex-direction: column
  gap: $small

.edit-actions
  display: flex
  gap: $small

.edit-refusal
  --action-result-margin: 0
</style>
