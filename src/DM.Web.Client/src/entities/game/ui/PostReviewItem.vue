<script setup lang="ts">
import { computed, ref } from "vue";
import dayjs from "dayjs";
import { storeToRefs } from "pinia";
import type { PostReview } from "../model/postTypes";
import type { ReviewSign } from "@/shared/api/models/game/reviews";
import type { RouteLocationRaw } from "vue-router";
import {
  RemovedAccountLink,
  UserLink,
  userIsAdmin,
  userIsSeniorModerator,
} from "@/entities/user/@x/game";
import postApi from "../api/postApi";
import {
  reviewSignToNumber,
  reviewSignName,
  reviewSignOptions,
} from "../model/reviewSign";
import { unwrapResource } from "@/shared/api";
import { useAuthStore } from "@/shared/stores/auth";
import { ContentText } from "@/shared/ui/Content";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import {
  CaptionAction,
  CaptionLine,
  CaptionPart,
} from "@/shared/ui/CaptionLine";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import Button from "@/shared/ui/Button/Button.vue";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";
import { EDIT_TIME_LIMIT_MINUTES } from "@/shared/lib/utils/chat";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = withDefaults(
  defineProps<{
    review: PostReview;
    highlight?: boolean;
    /** 1-based, drawn as the anchor. */
    number?: number;
    /** A review has no page of its own, so the widget that owns the post builds the address. */
    to?: RouteLocationRaw;
    /** Only the game room opts in; the read-only surfaces never show the controls. */
    editable?: boolean;
    /** The reader may warn the author for the text: decided by the list, which knows the ranks. */
    canWarn?: boolean;
  }>(),
  { highlight: false, editable: false, canWarn: false },
);

const emit = defineEmits<{
  /** The review was edited; carries the sign it now has. */
  updated: [payload: { id: string; sign: number }];
  /** The review was deleted; carries the sign it had. */
  deleted: [payload: { id: string; sign: number }];
  /** "Предупреждение" was pressed. */
  warn: [review: PostReview];
}>();

const { user: currentUser } = storeToRefs(useAuthStore());

const anchorHint = "Перейти к оценке поста";

function getSignClass(sign?: ReviewSign | string): string {
  const signStr = String(sign);
  if (signStr === "Positive") return "positive";
  if (signStr === "Negative") return "negative";
  return "neutral";
}

function getSignText(sign?: ReviewSign | string): string {
  const signStr = String(sign);
  if (signStr === "Positive") return "+1";
  if (signStr === "Negative") return "-1";
  return "+0";
}

// The edit's answer is rendered already, so the card updates without a refetch.
const textOverride = ref<string | null>(null);
const signOverride = ref<number | null>(null);
const removed = ref(false);

const effectiveText = computed(() => textOverride.value ?? props.review.text);
const effectiveSign = computed<ReviewSign | string>(() =>
  signOverride.value === null
    ? props.review.sign
    : reviewSignName(signOverride.value),
);

const formattedDate = computed(() => formatDateFull(props.review.createdUtc));

const signText = computed(() => getSignText(effectiveSign.value));

// By id, as the server compares: a username can change.
const isAuthor = computed(
  () => !!currentUser.value && currentUser.value.id === props.review.author?.id,
);

const withinEditWindow = computed(
  () =>
    dayjs().diff(dayjs(props.review.createdUtc), "minute", true) <=
    EDIT_TIME_LIMIT_MINUTES,
);

const canEdit = computed(
  () =>
    props.editable &&
    !removed.value &&
    isAuthor.value &&
    (withinEditWindow.value || userIsAdmin(currentUser.value)),
);

const canDelete = computed(
  () =>
    props.editable &&
    !removed.value &&
    (isAuthor.value || userIsSeniorModerator(currentUser.value)),
);

const canPickSign = computed(() => !currentUser.value?.isNewbie);
const signOptions = computed(() => reviewSignOptions(canPickSign.value));

const isEditing = ref(false);
const editText = ref("");
const editSign = ref(0);
const opening = ref(false);
const saving = ref(false);

// A refusal of "Редактировать" is printed under the caption, where the reader looks.
const openResult = useActionResult();

async function startEdit() {
  if (opening.value || isEditing.value) return;
  openResult.clear();
  opening.value = true;
  // The display text has the author's markup drawn; the author_edit answer is the text as typed.
  const { data, error } = await postApi.getPostReviewForEdit(
    props.review.postId,
    props.review.id,
  );
  opening.value = false;
  if (error) {
    openResult.refuse(error, "Не удалось загрузить оценку");
    return;
  }
  editSign.value = reviewSignToNumber(effectiveSign.value);
  editText.value = unwrapResource<PostReview>(data)?.text ?? "";
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
  saveResult.clear();
}

const saveResult = useActionResult();

