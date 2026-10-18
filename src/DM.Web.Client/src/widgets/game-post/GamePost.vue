<script setup lang="ts">
import {
  ref,
  computed,
  watch,
  onMounted,
  onUnmounted,
  onBeforeUnmount,
  nextTick,
} from "vue";
import dayjs from "dayjs";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { useRoute, type RouteLocationRaw } from "vue-router";
import { storeToRefs } from "pinia";
import type { Post, PostAttachment, PostReview } from "@/entities/game";
import {
  gameApi,
  GameLink,
  PostReviewItem,
  RoomLink,
  reviewSignToNumber,
  reviewSignName,
  reviewSignOptions,
} from "@/entities/game";
import { ContentText } from "@/shared/ui/Content";
import { SecondaryText } from "@/shared/ui/Layout";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { RemovedAccountLink, UserLink, userIsModerator } from "@/entities/user";
import {
  MAX_POST_ATTACHMENTS,
  POST_ATTACHMENT_ACCEPT,
  describeAttachmentProblem,
  reserveAttachmentImageBoxes,
  uploadPostAttachments,
} from "@/features/upload";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import { SvgIcon } from "@/shared/ui/Icon";
import PostAttachmentRow from "./PostAttachmentRow.vue";
import { DICE_RESULTS_HEADING, formatDiceRoll } from "./diceLine";
import { unwrapResource, uploadApi } from "@/shared/api";
import { apiUrl } from "@/shared/api/client";
import { formatFileSize } from "@/shared/lib/utils/fileSize";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { trimHtmlWhitespace } from "@/shared/lib/utils/bbcodeInteractive";
import { useAuthStore } from "@/shared/stores/auth";
import { useUiStore } from "@/shared/stores/ui";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";
import { symbols } from "@/shared/lib/utils/icons";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { scrollBlockIntoView } from "@/shared/lib/scroll";
import { refusalHere } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";

const props = withDefaults(
  defineProps<{
    post: Post;
    number?: number;
    /** Featured post mode — shows navigation breadcrumb, anchor icon instead of number */
    showNavigation?: boolean;
    /**
     * How far the breadcrumb reaches. "game" names the game and the room, for
     * the cross-game surfaces (pulse, profile, moderation, home). "room" names
     * the room alone, for a page that already is the game: repeating the game
     * title above every post of its own sub-page says nothing.
     */
    navigationLevel?: "game" | "room";
    /** Enable content truncation */
    truncatable?: boolean;
    /** Fallback max height before truncation (px) */
    maxHeight?: number;
    /** Search query for highlighting matches in post text */
    searchQuery?: string;
    /** Username to highlight among reviews (bold author + green left border) */
    highlightUsername?: string;
    /**
     * Enable author/moderator lifecycle controls (edit + delete) on the post.
     * Off by default so read-only surfaces (home "best post", pulse, rated
     * lists) never expose them; the game room opts in.
     */
    editable?: boolean;
  }>(),
  {
    showNavigation: false,
    navigationLevel: "game",
    truncatable: false,
    maxHeight: 150,
    editable: false,
  },
);

const emit = defineEmits<{
  /** A successful edit landed (parent may refetch if it wants). */
  edited: [id: string];
  /** The post was soft-deleted (parent may refetch / drop it). */
  deleted: [id: string];
}>();

const authStore = useAuthStore();
const { user: currentUser } = storeToRefs(useAuthStore());

// "Перенос слов" of the settings panel, over both blocks of text the author
// wrote; the rolls between them are figures and do not take it. Both blocks are
// flush on both sides at every width (+body-text).
const { hyphenateWords } = storeToRefs(useUiStore());
const typesetting = computed(() => ({
  hyphenated: hyphenateWords.value,
}));

// 15-minute author edit window — a client-side affordance matching the
// Comment block; moderators+ may edit/delete regardless (PostIntention).
const EDIT_TIME_LIMIT_MINUTES = 15;

// Reviews state
const showReviews = ref(false);
const reviews = ref<PostReview[]>([]);
const reviewsLoaded = ref(false);
const reviewsLoading = ref(false);
const reviewsError = ref<string | null>(null);

// Review form state
const newReviewSign = ref<number>(1);
const newReviewText = ref("");
const submittingReview = ref(false);
// The refusal of the last send, printed under the text it refused. A refused
// review leaves the form exactly as it was, so the form is where the answer
// has to stand: a toast in the corner is away from the field the reader is
// looking at and has to be dismissed by hand (error toasts never fade), and
// the draft it is about is down here.
const reviewRefusal = ref<string | null>(null);

// Data access
const character = computed(() => props.post?.character);
const author = computed(() => props.post?.author);
const hasCharacter = computed(() => !!character.value);
const hasAuthor = computed(() => !!author.value);
const authorGameRole = computed(() => props.post?.authorGameRole);
const characterName = computed(() => character.value?.name);
const canLinkCharacter = computed(
  () => !!character.value?.id && !!props.post?.room?.game?.id,
);
// After a successful inline edit the server returns the freshly rendered
// HTML; keep it as a local override so the post updates in place without the
// parent having to refetch (mirrors the reviewCount/rating override idiom).
const gameTextOverride = ref<string | null>(null);
const metaTextOverride = ref<string | null>(null);
// When the edit this page made was saved, for the edit line under the text.
const editedAtOverride = ref<string | null>(null);

const effectiveGameText = computed(
  () => gameTextOverride.value ?? props.post?.gameText,
);
const effectiveMetaText = computed(() =>
  metaTextOverride.value !== null
    ? metaTextOverride.value
    : props.post?.metagameText,
);

const hasMetagameText = computed(() => !!effectiveMetaText.value);
// Pre-trim leading/trailing empty lines so they never inflate scrollHeight
// or eat the collapsed budget, then declare the box of every picture that is
// one of this post's own attachments, so its decode does not push the text
// below it down. Pure transforms, no DOM mutation. `attachments` is read
// lazily, inside the computed, and is declared with the rest of the attachment
// state further down.
const postTextHtml = computed(() =>
  reserveAttachmentImageBoxes(
    trimHtmlWhitespace(effectiveGameText.value),
    attachments.value,
  ),
);
const postMetagameHtml = computed(() =>
  reserveAttachmentImageBoxes(
    trimHtmlWhitespace(effectiveMetaText.value),
    attachments.value,
  ),
);

const formattedDate = computed(() => formatDateFull(props.post?.createdUtc));

// Local overrides for reviewCount/rating — keep the visible values consistent
// with a just-submitted review until the parent list is refetched, without
// mutating the `post` prop (which is otherwise read-only data owned by the
// store/API response).
const reviewCountOverride = ref<number | null>(null);
const ratingOverride = ref<number | null>(null);

const reviewCount = computed(
  () => reviewCountOverride.value ?? props.post?.reviewCount ?? 0,
);

const postId = computed(() => props.post?.id);
const postAnchor = computed(() => `#post-${postId.value}`);
const reviewsCollapseId = computed(() => `post-reviews-${postId.value}`);

// ──────────────────────────────────────────────────────────────────────────────
// Dice rolls: "Результаты бросков:" and one numbered line each on a grey strip
// between the game text and the metagame text, as the old site has written
// them since 4 October 2026. All of them, however many: the old site never
// folds the rolls of a post.
// ──────────────────────────────────────────────────────────────────────────────
const diceLines = computed(() =>
  (props.post?.diceRolls ?? []).map((roll, index) =>
    formatDiceRoll(roll, index + 1),
  ),
);

