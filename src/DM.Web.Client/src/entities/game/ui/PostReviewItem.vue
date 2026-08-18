<script setup lang="ts">
import { computed, ref } from "vue";
import dayjs from "dayjs";
import { storeToRefs } from "pinia";
import type { PostReview } from "../model/types";
import type { ReviewSign } from "@/shared/api/models/game/reviews";
import type { RouteLocationRaw } from "vue-router";
import {
  UserLink,
  userIsAdmin,
  userIsSeniorModerator,
} from "@/entities/user/@x/game";
import gameApi from "../api/gameApi";
import { reviewSignToNumber, reviewSignName } from "../model/reviewSign";
import { unwrapResource } from "@/shared/api";
import { useAuthStore } from "@/shared/stores/auth";
import { ContentText } from "@/shared/ui/Content";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { Tooltip } from "@/shared/ui/Tooltip";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { notifyFailure } from "@/shared/lib/errors";
import { formatDateFull } from "@/shared/lib/utils/datetime";

const props = withDefaults(
  defineProps<{
    review: PostReview;
    /** Highlights this review's author (bold + accent-green left border) */
    highlight?: boolean;
    /** 1-based position — renders the anchor number (doc 4.2.2.14). */
    number?: number;
    /**
     * Where the number leads: the room page the reviewed post lives on,
     * anchored at the post and naming this review, which is what opens the
     * reviews block on arrival. A review has no page of its own, so the widget
     * that owns the post owns the address (GamePost.vue): built here it would
     * be the address of whatever surface the card is embedded in, which is how
     * a link to a review on the home page used to point at the home page.
     */
    to?: RouteLocationRaw;
    /**
     * Enable the author/moderator controls (edit + delete). Off by default, so
     * the read-only surfaces that draw a post with its reviews (home, pulse,
     * rated lists) never show them; the game room opts in, the same way the
     * post's own controls are opted into.
     */
    editable?: boolean;
  }>(),
  { highlight: false, editable: false },
);

const emit = defineEmits<{
  /** The review was edited; carries the sign it now has. */
  updated: [payload: { id: string; sign: number }];
  /** The review was deleted; carries the sign it had. */
  deleted: [payload: { id: string; sign: number }];
}>();

/**
 * The author's correction window, mirroring PostReviewService.EditWindow — the
 * same quarter of an hour a post and a comment give their author. The server
 * is authoritative and refuses a late PATCH; this is what keeps the button
 * from offering an edit that would come back forbidden.
 */
const EDIT_TIME_LIMIT_MINUTES = 15;

const { user: currentUser } = storeToRefs(useAuthStore());

// One sentence for both the name of the control and the tooltip that
// describes it. The number goes where it points, the way "Перейти к посту"
// does in the footer of the post itself: a copy button was the odd one out
// among the addresses of this page, and it made the reader paste a link to
// find out where it led. The address stays in the href, so the browser's own
// "copy link" is still there for whoever wants it.
const anchorHint = "Перейти к оценке поста";

// API returns string values: "Positive", "Negative", "Neutral"
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

// What a successful edit returns is already rendered by the server; keeping it
// here updates the card in place without the parent having to refetch the
// list — the same override idiom GamePost uses for its own text and rating.
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

// ──────────────────────────────────────────────────────────────────────────
// Who may do what. Both computeds repeat a server check rather than guess at
// one: PostReviewIntentionResolver allows Edit to the author alone and Delete
// to the author or a senior moderator, and PostReviewService closes the
// author's edit window after 15 minutes (admins exempt).
// ──────────────────────────────────────────────────────────────────────────
// The server compares identifiers, so this does too: a username can change,
// and the two sides would then disagree about the same person.
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

// Newbies may only hold the neutral sign: the create form hides "+" and "−"
// for them, and an edit that would set one is refused by the same probation
// rule on the server.
const canPickSign = computed(() => !currentUser.value?.isNewbie);

// ── Inline edit ───────────────────────────────────────────────────────────
const isEditing = ref(false);
const editText = ref("");
const editSign = ref(0);
const opening = ref(false);
const saving = ref(false);