async function saveEdit() {
  if (!editText.value.trim() || saving.value) return;
  saving.value = true;
  saveResult.clear();
  // Clamp defensively even though the ± buttons are hidden for a newbie.
  const sign = canPickSign.value ? editSign.value : 0;
  const { data, error } = await postApi.updatePostReview(
    props.review.postId,
    props.review.id,
    { sign, text: editText.value },
  );
  saving.value = false;
  if (error) {
    saveResult.refuse(error, "Не удалось сохранить оценку");
    return;
  }
  const updated = unwrapResource<PostReview>(data);
  textOverride.value = updated?.text ?? editText.value;
  signOverride.value = updated ? reviewSignToNumber(updated.sign) : sign;
  isEditing.value = false;
  editText.value = "";
  emit("updated", { id: props.review.id, sign: signOverride.value });
}

function handleEditKeydown(event: KeyboardEvent) {
  if (event.key === "Escape") cancelEdit();
}

const showDeleteConfirm = ref(false);

function requestDelete() {
  showDeleteConfirm.value = true;
}

// The confirmation prints a refusal in itself and closes on success alone.
async function deleteReview() {
  const sign = reviewSignToNumber(effectiveSign.value);
  const result = await postApi.deletePostReview(
    props.review.postId,
    props.review.id,
    OWNS_REFUSAL,
  );
  if (!result.error) {
    removed.value = true;
    emit("deleted", { id: props.review.id, sign });
  }
  return result;
}
</script>

<template>
  <li
    :id="`review-${review.id}`"
    class="review-item"
    :class="{ highlighted: highlight }"
  >
    <!-- For a surface that does not drop the row on the event. -->
    <template v-if="removed">
      <span class="review-removed">Оценка удалена</span>
    </template>

    <template v-else-if="isEditing">
      <div class="review-edit">
        <SegmentedControl
          :model-value="String(editSign)"
          :options="signOptions"
          ariaLabel="Знак оценки"
          @update:model-value="editSign = reviewSignToNumber($event)"
        />
        <textarea
          v-model="editText"
          class="review-input"
          aria-label="Текст оценки"
          rows="2"
          @keydown="handleEditKeydown"
        ></textarea>
        <div class="edit-actions">
          <Button
            type="button"
            variant="primary"
            class="submit-btn"
            :disabled="!editText.trim() || saving"
            @click="saveEdit"
          >
            Сохранить
          </Button>
          <Button type="button" class="cancel-btn" @click="cancelEdit">
            Отмена
          </Button>
        </div>
        <ActionResult :result="saveResult" class="review-refusal" />
      </div>
    </template>

    <template v-else>
      <ContentText
        v-if="effectiveText"
        :html="effectiveText"
        class="review-text"
      />
      <!-- Commas between the pieces, so a narrow column breaks after one and keeps each whole. -->
      <CaptionLine class="review-meta">
        <CaptionPart :bar="false"
          ><span class="review-sign" :class="getSignClass(effectiveSign)">{{
            signText
          }}</span
          >{{ " от "
          }}<UserLink
            v-if="review.author"
            :user="review.author"
            :hide-badge="true"
            :class="{ 'author-highlight': highlight }"
          /><RemovedAccountLink v-else />,</CaptionPart
        >
        <CaptionPart :bar="!(number && to)"
          >{{ formattedDate
          }}<template v-if="number && to">,</template></CaptionPart
        >
        <span v-if="number && to" class="review-anchor-part"
          ><Tooltip :text="anchorHint"
            ><router-link
              class="review-anchor"
              :to="to"
              :aria-label="anchorHint"
              >{{ `#${number}` }}</router-link
            ></Tooltip
          ></span
        >
        <CaptionAction v-if="canEdit" @click="startEdit"
          >Редактировать</CaptionAction
        >
        <CaptionAction v-if="canDelete" @click="requestDelete"
          >Удалить</CaptionAction
        >
        <CaptionAction
          v-if="canWarn && !removed && effectiveText"
          @click="emit('warn', review)"
          >Предупреждение</CaptionAction
        >
      </CaptionLine>
      <ActionResult :result="openResult" class="review-refusal" />
    </template>

    <!-- Outside the branches, so the answer still has a dialog to close. -->
    <ConfirmDialog
      v-model:show="showDeleteConfirm"
      title="Удалить оценку?"
      message="Оценка будет удалена, а ее вклад в сумму оценок поста и в рейтинг его автора снят"
      confirm-label="Удалить"
      failure="Не удалось удалить оценку"
      :action="deleteReview"
    />
  </li>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ReviewParts" as *

.review-item
  +review-item

  // A rule down one edge belongs to quotes alone.
  &.highlighted
    background-color: $bg-highlight-yellow

.review-text
  +review-text

.review-meta
  display: block

.review-removed
  font-size: $tertiary-font-size
  color: $text-muted
  font-style: italic

// No margin: the space before the number is the ", " of the date.
.review-anchor
  font-size: $tertiary-font-size
  color: $link
  &:hover
    color: $link-hover

.review-sign
  &.positive
    color: $accent-green

  &.negative
    color: $accent-red

  &.neutral
    color: $text-muted

:deep(.author-highlight .user-link)
  font-weight: bold

// The create form of a review, unchanged, so the two are not two different forms.
.review-edit
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  margin: $small 0

.review-input
  box-sizing: border-box
  width: 100%
  resize: vertical

.review-refusal
  --action-result-margin: 0

.edit-actions
  display: flex
  gap: $small
</style>