// The sum of the post's PostReview signs — "Оценки: +N", bold colored link.
// "Рейтинг" is the user's own number (review sum over post count, UserRating),
// so the post line is named after what it counts: the reviews of this post.
const postRating = computed(
  () => ratingOverride.value ?? props.post?.rating ?? null,
);
const ratingText = computed(() => {
  const r = postRating.value;
  if (r === null || r === undefined) return "+0";
  if (r > 0) return `+${r}`;
  if (r === 0) return "+0";
  return `${r}`;
});
const ratingColorClass = computed(() => {
  const r = postRating.value ?? 0;
  if (r > 0) return "positive";
  if (r < 0) return "negative";
  return "neutral";
});

const hasReviews = computed(() => reviewCount.value > 0);

// ──────────────────────────────────────────────────────────────────────────────
// Review-form gating (doc 4.2.2.14). The server answers whether this viewer may
// rate the post (post.viewer.canReview, PostReviewService.ReviewableAsync): not
// their own, not rated by them, and no rating of theirs in the game during the
// last three days. The form is offered by that answer and nothing else, so a
// rating the server would refuse is never typed. Newbies may pick only "="
// (need ≥100 posts for ±); that narrows the sign, not the form.
//
// What the answer cannot know is what happened on this page since it was read.
// A rating sent here closes its post (the list shows it, alreadyVoted) and its
// game (gamesRatedHere, shared by every post on the page). A viewer who signs in
// or out makes it somebody else's answer: the post is read again for them, and
// until it answers nothing is offered. A rating that still loses a race, say to
// one sent from another tab, gets the server's own sentence under the button.
// ──────────────────────────────────────────────────────────────────────────────
const alreadyVoted = computed(
  () =>
    !!currentUser.value &&
    reviews.value.some(
      (r) => r.author?.username === currentUser.value?.username,
    ),
);
const isNewbieReviewer = computed(() => !!currentUser.value?.isNewbie);
// Newbies are limited to the neutral "=" sign until they reach 100 posts.
const canPickSignedReview = computed(() => !isNewbieReviewer.value);
/** The segments of the sign toggle: "=" alone for a newbie. */
const reviewSigns = computed(() =>
  reviewSignOptions(canPickSignedReview.value),
);
/**
 * The server's answer for the viewer of the moment: the one the post came
 * with, or the one read again for a viewer who signed in since.
 */
const viewerAnswer = ref<Pick<NonNullable<Post["viewer"]>, "canReview"> | null>(
  null,
);
watch(
  () => props.post,
  () => {
    viewerAnswer.value = null;
  },
);
const postViewer = computed(() => viewerAnswer.value ?? props.post?.viewer);
/** A rating sent from this page already closed the post's game. */
const gameRatedHere = computed(
  () => !!props.post?.gameId && gamesRatedHere.has(props.post.gameId),
);
// Somebody the server would let rate this post, once the list says they have
// not yet. What opens the reviews on a post nobody has rated: the sum "+0" is
// the way to the form of its first review.
const canReview = computed(
  () =>
    authStore.isAuthenticated &&
    !isDeleted.value &&
    !!postViewer.value?.canReview &&
    !gameRatedHere.value,
);

/**
 * Asks the server again what the viewer may do with this post. A failed read
 * leaves the form off: nothing is offered on an answer nobody gave.
 */
