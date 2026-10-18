<script setup lang="ts">
import { computed, ref } from "vue";
import dayjs from "dayjs";
import { storeToRefs } from "pinia";
import type { PostReview } from "../model/types";
import type { ReviewSign } from "@/shared/api/models/game/reviews";
import type { RouteLocationRaw } from "vue-router";
import {
  RemovedAccountLink,
  UserLink,
  userIsAdmin,
  userIsSeniorModerator,
} from "@/entities/user/@x/game";
import gameApi from "../api/gameApi";
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
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import Button from "@/shared/ui/Button/Button.vue";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";

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
/** The segments of the sign toggle: "=" alone for a newbie. */
const signOptions = computed(() => reviewSignOptions(canPickSign.value));

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
  saveResult.clear();
}

// A refusal stands under the buttons of the edit, which stays open with the
// text in it.
const saveResult = useActionResult();

async function saveEdit() {
  if (!editText.value.trim() || saving.value) return;
  saving.value = true;
  saveResult.clear();
  // Clamp defensively even though the ± buttons are hidden for a newbie.
  const sign = canPickSign.value ? editSign.value : 0;
  const { data, error } = await gameApi.updatePostReview(
    props.review.postId,
    props.review.id,
    { sign, text: editText.value.trim() },
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

// ── Delete ────────────────────────────────────────────────────────────────
const showDeleteConfirm = ref(false);

function requestDelete() {
  showDeleteConfirm.value = true;
}

/**
 * The confirmation's action: it waits shut for the answer, prints a refusal
 * in itself and closes on success alone, so the card is struck through only
 * once the server has taken the review down.
 */
async function deleteReview() {
  const sign = reviewSignToNumber(effectiveSign.value);
  const result = await gameApi.deletePostReview(
    props.review.postId,
    props.review.id,
    { ownsRefusal: true, ownsRateLimit: true },
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
    <!-- Deleted placeholder. The surface that owns the list drops the row on
         the event; this is what a surface that does not shows instead. -->
    <template v-if="removed">
      <span class="review-removed">Оценка удалена</span>
    </template>

    <!-- Edit in place: the sign and the sentence, the pair the create form
         asks for, in the place the review already occupies. -->
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
      <!-- One caption line: "+1 от Имя, дата, #N | Редактировать | Удалить",
           the number in the line after the date, where it stood before the
           room wave of 24.09 moved it to the right corner (the owner, 05.10:
           "У оценок постов что ты сделал с #1? Почему он справа?"). The sign
           and the name run into the date, and the date into the number, with
           a comma and a space, so a narrow column can break after either
           comma and keeps each piece whole; a copy of the row reads
           "+1 от Имя, дата, #1". -->
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
      </CaptionLine>
    </template>

    <!-- The question "Удалить" asks before it deletes, the way the post and
         the comment do. Kept outside the branches above so the answer still
         has a dialog to close. -->
    <ConfirmDialog
      v-model:show="showDeleteConfirm"
      title="Удалить оценку?"
      message="Оценка будет удалена, а ее вклад в рейтинг поста и его автора снят"
      confirm-label="Удалить"
      failure="Не удалось удалить оценку"
      :action="deleteReview"
    />
  </li>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// Review item - bullet list style like old DM site. The reviews of a post stand
// a clear step apart (owner, 27.09): the next one takes $medium over the
// $minor the list keeps at its edges.
.review-item
  margin: $minor 0
  padding-left: $small
  line-height: 1.4

  & + &
    margin-top: $medium

  // The review the reader came for is marked the way the site marks any
  // navigated-to block: the highlight tint of the theme, square as every
  // content surface. The author line is bold here already, and a rule down
  // one edge belongs to quotes alone.
  &.highlighted
    background-color: $bg-highlight-yellow

// Review text - first line, primary content
.review-text
  color: $text
  font-weight: 500
  margin-bottom: $small

// The caption line draws its own size and colour; here it is a block of its
// own under the text.
.review-meta
  display: block

.review-removed
  font-size: $tertiary-font-size
  color: $text-muted
  font-style: italic

// No margin: the space before the number is the ", " of the date before it.
// Same rule as .post-number in GamePost: a "go to the post" affordance is a
// link and looks like one everywhere, home page included (UI_STANDARDS).
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

// Edit mode — the create form of a review, unchanged: the sign as segments,
// the text field across the column, the buttons under it at the left, so the
// two are not two different forms.
.review-edit
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  margin: $small 0

// A text area of the site (+input): its frame, ground, padding and type,
// across the column, growing downwards only.
.review-input
  box-sizing: border-box
  width: 100%
  resize: vertical

// Under the buttons, spaced by the gap of the column.
.review-refusal
  --action-result-margin: 0

.edit-actions
  display: flex
  gap: $small
</style>
