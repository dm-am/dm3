<script setup lang="ts">
/**
 * GameReviewEditable — a game review with the controls of whoever may act on
 * it: the pencil and the trash in the top right corner of the bubble.
 *
 * The pencil rewrites the review where it stands, the way a website
 * testimonial is rewritten (TestimonialEditable): the card is replaced by the
 * site's BBCode editor with "Сохранить" / "Отмена", and Escape cancels. The
 * editor is the one the review was written in, because the text is BBCode and
 * a plain field would hand the author their markup as raw tags. The trash asks
 * in a confirmation window first; the window waits shut for the answer,
 * prints a refusal in itself and closes on success alone.
 *
 * Who is shown what repeats the server and does not widen it:
 * GameReviewIntentionResolver admits Edit to the author alone and Delete to
 * the author or to SeniorModerator and above, and GameReviewService closes the
 * author's edit window a day after publication, an admin exempt from it.
 */
import { computed, ref } from "vue";
import dayjs from "dayjs";
import { storeToRefs } from "pinia";
import { GameReviewCard, gameApi } from "@/entities/game";
import {
  useAuthStore,
  userIsAdmin,
  userIsSeniorModerator,
} from "@/entities/user";
import type { GameReview } from "@/shared/api/models/game/reviews";
import { unwrapResource } from "@/shared/api";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { BubbleAction } from "@/shared/ui/SpeechBubble";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";

const props = withDefaults(
  defineProps<{
    review: GameReview;
    /** Forwarded to the card: name the reviewed game in the footer. */
    showGame?: boolean;
    /** Search query for highlighting, forwarded to the card. */
    searchQuery?: string;
  }>(),
  { showGame: true, searchQuery: undefined },
);

const emit = defineEmits<{
  /** The server took the edit; carries the review as it now reads. */
  updated: [review: GameReview];
  /** The server took the review down; carries its id. */
  deleted: [id: string];
}>();

/**
 * The author's correction window, mirroring GameReviewService.EditWindow. The
 * server is authoritative and refuses a late PATCH; this is what keeps the
 * pencil from offering an edit that would come back forbidden.
 */
const EDIT_WINDOW_HOURS = 24;

/**
 * The shortest text the server keeps (UpdateGameReviewRequest), the bound the
 * create form's button already holds.
 */
const MIN_TEXT_LENGTH = 10;

const { user: currentUser } = storeToRefs(useAuthStore());

// The server compares identifiers, so this does too: a username can change,
// and the two sides would then disagree about the same person.
const isAuthor = computed(
  () => !!currentUser.value && currentUser.value.id === props.review.author?.id,
);

const withinEditWindow = computed(
  () =>
    dayjs().diff(dayjs(props.review.createdUtc), "hour", true) <=
    EDIT_WINDOW_HOURS,
);

const canEdit = computed(
  () =>
    isAuthor.value &&
    (withinEditWindow.value || userIsAdmin(currentUser.value)),
);

const canDelete = computed(
  () => isAuthor.value || userIsSeniorModerator(currentUser.value),
);

// ── Edit in place ─────────────────────────────────────────────────────────
const isEditing = ref(false);
const editText = ref("");
const opening = ref(false);
const saving = ref(false);

const canSave = computed(
  () => editText.value.trim().length >= MIN_TEXT_LENGTH && !saving.value,
);

async function startEdit() {
  if (opening.value || isEditing.value) return;
  // The card shows the display rendering; the editor needs what the author
  // wrote. Seeded from the display text first, so a failed request opens the
  // editor anyway instead of refusing to open at all (as the post review does).
  editText.value = htmlToBbcode(props.review.text ?? "");
  opening.value = true;
  const { data, error } = await gameApi.getGameReviewForEdit(
    props.review.gameId,
    props.review.id,
  );
  opening.value = false;
  if (!error) {
    const source = unwrapResource<GameReview>(data)?.text;
    if (source) editText.value = htmlToBbcode(source);
  }
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
  saveResult.clear();
}

/** A refused save, under the buttons of the editor. */
const saveResult = useActionResult();

async function saveEdit() {
  if (!canSave.value) return;
  saving.value = true;
  saveResult.clear();
  const { data, error } = await gameApi.updateGameReview(
    props.review.gameId,
    props.review.id,
    { text: editText.value.trim() },
  );
  saving.value = false;
  if (error) {
    // Keep the editor open with the text still in it, so a refusal (the day
    // has run out, the text is too long) costs nothing that was typed, and
    // say it under the buttons that were pressed.
    saveResult.refuse(error, "Не удалось сохранить рецензию");
    return;
  }
  isEditing.value = false;
  editText.value = "";
  const updated = unwrapResource<GameReview>(data);
  if (updated) emit("updated", updated);
}

function handleEditKeydown(event: KeyboardEvent) {
  // Ctrl+Enter saves through the editor's own "submit"; Escape is added here,
  // the create editor's pair of shortcuts.
  if (event.key === "Escape") cancelEdit();
}

// ── Delete ────────────────────────────────────────────────────────────────
const showDeleteConfirm = ref(false);

/**
 * The confirmation's action. The review leaves the list only once the server
 * has taken it down; a refusal stays in the window with the delete ready for a
 * retry.
 */
async function deleteReview() {
  const result = await gameApi.deleteGameReview(
    props.review.gameId,
    props.review.id,
    { ownsRefusal: true, ownsRateLimit: true },
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
      placeholder="Текст рецензии (минимум 10 символов)"
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
        class="action-btn save-btn"
        :loading="saving"
        :disabled="!canSave"
        @click="saveEdit"
      >
        Сохранить
      </Button>
      <Button type="button" class="action-btn" @click="cancelEdit">
        Отмена
      </Button>
    </div>
    <ActionResult :result="saveResult" class="edit-refusal" />
  </div>

  <GameReviewCard
    v-else
    :review="review"
    :show-game="showGame"
    :search-query="searchQuery"
  >
    <template v-if="canEdit || canDelete" #actions>
      <BubbleAction
        v-if="canEdit"
        icon="pencil"
        label="Редактировать"
        @click="startEdit"
      />
      <BubbleAction
        v-if="canDelete"
        icon="trash"
        label="Удалить"
        @click="showDeleteConfirm = true"
      />
    </template>
  </GameReviewCard>

  <!-- Outside the branches above, so the answer still has a window to close
       whichever of them is on screen. -->
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
// The editing state of a website testimonial, so the two reviews of the site
// are rewritten by one form: the field over a row of "Сохранить" / "Отмена".
.review-edit
  display: flex
  flex-direction: column
  gap: $small

.edit-actions
  display: flex
  gap: $small

// Spaced by the gap of the column. The two buttons above it are the shared
// pair of every form on the site, the primary first.
.edit-refusal
  --action-result-margin: 0
</style>