async function askViewerAgain(username: string): Promise<boolean> {
  const id = postId.value;
  if (!id) return false;
  const { data } = await gameApi.getPost(id, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  // A post replaced or a viewer changed again meanwhile owns the answer.
  if (postId.value !== id || currentUser.value?.username !== username) {
    return false;
  }
  const fresh = unwrapResource<Post>(data)?.viewer;
  if (!fresh) return false;
  viewerAnswer.value = fresh;
  return fresh.canReview;
}

// The answer is for whoever held the session when the post was read. Signing
// in or out on this page makes it somebody else's: until the post answers for
// the new viewer, no form; a guest is asked nothing and offered nothing.
watch(
  () => currentUser.value?.username ?? null,
  (username, before) => {
    if (username === before) return;
    gamesRatedHere.clear();
    viewerAnswer.value = { canReview: false };
    if (username && !isPostAuthor.value) void askViewerAgain(username);
  },
);
const showReviewForm = computed(() => canReview.value && !alreadyVoted.value);
const canOpenReviews = computed(() => hasReviews.value || canReview.value);

// Keep the selected sign valid for newbies (they may only pick "=").
watch(
  canPickSignedReview,
  (canSign) => {
    if (!canSign) newReviewSign.value = 0;
  },
  { immediate: true },
);

// Register reviews collapse with the expandable registry so
// "Свернуть/Развернуть все" in ScrollNav controls post reviews.
const reviewHandleId = Symbol("post-reviews");
let unregisterReviewHandle: (() => void) | null = null;

function ensureReviewRegistration() {
  if (hasReviews.value && !unregisterReviewHandle) {
    unregisterReviewHandle = registerExpandable({
      id: reviewHandleId,
      isExpanded: () => showReviews.value,
      // expand/collapse callbacks are PURE state changes — no
      // notifyExpandableChanged() here, that's only for manual user clicks
      // in toggleReviews(). Calling it from bulk expandAll/collapseAll
      // would clear pendingAction mid-loop and break other handles.
      expand: () => {
        if (!showReviews.value) {
          showReviews.value = true;
          loadReviews();
        }
      },
      collapse: () => {
        if (showReviews.value) {
          showReviews.value = false;
        }
      },
    });
  }
}

// Register synchronously in setup so ScrollNav's "Развернуть все" button
// appears in the SAME render frame as the post itself — no visible lag
// between posts becoming visible and the toggle button showing up.
ensureReviewRegistration();
// watch handles posts that gain reviews after mount (e.g. user adds a review
// on a single-game page where reviewCount started at 0).
watch(hasReviews, ensureReviewRegistration);
onBeforeUnmount(() => unregisterReviewHandle?.());

// Navigation (featured post mode). `post.room.game` is already a
// full sidebar-tier GameRef (master, assistants, activeCharacters,
// recruitment, subscribersCount) hydrated by the backend's batched
// EnrichGamesAsync step in PostRepository.GetRated — no per-row
// fetch, no tooltip cache, no reactivity dance. Same data-flow as
// ActiveGames / RecruitingGames in the sidebar: receive the full
// ref, pass it straight into GameLink. See PATTERNS.md → "DTO
// Projection Pattern" — GameRef is the sidebar tier with all
// tooltip-feeding fields populated.
const hasNavigation = computed(
  () => props.showNavigation && !!props.post?.room?.game?.publicId,
);

// Raw identifiers used by the "перейти к посту" router-link in the
// post footer. Resolved from the nested GameRef; the hasNavigation
// gate above ensures both are defined whenever the link renders.
const gameId = computed(() => props.post?.room?.game?.publicId);
const roomNumber = computed(() => props.post?.room?.roomNumber);

const route = useRoute();

// The post's own address, built once here and handed to everything that needs
// it: the room page the post lives on, anchored at the post. A surface that is
// not that room (the pulse, the home page, a profile) has a pathname of its
// own, so an address taken from window.location there points at the page the
// reader happened to be on. On the room page itself `post.room` is redundant
// and not sent, and there the current pathname IS the room.
const postRoute = computed(() =>
  gameId.value && roomNumber.value
    ? {
        name: "game-room" as const,
        params: { id: gameId.value, num: roomNumber.value },
        hash: postAnchor.value,
      }
    : null,
);

/**
 * The address of one review: the post's address plus `?review={id}`. A review
 * has no page of its own, and the block it lives in is collapsed and unfetched
 * until a reader opens it, so the post anchor alone landed the reader on the
 * post with the review still out of sight. The parameter is read back below:
 * it opens the block and marks the review it names.
 */
function reviewRoute(reviewId: string): RouteLocationRaw {
  return postRoute.value
    ? { ...postRoute.value, query: { review: reviewId } }
    : {
        // The room page itself: `post.room` is not sent there, and the current
        // route IS the room. Its own query (the page number) has to survive,
        // or the link would point at the first page of the room.
        path: route.path,
        query: { ...route.query, review: reviewId },
        hash: postAnchor.value,
      };
}

/**
 * The review the current address points at, or null. The hash is what makes it
 * this post's business: a room draws many posts, and every one of them reads
 * the same query.
 */
const anchoredReviewId = computed(() => {
  if (route.hash !== postAnchor.value) return null;
  const asked = route.query.review;
  return (Array.isArray(asked) ? asked[0] : asked) || null;
});

/** A review is marked either because it is the one linked to, or by author. */
function isReviewHighlighted(review: PostReview): boolean {
  return (
    review.id === anchoredReviewId.value ||
    (!!props.highlightUsername &&
      review.author?.username === props.highlightUsername)
  );
}

// ──────────────────────────────────────────────────────────────────────────────
// Truncation — dynamic height matching post-meta, line-aligned.
// Opt-in via `truncatable` only. Contexts that just need the breadcrumb
// (e.g. Pulse) pass `show-navigation` without `truncatable` and get full posts.
// ──────────────────────────────────────────────────────────────────────────────
const shouldTruncate = computed(() => props.truncatable);

const metaRef = ref<HTMLElement | null>(null);
const dynamicMaxHeight = ref(props.maxHeight);

// The budget is counted in lines: as many as stand beside the left column, at
// every width, the column standing beside the text on a phone as on 1366. The
// prop stays the budget until the column is laid out.
function recalcMaxHeight() {
  if (!metaRef.value) return;
  const metaH = metaRef.value.offsetHeight;
  if (metaH <= 0) return;

  // Reserve space for "... показать полностью" link (~22px)
  const expandLinkH = 22;
  const available = metaH - expandLinkH;

  // Rough BUDGET estimate only (approximate line step for card sizing).
  // The authoritative whole-line snapping happens inside
  // <TruncatedContent>, which measures the real content line-height at
  // runtime — this estimate never has to match it to keep lines uncut.
  const lineH = 20;
  const lines = Math.max(1, Math.floor(available / lineH));
  dynamicMaxHeight.value = lines * lineH;
}

let resizeObserver: ResizeObserver | null = null;

onMounted(() => {
  nextTick(recalcMaxHeight);
  if (metaRef.value) {
    resizeObserver = new ResizeObserver(recalcMaxHeight);
    resizeObserver.observe(metaRef.value);
  }
});

onUnmounted(() => {
  resizeObserver?.disconnect();
});

// Truncation is delegated to <TruncatedContent> in the template.

// ──────────────────────────────────────────────────────────────────────────────
// Author / moderator lifecycle: edit + soft-delete (doc 4.2.2.12)
// ──────────────────────────────────────────────────────────────────────────────
const isModerator = computed(() => userIsModerator(currentUser.value));
const isPostAuthor = computed(
  () =>
    !!currentUser.value &&
    currentUser.value.username === author.value?.username,
);
const withinEditWindow = computed(
  () =>
    dayjs().diff(dayjs(props.post?.createdUtc), "minute", true) <=
    EDIT_TIME_LIMIT_MINUTES,
);
// Edit: author within 15 min, or moderator+ any time. Delete: author or
// moderator+ (backend PostIntention.Delete has no author time window).
const canEditPost = computed(
  () =>
    props.editable &&
    !isDeleted.value &&
    (isModerator.value || (isPostAuthor.value && withinEditWindow.value)),
);
const canDeletePost = computed(
  () =>
    props.editable &&
    !isDeleted.value &&
    (isModerator.value || isPostAuthor.value),
);
// The latest edit leads the history the server sends.
const editedAt = computed(
  () => editedAtOverride.value ?? props.post?.edits?.[0]?.modifiedUtc ?? null,
);
const isEdited = computed(
  () => !!editedAt.value || (props.post?.edits?.length ?? 0) > 0,
);
const formattedEditedAt = computed(() =>
  editedAt.value ? formatDateFull(editedAt.value) : "",
);

const isEditingPost = ref(false);
const editGameText = ref("");
const editMetaText = ref("");
const savingPost = ref(false);

// The line under the text: when the post was last edited, and what the viewer
// may do with it. A post with neither has no line at all.
const showEditLine = computed(
  () =>
    !isEditingPost.value &&
    (isEdited.value || canEditPost.value || canDeletePost.value),
);

async function startEditPost() {
  // Seeded from the display rendering, the editor received a post whose
  // [private] blocks had already been flattened into ordinary markup: saving
  // then published the private text to the whole room. The author's own view
  // of their post is a different rendering, and it has to be asked for.
  editGameText.value = effectiveGameText.value ?? "";
  editMetaText.value = effectiveMetaText.value ?? "";
  isEditingPost.value = true;

  try {
    const { data } = await gameApi.getPostForEdit(props.post.id);
    // The answer is an envelope: the field sits under `resource`, and reading
    // it off the top level gave undefined every time. The editor then kept the
    // value seeded two lines above - the Display rendering - and handed
    // server-built HTML to an editor that takes BBCode. Saving that published
    // the [private] block as ordinary text to the whole room, which is the one
    // thing this fetch exists to prevent.
    const source = unwrapResource<Post>(data);
    if (source && isEditingPost.value) {
      // htmlToBbcode rather than a plain assignment: the AuthorEdit audience
      // returns HTML carrying data-bb-* attributes (BbConverter calls RenderHtml
      // for every audience except plain text), while the editor takes and returns
      // BBCode. Without the reverse conversion a [private] block is saved as
      // markup, the server escapes it on the way out, and the private line is
      // published to the whole room — the very harm this method asks for the
      // author's audience to avoid.
      editGameText.value = source.gameText
        ? htmlToBbcode(source.gameText)
        : editGameText.value;
      editMetaText.value = source.metagameText
        ? htmlToBbcode(source.metagameText)
        : editMetaText.value;
    }
  } catch {
    // The editor is already open on the display text. Refusing to open it at
    // all would be worse than editing a post without private blocks, and the
    // request failing is what the general interceptor reports.
  }
}

function cancelEditPost() {
  isEditingPost.value = false;
  editResult.clear();
}

/** A refused save, under the buttons of the edit; the text stays in it. */
const editResult = useActionResult();

async function saveEditPost() {
  if (!postId.value || !editGameText.value.trim() || savingPost.value) return;
  savingPost.value = true;
  editResult.clear();
  const { data, error } = await gameApi.updatePost(postId.value, {
    gameText: editGameText.value.trim(),
    metagameText: editMetaText.value.trim() || undefined,
  });
  savingPost.value = false;
  if (error) {
    // The server refuses the game text by the command's name for it and says
    // "too long" or "nested too deep" without saying of what: the line names
    // it by the label the editor shows over it. The metagame text is refused
    // with a sentence of its own, which names it already.
    editResult.refuse(error, "Не удалось сохранить пост", {
      gameText: "Игровой текст",
    });
    return;
  }
  // Reflect the server-rendered result in place.
  gameTextOverride.value = data?.resource?.gameText ?? editGameText.value;
  metaTextOverride.value = data?.resource?.metagameText ?? "";
  editedAtOverride.value =
    data?.resource?.edits?.[0]?.modifiedUtc ?? new Date().toISOString();
  isEditingPost.value = false;
  emit("edited", postId.value);
}

// ──────────────────────────────────────────────────────────────────────────────
// Attachments
// ──────────────────────────────────────────────────────────────────────────────
// Kept locally once the post is on screen, so adding or removing a file updates
// the block in place — the same override idiom the edited text uses, and for the
// same reason: the parent refetches the page for its own purposes, not for this.
const attachmentsOverride = ref<PostAttachment[] | null>(null);
const attachments = computed<PostAttachment[]>(
  () => attachmentsOverride.value ?? props.post?.attachments ?? [],
);
const attachmentError = ref<string | null>(null);
const attachmentBusy = ref(false);
const attachmentsFull = computed(
  () => attachments.value.length >= MAX_POST_ATTACHMENTS,
);

// The files are not drawn in the post. A paper clip in the card's top right
// corner, the mirror of the number in the bottom right one, opens a strip of
// them over the text; one file opens the same strip, so the clip always does
// the same thing. The strip is content like the reviews, so it is a member of
// the page's "Развернуть все".
const showAttachments = ref(false);
// The editor lists the files with their controls itself; the clip would open a
// second list of the same files beside it.
const hasAttachmentsClip = computed(
  () => attachments.value.length > 0 && !isEditingPost.value,
);
const attachmentsStripId = computed(() => `post-attachments-${postId.value}`);
const attachmentsLabel = computed(() =>
  attachments.value.length > 1
    ? `Вложения: ${attachments.value.length}`
    : "Вложение",
);
const attachmentsHandleId = Symbol("post-attachments");
let unregisterAttachmentsHandle: (() => void) | null = null;

watch(
  () => attachments.value.length > 0,
  (has) => {
    if (has && !unregisterAttachmentsHandle) {
      unregisterAttachmentsHandle = registerExpandable({
        id: attachmentsHandleId,
        isExpanded: () => showAttachments.value,
        expand: () => {
          showAttachments.value = true;
        },
        collapse: () => {
          showAttachments.value = false;
        },
      });
    } else if (!has && unregisterAttachmentsHandle) {
      unregisterAttachmentsHandle();
      unregisterAttachmentsHandle = null;
      showAttachments.value = false;
    }
  },
  { immediate: true },
);
onBeforeUnmount(() => unregisterAttachmentsHandle?.());

/** Manual toggle by the clip — notifies the registry so pendingAction resets. */
function toggleAttachments() {
  showAttachments.value = !showAttachments.value;
  notifyExpandableChanged();
}

/**
 * Absolute address of an attachment.
 *
 * The payload carries the API path and the origin is the client's to add; the
 * endpoint behind it re-decides who may read the bytes on every request, so the
 * address grants nothing on its own.
 */
function attachmentHref(attachment: PostAttachment): string {
  return apiUrl(attachment.url);
}

async function addAttachments(event: Event) {
  const input = event.target as HTMLInputElement;
  const chosen = Array.from(input.files ?? []);
  // The picker keeps the chosen file selected, so picking the same one twice
  // fires no change event and looks like a dead control.
  input.value = "";
  if (!postId.value || attachmentBusy.value || chosen.length === 0) return;

  attachmentError.value = null;
  const accepted: File[] = [];
  for (const file of chosen) {
    if (attachments.value.length + accepted.length >= MAX_POST_ATTACHMENTS) {
      attachmentError.value = `Не больше ${MAX_POST_ATTACHMENTS} файлов`;
      break;
    }
    const problem = describeAttachmentProblem(file);
    if (problem) {
      attachmentError.value = `${file.name}: ${problem}`;
      continue;
    }
    accepted.push(file);
  }
  if (accepted.length === 0) return;

  attachmentBusy.value = true;
  const { failedNames } = await uploadPostAttachments(postId.value, accepted);
  attachmentBusy.value = false;

  if (failedNames.length > 0) {
    attachmentError.value = `Не удалось приложить: ${failedNames.join(", ")}`;
  }
  if (failedNames.length < accepted.length) {
    await refreshAttachments();
  }
}

async function removeAttachment(attachment: PostAttachment) {
  if (attachmentBusy.value) return;
  attachmentBusy.value = true;
  attachmentError.value = null;
  const { error } = await uploadApi.deleteUpload(attachment.id);
  attachmentBusy.value = false;
  if (error) {
    // Under the attachments of the edit, where the other refusals about
    // files already stand.
    attachmentError.value = refusalHere(error, "Не удалось убрать вложение");
    return;
  }
  attachmentsOverride.value = attachments.value.filter(
    (a) => a.id !== attachment.id,
  );
}

/**
 * Re-read the post for its attachment list.
 *
 * The upload answers with the file's own record, not with the post's list, and
 * building the list from those answers would have the client decide an order
 * the server already decides.
 */
async function refreshAttachments() {
  if (!postId.value) return;
  const { data } = await gameApi.getPost(postId.value);
  if (data?.resource) {
    attachmentsOverride.value = data.resource.attachments ?? [];
  }
}

const isDeleted = ref(false);
const showDeleteConfirm = ref(false);

function requestDeletePost() {
  showDeleteConfirm.value = true;
}

/**
 * The confirmation's action: it waits shut for the answer, prints a refusal
 * in itself and closes on success alone, so the post is taken off only once
 * the server has taken it down.
 */
async function deletePost() {
  const id = postId.value;
  if (!id) return;
  const result = await gameApi.deletePost(id, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (!result.error) {
    isDeleted.value = true;
    emit("deleted", id);
  }
  return result;
}

// Review functions
async function loadReviews() {
  if (reviewsLoaded.value || reviewsLoading.value || !postId.value) return;
  reviewsLoading.value = true;
  reviewsError.value = null;
  // Cap high enough to fetch every review in one request for the small
  // counts seen in practice, instead of silently truncating at the
  // default take=20.
  const take = Math.max(20, reviewCount.value);
  // Api never throws — failures come back in the error field
  const { data, error } = await gameApi.getPostReviews(postId.value, {
    take,
  });
  reviewsLoading.value = false;
  if (error) {
    reviewsError.value = "Не удалось загрузить оценки";
    return;
  }
  reviews.value = data?.resources ?? [];
  reviewsLoaded.value = true;
}

/**
 * A permalink to a review has to open what it points at: the block starts
 * collapsed and its list is fetched on demand, so a reader who followed one
 * landed on a post whose reviews were still hidden. Once, on arrival — what
 * the reader opens and closes afterwards is theirs.
 */
onMounted(async () => {
  const target = anchoredReviewId.value;
  if (!target) return;
  showReviews.value = true;
  await loadReviews();
  await nextTick();
  const marked = document.getElementById(`review-${target}`);
  if (marked) scrollBlockIntoView(marked);
});

/** Manual toggle by user click — notifies the registry so pendingAction resets. */
async function toggleReviews() {
  showReviews.value = !showReviews.value;
  if (showReviews.value) {
    await loadReviews();
  }
  notifyExpandableChanged();
}

async function submitReview() {
  if (!postId.value || !newReviewText.value.trim() || submittingReview.value)
    return;
  submittingReview.value = true;
  reviewRefusal.value = null;
  // Newbies may only submit the neutral sign (backend enforces ≥100 posts
  // for ±); clamp defensively even though the ± buttons are hidden for them.
  const sign = canPickSignedReview.value ? newReviewSign.value : 0;
  try {
    const { data, error } = await gameApi.createPostReview(postId.value, {
      sign,
      text: newReviewText.value.trim(),
    });
    // Out of the envelope: the review a person had just written appeared in
    // the list as an empty row while the counter beside it moved, so the two
    // disagreed on the same screen.
    const created = unwrapResource<PostReview>(data);
    if (created) {
      // First: the server lists the newest rating first and numbers the list
      // by that order, so at the end it took a number a reload would change.
      reviews.value.unshift(created);
      newReviewText.value = "";
      newReviewSign.value = canPickSignedReview.value ? 1 : 0;
      // Keep the visible rating/reviewCount consistent with the new review
      // until the parent list is refetched, via local overrides (post prop
      // itself is read-only data owned by the store/API response).
      reviewCountOverride.value = reviewCount.value + 1;
      ratingOverride.value = (postRating.value ?? 0) + sign;
      // The rating closes the game to this rater for three days: the other
      // posts of it on the page stop offering the form.
      if (props.post?.gameId) gamesRatedHere.add(props.post.gameId);
    } else if (error) {
      // The draft text is preserved (not cleared) so the user can retry, and
      // the refusal stands under it in the server's own words: a 409 says the
      // post is already rated, a 404 that it is gone. Not a toast - the form
      // is still on screen, and a toast is one more thing to dismiss beside
      // it (UI_STANDARDS, "Всплывающие сообщения").
      //
      // One carrier per refusal: a 403 (own post, newbie), a 409 (rated
      // already, or a rating in the same game during the last three days, sent
      // since the post was read) and a 5xx stand here too, and the plate the
      // interceptor held for them is taken down. A 401 and a dead connection
      // stay with the plate.
      reviewRefusal.value = refusalHere(error, "Не удалось отправить оценку");
    }
  } finally {
    submittingReview.value = false;
  }
}

/**
 * A review changed sign, so the post's rating moved by the difference. The
 * card renders its own new text; what only this widget can keep true is the
 * number in the left column, which is a sum over the very reviews it lists.
 */
function onReviewUpdated({ id, sign }: { id: string; sign: number }) {
  const review = reviews.value.find((r) => r.id === id);
  if (!review) return;
  const previous = reviewSignToNumber(review.sign);
  if (previous !== sign) {
    ratingOverride.value = (postRating.value ?? 0) + (sign - previous);
  }
  // Written back in the spelling the wire uses, so a second edit measures its
  // delta against a sign and not against a number that reads as neutral.
  review.sign = reviewSignName(sign) as unknown as PostReview["sign"];
}

/**
 * A deleted review takes its sign out of the post's rating and its row out of
 * the count — the two numbers the server recomputes from the surviving rows,
 * mirrored here so the page does not have to be refetched to agree with it.
 * The row leaves the list as well: with it gone the reader may rate the post
 * again, which is exactly what the server would now allow.
 */
function onReviewDeleted({ id, sign }: { id: string; sign: number }) {
  const deleted = reviews.value.find((r) => r.id === id);
  if (!deleted) return;
  reviews.value = reviews.value.filter((r) => r.id !== id);
  reviewCountOverride.value = Math.max(0, reviewCount.value - 1);
  ratingOverride.value = (postRating.value ?? 0) - sign;
  // The viewer took back a rating of their own. Whether the post is theirs to
  // rate again is the server's to say (another rating of theirs may still hold
  // the game's three days), so it is asked; a yes reopens the game here too.
  const username = currentUser.value?.username;
  if (username && deleted.author?.username === username) {
    void askViewerAgain(username).then((reopened) => {
      if (reopened && props.post?.gameId) {
        gamesRatedHere.delete(props.post.gameId);
      }
    });
  }
}
</script>

<script lang="ts">
import { reactive } from "vue";

/**
 * The games the viewer rated a post of on this page since its posts were read.
 * A rating closes its game to the same rater for three days, and the answers
 * the other posts came with were given before it: without this, one rating
 * left the form under every other post of the game, to be refused once the
 * text was typed. Module state on purpose, so every post on the page sees it.
 * A change of viewer empties it; a rating taken back is asked about again.
 */
const gamesRatedHere = reactive(new Set<string>());
</script>

<template>
  <article
    :id="`post-${postId}`"
    class="game-post"
    :class="{ featured: hasNavigation }"
  >
    <!-- Navigation breadcrumb (featured post only). `post.room.game`
         is a full sidebar-tier GameRef so GameLink shows the same
         tooltip as the sidebar without a second fetch, and RoomLink
         resolves its route. Plain link colors (no green/gray status
         tinting) — the sidebar coloring is a sidebar affordance. -->
    <div v-if="hasNavigation" class="post-nav">
      <template v-if="navigationLevel === 'game'">
        <GameLink :game="post.room!.game!" />
        <span class="nav-separator" aria-hidden="true"> > </span>
      </template>
      <RoomLink :room="post.room!" :game="post.room!.game!" />
    </div>

    <!-- Post card (bordered area) -->
    <div class="post-card" :class="{ 'has-attachments': hasAttachmentsClip }">
      <!-- Soft-deleted placeholder (after an author/moderator delete) -->
      <div v-if="isDeleted" class="post-deleted">Пост удален</div>

      <template v-else>
        <!-- The files of the post: a grey clip in the top right corner, where
             the number stands in the bottom right one, with their count when
             there are several. It opens the strip over the text. -->
        <button
          v-if="hasAttachmentsClip"
          type="button"
          class="attachments-toggle"
          :class="{ counted: attachments.length > 1 }"
          :aria-expanded="showAttachments"
          :aria-controls="attachmentsStripId"
          :aria-label="attachmentsLabel"
          @click="toggleAttachments"
        >
          <SvgIcon name="attachment" /><span
            v-if="attachments.length > 1"
            class="attachments-count"
            >{{ attachments.length }}</span
          >
        </button>

        <div class="post-columns">
          <!-- Left column: metadata -->
          <div ref="metaRef" class="post-meta">
            <div class="meta-inner">
              <!-- Character/Author info -->
              <template v-if="hasCharacter">
                <!-- The character's own page, the same target the game's
                     roster points a name at. -->
                <router-link
                  v-if="canLinkCharacter"
                  class="character-name"
                  :to="{
                    name: 'game-character',
                    params: {
                      id: post.room?.game?.publicId || post.room?.game?.id,
                      characterId: character?.id,
                    },
                  }"
                  >{{ characterName }}</router-link
                >
                <span v-else class="character-name">{{ characterName }}</span>
                <UserLink v-if="hasAuthor" :user="author!" hide-badge />
                <RemovedAccountLink v-else />
              </template>
              <template v-else-if="authorGameRole">
                <span class="game-role">{{ authorGameRole }}</span>
                <UserLink v-if="hasAuthor" :user="author!" hide-badge />
                <RemovedAccountLink v-else />
              </template>
              <template v-else-if="hasAuthor">
                <UserLink :user="author!" />
              </template>
              <!-- Every post has an author, so a post that arrives without one
                   was written by an account removed since: the server hides
                   the account and keeps the post. -->
              <RemovedAccountLink v-else />

              <!-- Avatar: no-default for characters (no avatar = no image).
                 size=128 — the actual render width (the 140px column minus
                 its margins); CSS stretches it to 100% width. With size=128 the
                 browser takes medium (400px) at any DPR — a large, sharp
                 portrait. -->
              <AvatarImg
                v-if="hasCharacter"
                :picture="character?.picture"
                :alt="characterName || ''"
                :size="128"
                img-class="avatar"
                no-default
              />

              <!-- Date -->
              <span class="post-date">{{ formattedDate }}</span>

              <!-- Review sum: "Оценки: <bold value>". It opens the reviews
                   under the card, and for a reader who may rate the post the
                   "+0" of a post nobody has rated opens the form of the first
                   one. -->
              <span class="rating-line"
                >Оценки:
                <!--
              --><button
                  v-if="canOpenReviews"
                  type="button"
                  class="rating-value"
                  :class="ratingColorClass"
                  :aria-expanded="showReviews"
                  :aria-controls="reviewsCollapseId"
                  :aria-label="`Оценки ${ratingText}, развернуть`"
                  @click="toggleReviews"
                >
                  <b>{{ ratingText }}</b></button
                ><span v-else class="rating-value" :class="ratingColorClass"
                  ><b>{{ ratingText }}</b></span
                >
              </span>
            </div>
          </div>

          <!-- Right column: content -->
          <div class="post-body">
            <!-- Inline edit mode (author ≤15min / moderator+) -->
            <div v-if="isEditingPost" class="post-edit">
              <span class="edit-label">Игровой текст</span>
              <BBCodeEditor
                v-model="editGameText"
                context="post"
                placeholder="Игровой текст поста..."
                :min-height="120"
                :is-moderator="isModerator"
              />
              <span class="edit-label">Метаигровой текст</span>
              <!-- Checked as the comment surface the metagame text is saved
                   on, as in the room's composer. -->
              <BBCodeEditor
                v-model="editMetaText"
                context="post"
                check-context="common"
                placeholder="Метаигровой комментарий (необязательно)..."
                :min-height="60"
                :max-height="200"
                :is-moderator="isModerator"
              />
              <!-- Attachments are their own requests, not part of the patch:
                   the post's text and its files live in different tables and
                   the edit endpoint deliberately carries only the two texts. -->
              <div class="edit-attachments">
                <span class="edit-label">Вложения</span>
                <ul v-if="attachments.length" class="attachment-list">
                  <li
                    v-for="file in attachments"
                    :key="file.id"
                    class="attachment-chip"
                  >
                    <a
                      class="attachment-name"
                      :href="attachmentHref(file)"
                      target="_blank"
                      rel="noopener"
                      >{{ file.fileName }}</a
                    >
                    <span class="attachment-size">{{
                      formatFileSize(file.sizeBytes)
                    }}</span>
                    <button
                      type="button"
                      class="attachment-remove"
                      aria-label="Убрать вложение"
                      :disabled="attachmentBusy"
                      @click="removeAttachment(file)"
                    >
                      {{ symbols.close }}
                    </button>
                  </li>
                </ul>
                <!-- Only the author attaches: the server refuses everyone else,
                     moderators included, and a live control that always ends in
                     a refusal is a promise the client has no right to make. -->
                <label v-if="isPostAuthor" class="attachment-add">
                  <input
                    type="file"
                    class="attachment-input"
                    :accept="POST_ATTACHMENT_ACCEPT"
                    :disabled="attachmentBusy || attachmentsFull"
                    multiple
                    @change="addAttachments"
                  />
                  <span class="attachment-add-label">Прикрепить файл</span>
                </label>
                <ActionResult
                  :text="attachmentError"
                  class="attachment-error"
                />
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
                <Button type="button" class="edit-btn" @click="cancelEditPost">
                  Отмена
                </Button>
              </div>
              <ActionResult :result="editResult" />
            </div>

            <template v-else>
              <!-- The strip the clip opens: one file per line, the name in the
                   post's text colour, a link to the file, and its size in the
                   grey of the edit line. The address is the content endpoint,
                   which decides who may have the bytes on every request, so a
                   copied link hands out no access the reader did not have. -->
              <div
                v-if="attachments.length"
                class="expand-fold attachments-fold"
                :class="{ open: showAttachments }"
              >
                <div class="expand-fold-clip" :inert="!showAttachments">
                  <ul :id="attachmentsStripId" class="attachments-strip">
                    <PostAttachmentRow
                      v-for="file in attachments"
                      :key="file.id"
                      :file="file"
                    />
                  </ul>
                </div>
              </div>

              <TruncatedContent
                :truncatable="shouldTruncate"
                :max-height="dynamicMaxHeight"
                :watch-key="postTextHtml"
              >
                <div class="game-text" :class="typesetting">
                  <content-text
                    :html="postTextHtml"
                    :search-query="searchQuery"
                  />
                </div>

                <!-- The rolls, in the old site's words: the heading, then one
                     numbered line per roll, every one of them. -->
                <div v-if="diceLines.length" class="dice-rolls">
                  <div class="dice-heading">{{ DICE_RESULTS_HEADING }}</div>
                  <div
                    v-for="(line, lineIndex) in diceLines"
                    :key="lineIndex"
                    class="dice-roll"
                  >
                    {{ line }}
                  </div>
                </div>

                <div
                  v-if="hasMetagameText"
                  class="metagame-text"
                  :class="typesetting"
                >
                  <content-text
                    :html="postMetagameHtml"
                    :search-query="searchQuery"
                  />
                </div>
              </TruncatedContent>

              <!-- "Отредактировано ... | Редактировать | Удалить", under the
                   last block of the post at the left edge of its text. It
                   wraps between parts where the text is narrower, and nothing
                   of it is put away for want of room (owner, 12 October
                   2026). -->
              <div v-if="showEditLine" class="post-edit-line">
                <CaptionLine>
                  <span v-if="isEdited && editedAt"
                    >Отредактировано {{ formattedEditedAt }}</span
                  >
                  <span v-else-if="isEdited">Отредактировано</span>
                  <CaptionAction v-if="canEditPost" @click="startEditPost"
                    >Редактировать</CaptionAction
                  >
                  <CaptionAction v-if="canDeletePost" @click="requestDeletePost"
                    >Удалить</CaptionAction
                  >
                </CaptionLine>
              </div>
            </template>
          </div>
        </div>

        <!-- Post footer: the number or the anchor icon alone, in the corner
             (inside the card, like DM2 td[colspan=3]) -->
        <div class="post-footer">
          <Tooltip v-if="hasNavigation" text="Перейти к посту">
            <router-link class="post-link" :to="postRoute!">{{
              symbols.returnArrow
            }}</router-link>
          </Tooltip>
          <!-- The span is the flex child, not the Tooltip: Tooltip renders a
               trigger and a teleport, so a class on it lands nowhere, and the
               flex box needs something of the number's own size to measure. -->
          <span v-else-if="number" class="post-number-tip">
            <Tooltip text="Перейти к посту">
              <a class="post-number" :href="postAnchor">{{ number }}</a>
            </Tooltip>
          </span>
        </div>
      </template>
    </div>

    <!-- Delete confirmation (soft delete) -->
    <ConfirmDialog
      v-model:show="showDeleteConfirm"
      title="Удалить пост?"
      message="Пост будет удален, вернуть его сможет только модерация"
      confirm-label="Удалить"
      failure="Не удалось удалить пост"
      :action="deletePost"
    />

    <!-- Reviews section (below card, grid-based collapse animation) -->
    <div
      :id="reviewsCollapseId"
      class="reviews-collapse"
      :class="{ expanded: showReviews }"
      :inert="!showReviews"
    >
      <div class="reviews-overflow">
        <SecondaryText v-if="reviewsLoading" class="reviews-status">
          Загрузка...
        </SecondaryText>
        <SecondaryText v-else-if="reviewsError" class="reviews-status">
          {{ reviewsError }}
        </SecondaryText>
        <!-- The reviews and, under them, the form for whoever may still
             write one. Who may not gets the list alone: an own post and a
             post already rated said so in a line where the form would be,
             and a newbie read under his single "=" what it already showed. -->
        <ul
          v-if="hasReviews || (showReviews && showReviewForm)"
          class="reviews-section"
        >
          <PostReviewItem
            v-for="(review, i) in reviews"
            :key="review.id"
            :review="review"
            :number="i + 1"
            :to="reviewRoute(review.id)"
            :highlight="isReviewHighlighted(review)"
            :editable="editable"
            @updated="onReviewUpdated"
            @deleted="onReviewDeleted"
          />
          <!-- Review form (eligible logged-in users) -->
          <!-- A composer of the page: the sign as segments, the text field
               across the column, "Отправить" under it at the left. -->
          <li v-if="showReviews && showReviewForm" class="review-form">
            <SegmentedControl
              :model-value="String(newReviewSign)"
              :options="reviewSigns"
              ariaLabel="Знак оценки"
              @update:model-value="newReviewSign = reviewSignToNumber($event)"
            />
            <textarea
              v-model="newReviewText"
              class="review-input"
              placeholder="Текст оценки..."
              aria-label="Текст оценки"
              rows="2"
            ></textarea>
            <Button
              type="button"
              variant="primary"
              class="submit-btn"
              :disabled="!newReviewText.trim() || submittingReview"
              @click="submitReview"
            >
              Отправить
            </Button>
            <ActionResult :text="reviewRefusal" class="form-error" />
          </li>
        </ul>
      </div>
    </div>
  </article>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Animations" as *
@use "@/assets/styles/BbcodeContent" as *

// ============================================================================
// Game Post — layout dimensions matching DM2
// ============================================================================

// Navigation (featured post only, above card)
.post-nav
  margin-bottom: $small
  a
    color: $link
    // No local text-decoration override — global a:hover rule provides
    // the underline on hover.
    &:hover
      color: $link-hover

.nav-separator
  color: $text-muted

// Card: DM2 .commentItem { padding: 5px; border: 1px dashed; background }
// Positioned for the attachment clip in its top right corner.
.post-card
  position: relative
  padding: 5px
  background-color: $bg-element
  border: 1px dashed $border

// Two-column layout: DM2 table (td.firstrow 140px + td.secondrow 10px spacer +
// td.thirdrow the rest), at every width: a phone shows the post of 1366.
.post-columns
  display: flex
  gap: 10px
  align-items: flex-start

// ──────────────────────────────────────────────────────────────────────────────
// Left column — DM2: td.firstrow { width: 140px }, its lines on DM2's step of
// 20 ("line-height: normal" of PT Sans). A name or a login wider than the
// column wraps inside it, as there.
// ──────────────────────────────────────────────────────────────────────────────
.post-meta
  flex-shrink: 0
  width: 140px
  text-align: left
  font-size: $font-size
  line-height: 1.25

// DM2: div.p { margin: 11px 5px; padding-right: 2px } inside td.firstrow, which
// leaves 128 for the name, the login, the portrait and the date.
.meta-inner
  margin: 11px 5px
  padding-right: 2px

  // DM2: gray6 is block, br tags separate elements — block children replicate this
  > *
    display: block

// Character name: #666 gray, not bold (DM2 .gray6 { color: #666 })
.character-name
  display: block
  padding-bottom: 4px
  color: $heading-alt

a.character-name
  &:hover
    color: $link-hover

// Game role (DungeonMaster / Assistant): bold, same #666 gray
.game-role
  display: block
  padding-bottom: 4px
  font-weight: bold
  color: $heading-alt

// Avatar: constrained by .meta-inner width (same as username text area)
.avatar
  display: block
  box-sizing: border-box
  width: 100%
  max-height: 500px
  height: auto
  margin: 2px 0
  border: 1px solid $border

// Date: normal text color (not muted), on one line as on DM2. The site's
// format, "25.07.2026 в 18:29", is a few pixels wider than the 128 of the
// column's content and of a fixed width: it runs into the 7px the column keeps
// at its right and ends inside the column, short of the spacer.
.post-date
  color: $text
  white-space: nowrap

// ──────────────────────────────────────────────────────────────────────────────
// Review sum: "Оценки: <bold +N>"
// ──────────────────────────────────────────────────────────────────────────────
// "Оценки:" label in normal text color, value is colored
.rating-line
  color: $text
  font-size: $font-size

.rating-value
  font-size: $font-size

  &.positive
    color: $accent-green
  &.negative
    color: $accent-red
  &.neutral
    color: $text-muted

// Reviews toggle — real <button> for keyboard access, reset to look
// exactly like the inline link it replaced. The pointer gets an underline
// and nothing else, the way a caption action answers it: the sum keeps the
// colour of its sign, and a grey "+0" never turns into a blue link.
button.rating-value
  display: inline
  margin: 0
  padding: 0
  border: none
  background: none
  font: inherit
  font-size: $font-size
  cursor: pointer
  text-decoration: none
  &:hover
    text-decoration: underline

// ──────────────────────────────────────────────────────────────────────────────
// Right column — DM2: td.thirdrow { valign: top; width: 100% }
// No min-height. The line of the text is the text's own (below), not the
// column's: the edit view and the strips keep the line of the page.
// The column's 11px above and below is the margin of DM2's div.dmtxt, the 11px
// of each block inside it the margin of its div.p: the text starts 22px under
// the top of the column, 11px under the name beside it, and ends 22px over the
// row of the number.
// ──────────────────────────────────────────────────────────────────────────────
.post-body
  flex: 1
  min-width: 0
  padding: 11px 0
  word-wrap: break-word
  word-break: break-word
  overflow-wrap: break-word
  color: $text

// Game text: DM2 div.dmtxt { margin: 11px 5px } > div.p { margin: 11px 5px },
// 10px in from either edge of the column.
.game-text
  color: $text
  margin: 11px 10px

// "... показать полностью" of a long post at the left edge of its text, as it
// stands under the text of a comment.
.post-body :deep(.truncated-expand-button)
  margin-left: 10px

// Metagame text: DM2 div.p.postcomment { color: #7B532B }, set as the game
// text is and with no caption of its own.
.metagame-text
  color: $text-meta
  margin: 11px 10px

  b, strong, i, em, u, s, strike, del, pre, code, li, span, div
    color: inherit

// Right under the game text the brown text stands 33px off, as on DM2, where
// the two stood each in a div.dmtxt of its own with a div.p inside, 11px of
// margin apiece. That is farther than a blank line of the text (20px), so it
// reads as a word apart. Under the rolls it keeps their 22px, as there.
.game-text + .metagame-text
  margin-top: 33px

// DM2 set this text in 16px on a step of 20, the post and every comment alike,
// flush on both sides, its words broken while the reader's switch on the
// settings panel is on.
.game-text,
.metagame-text
  +body-text

// Dice rolls, DM2 div.diceRollsbg: a flat strip with no frame and no rounding,
// from the text's left edge to the column's right edge, 10px past the text;
// its text stands 7px in and 2px from the top and bottom, one roll per line,
// and 22px of air separate it from the blocks on either side. Flush on both
// sides like every text block of DM2, the words of a roll never broken. Its
// ground is the grey of the theme nearest DM2's rgb(230, 230, 229) among the
// grey grounds of the site's blocks: $bg-element-accent, #eaeaea, the ground
// of a table's head, against the #ededed of $overlay-subtle on the card.
.dice-rolls
  margin: 22px 0 22px 10px
  padding: 2px 7px
  background-color: $bg-element-accent
  color: $text
  font-size: $font-size
  line-height: 1.25
  text-align: justify
  hyphens: manual

  // The last block of the post: the column adds its own 11px under it.
  &:last-child
    margin-bottom: 11px

// ──────────────────────────────────────────────────────────────────────────────
// Post footer — DM2: td[colspan=3] height=0, span.comment float:right: the
// number in the very corner of the card's padding.
// ──────────────────────────────────────────────────────────────────────────────
.post-footer
  display: flex
  justify-content: flex-end
  line-height: 15px

// A flex child blockifies and takes a line box of whatever font-size it
// inherits — 16px from the card, while the number inside is 12. Sizing this
// wrapper with its content keeps the footer the height it was when the number
// sat in the row bare, without a tooltip around it.
.post-number-tip
  font-size: $tertiary-font-size

// Link colour, not muted: every "go to the post" affordance looks like a link
// (UI_STANDARDS, owner decision 2026-08-25) - the same element renders on pages
// where it is pure navigation, and one glyph must not wear two coats.
.post-number
  font-size: $tertiary-font-size
  +link-tone($link)

.post-link
  font-size: $secondary-font-size
  +link-tone($link)

// ──────────────────────────────────────────────────────────────────────────────
// Reviews section — grid-based collapse for smooth animation
// Using grid-template-rows 0fr/1fr avoids the max-height jank
// ──────────────────────────────────────────────────────────────────────────────
.reviews-collapse
  display: grid
  grid-template-rows: 0fr
  // Unified reveal tokens — same tempo as TruncatedContent, ExpandableList
  // and the BBCode spoiler/NSFW blocks. Reduced-motion: Reset.sass.
  transition: grid-template-rows $expand-duration $expand-easing
  &.expanded
    grid-template-rows: 1fr

.reviews-overflow
  overflow: hidden

// Bullet indent follows the old site (ModuleBestPosts), where the list sits on
// the browser's default 40px padding and the reviews are clearly set off from
// the post's edge. It used to be a hand-picked 25px, off the spacing scale,
// and the bullets nearly touched the text beside them.
.reviews-section
  list-style: disc
  margin: 0
  padding-top: $small
  padding-left: $big + $small

// Loading / error line shown while reviews are fetched — aligned
// with the reviews list content
.reviews-status
  padding-top: $small
  padding-left: 25px

// ──────────────────────────────────────────────────────────────────────────────
// Review creation form (at bottom of reviews list)
// ──────────────────────────────────────────────────────────────────────────────
// A column: the sign, the text, the button at its own width at the left, as
// every composer of the site stands (the comment, the chat, the post).
.review-form
  list-style: none
  margin-top: $small
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small

// A text area of the site (+input): its frame, ground, padding and type,
// across the column, growing downwards only.
.review-input
  box-sizing: border-box
  width: 100%
  resize: vertical

// The refusal of a send, under the button it refused. No margin of its own:
// the gap of the column spaces it.
.form-error
  --action-result-margin: 0

// ──────────────────────────────────────────────────────────────────────────────
// Author / moderator controls: edit, delete, edited indicator
// ──────────────────────────────────────────────────────────────────────────────
.post-deleted
  padding: $small
  color: $text-muted
  font-style: italic

.post-edit
  display: flex
  flex-direction: column
  gap: $tiny
  margin: 11px 10px

// A caption of a field of the page: +field-caption, as FormField draws it.
.edit-label
  +field-caption

.edit-actions
  display: flex
  gap: $small
  margin-top: $small


// The edit line, DM2 geometry (div.p.comment { margin: 11px 5px 11px 10px }):
// 12px grey at the left edge of the text, 22px under the last block of the
// post (that block's 11px and this line's own), and 11px above the number in
// the corner (the column's). The caption's size on the block as well, so the
// line it stands on is the caption's 15px and not the text's.
.post-edit-line
  display: block
  margin: 11px 5px 0 10px
  font-size: $tertiary-font-size
  line-height: 15px
  text-align: left

// ──────────────────────────────────────────────────────────────────────────────
// Attachments — the clip in the corner and the strip it opens, and the edit
// view inside the editor
// ──────────────────────────────────────────────────────────────────────────────
// The clip mirrors the number: its ink stands as far from the top border as
// the number's ink from the bottom one, and as far from the right border. The
// svg box is the ink of the clip; a count is text, whose ink starts a hair
// inside its box, so a clip with a count keeps the box where the number's box
// is. The count is 12px and sits on the line of the clip's lowest point.
.attachments-toggle
  position: absolute
  top: 8.6px
  right: 5.55px
  display: flex
  align-items: baseline
  gap: 3px
  margin: 0
  padding: 0
  border: none
  background: none
  font-family: inherit
  font-size: $tertiary-font-size
  line-height: 15px
  color: $text-muted
  cursor: pointer

  &.counted
    right: 5px

  // The glyph stays 14px; the pointer target grows around it to the floor.
  &::after
    content: ""
    position: absolute
    top: 50%
    left: 50%
    width: max(100%, #{$tap-target-min})
    height: max(100%, #{$tap-target-min})
    transform: translate(-50%, -50%)

  :deep(.svg-icon)
    display: block
    width: 14px
    height: auto

  &:hover .attachments-count
    text-decoration: underline

// The strip of the rolls, over the text: its left edge on the text's, its
// right edge on the rolls' strip, 22px of air under it (its 11px and the
// text's). It starts where the capitals of the text started, which leaves the
// clip equal air above and below.
.attachments-strip
  list-style: none
  margin: 16px 0 11px 10px
  padding: 2px 7px
  background-color: $bg-element-accent
  color: $text
  font-size: $font-size
  text-align: left
  hyphens: manual

.attachment-name
  color: $link
  overflow-wrap: anywhere

.attachment-size
  color: $text-muted
  font-size: $secondary-font-size

.edit-attachments
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $tiny
  margin-top: $small

.attachment-list
  list-style: none
  display: flex
  flex-wrap: wrap
  gap: $small
  margin: 0

.attachment-chip
  display: inline-flex
  align-items: center
  gap: $tiny
  padding: $tiny $small
  background-color: $bg-element
  border: 1px solid $border
  border-radius: $border-radius

.attachment-remove
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 0 $tiny
  border: none
  background: transparent
  color: $text-muted
  font-size: $font-size
  line-height: 1
  cursor: pointer

  &:hover:not(:disabled)
    color: $accent-red

// The native picker is the click target and the label covers it, the same way
// the avatar picker does.
.attachment-add
  position: relative
  display: inline-flex

.attachment-input
  position: absolute
  inset: 0
  width: 100%
  opacity: 0
  cursor: pointer

  &:disabled
    cursor: default

.attachment-add-label
  +button

// Under the control that adds files, spaced by the block of attachments.
.attachment-error
  --action-result-margin: 0
</style>