async function startEdit() {
  if (opening.value || isEditing.value) return;
  editSign.value = reviewSignToNumber(effectiveSign.value);
  // The card shows the display rendering; the editor needs what the author
  // wrote. Seeded from the display text first, so a failed request opens the
  // editor anyway instead of refusing to open at all.
  editText.value = htmlToBbcode(effectiveText.value ?? "");
  opening.value = true;
  const { data, error } = await gameApi.getPostReviewForEdit(
    props.review.postId,
    props.review.id,
  );
  opening.value = false;
  if (!error) {
    const source = unwrapResource<PostReview>(data)?.text;
    if (source) editText.value = htmlToBbcode(source);
  }
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
}

async function saveEdit() {
  if (!editText.value.trim() || saving.value) return;
  saving.value = true;
  // Clamp defensively even though the ± buttons are hidden for a newbie.
  const sign = canPickSign.value ? editSign.value : 0;
  const { data, error } = await gameApi.updatePostReview(
    props.review.postId,
    props.review.id,
    { sign, text: editText.value.trim() },
  );
  saving.value = false;
  if (error) {
    notifyFailure(error, "Не удалось сохранить оценку");
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

// ── Delete ────────────────────────────────────────────────────────────────
const showDeleteConfirm = ref(false);
const deleting = ref(false);

function requestDelete() {
  showDeleteConfirm.value = true;
}

async function confirmDelete() {
  if (deleting.value) return;
  deleting.value = true;
  const sign = reviewSignToNumber(effectiveSign.value);
  const { error } = await gameApi.deletePostReview(
    props.review.postId,
    props.review.id,
  );
  deleting.value = false;
  showDeleteConfirm.value = false;
  if (error) {
    notifyFailure(error, "Не удалось удалить оценку");
    return;
  }
  removed.value = true;
  emit("deleted", { id: props.review.id, sign });
}
</script>

<template>
  <li
    :id="`review-${review.id}`"
    class="review-item"
    :class="{ highlighted: highlight }"
  >
    <!-- Deleted placeholder. The surface that owns the list drops the row on
         the event; this is what a surface that does not shows instead. -->
    <template v-if="removed">
      <span class="review-removed">Оценка удалена</span>
    </template>

    <!-- Edit in place: the sign and the sentence, the pair the create form
         asks for, in the place the review already occupies. -->
    <template v-else-if="isEditing">
      <div class="review-edit">
        <div class="sign-selector">
          <button
            v-if="canPickSign"
            type="button"
            class="sign-btn"
            :class="{ active: editSign === 1, positive: editSign === 1 }"
            aria-label="Положительная оценка"
            @click="editSign = 1"
          >
            +
          </button>
          <button
            type="button"
            class="sign-btn"
            :class="{ active: editSign === 0, neutral: editSign === 0 }"
            aria-label="Нейтральная оценка"
            @click="editSign = 0"
          >
            =
          </button>
          <button
            v-if="canPickSign"
            type="button"
            class="sign-btn"
            :class="{ active: editSign === -1, negative: editSign === -1 }"
            aria-label="Отрицательная оценка"
            @click="editSign = -1"
          >
            −
          </button>
        </div>
        <textarea
          v-model="editText"
          class="review-input"
          aria-label="Текст оценки"
          rows="2"
          @keydown="handleEditKeydown"
        ></textarea>
        <div class="edit-actions">
          <button
            type="button"
            class="submit-btn"
            :disabled="!editText.trim() || saving"
            @click="saveEdit"
          >
            Сохранить
          </button>
          <button type="button" class="action-btn" @click="cancelEdit">
            Отмена
          </button>
        </div>
      </div>
    </template>

    <template v-else>
      <ContentText
        v-if="effectiveText"
        :html="effectiveText"
        class="review-text"
      />
      <div class="review-footer">
        <div class="review-meta">
          <span class="review-sign" :class="getSignClass(effectiveSign)">{{
            signText
          }}</span
          >{{ " от "
          }}<UserLink
            :user="review.author!"
            :hide-badge="true"
            :class="{ 'author-highlight': highlight }"
          />{{ `, ${formattedDate}`
          }}<template v-if="number && to"
            >{{ ", "
            }}<Tooltip :text="anchorHint"
              ><router-link
                class="review-anchor"
                :to="to"
                :aria-label="anchorHint"
                >{{ `#${number}` }}</router-link
              ></Tooltip
            ></template
          >
        </div>

        <span v-if="canEdit || canDelete" class="review-actions">
          <button
            v-if="canEdit"
            type="button"
            class="action-btn"
            @click="startEdit"
          >
            Редактировать
          </button>
          <button
            v-if="canDelete"
            type="button"
            class="action-btn delete-btn"
            @click="requestDelete"
          >
            Удалить
          </button>
        </span>
      </div>
    </template>

    <!-- The question "Удалить" asks before it deletes, the way the post and
         the comment do. Kept outside the branches above so the answer still
         has a dialog to close. -->
    <ConfirmDialog
      :show="showDeleteConfirm"
      title="Удалить оценку?"
      message="Оценка будет удалена, а ее вклад в рейтинг поста и его автора снят."
      confirm-label="Удалить"
      danger
      :loading="deleting"
      @confirm="confirmDelete"
      @update:show="showDeleteConfirm = $event"
    />
  </li>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

// Review item - bullet list style like old DM site
.review-item
  margin: $minor 0
  padding-left: $small
  line-height: 1.4
  border-left: 2px solid transparent

  &.highlighted
    border-left-color: $accent-green

// Review text - first line, primary content
.review-text
  color: $text
  font-weight: 500
  margin-bottom: $small

// The meta line and the controls share one baseline row. The buttons are a
// sibling of the meta and not part of it, so the line still copies as the one
// human sentence it reads as.
.review-footer
  display: flex
  align-items: baseline
  flex-wrap: wrap
  gap: $small

// Meta line: "+1 от Username, дата, #N"
.review-meta
  font-size: $tertiary-font-size
  color: $text-muted

.review-removed
  font-size: $tertiary-font-size
  color: $text-muted
  font-style: italic

// No margin: the space before the number is the ", " text node above.
.review-anchor
  font-size: $tertiary-font-size
  color: $text-muted
  &:hover
    color: $link

.review-sign
  &.positive
    color: $accent-green

  &.negative
    color: $accent-red

  &.neutral
    color: $text-muted

:deep(.author-highlight .user-link)
  font-weight: bold

.review-actions
  display: inline-flex
  align-items: baseline
  gap: $small

.action-btn
  padding: 0 $tiny
  font-size: $secondary-font-size
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted
  font-family: inherit

  &:hover
    color: $link

  &.delete-btn:hover
    color: $accent-red

// Edit mode — the create form of a review, unchanged: same sign column, same
// text field, same submit button, so the two are not two different forms.
.review-edit
  display: flex
  gap: $small
  align-items: flex-start
  flex-wrap: wrap
  margin: $small 0

.sign-selector
  display: flex
  flex-direction: column
  gap: 2px

.sign-btn
  width: 28px
  height: 24px
  border: 1px solid $border
  background: $bg-element
  color: $text-muted
  cursor: pointer
  font-size: $font-size
  font-weight: bold
  padding: 0
  font-family: inherit
  &:hover
    border-color: $link
  &.active.positive
    color: $accent-green
    border-color: $accent-green
  &.active.neutral
    color: $text-muted
    border-color: $text-muted
  &.active.negative
    color: $accent-red
    border-color: $accent-red

.review-input
  flex: 1
  min-width: 200px
  min-height: 40px
  padding: $minor
  border: 1px solid $border
  font-size: $font-size
  font-family: inherit
  resize: vertical
  color: $text
  background: $bg-page

.edit-actions
  display: inline-flex
  align-items: flex-start
  gap: $small

.submit-btn
  +button
</style>
