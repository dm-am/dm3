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
import type {
  DiceRoll,
  Post,
  PostAttachment,
  PostReview,
} from "@/entities/game";
import {
  gameApi,
  GameLink,
  PostReviewItem,
  RoomLink,
  reviewSignToNumber,
  reviewSignName,
} from "@/entities/game";
import { ContentText } from "@/shared/ui/Content";
import { SecondaryText } from "@/shared/ui/Layout";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { UserLink, userIsModerator } from "@/entities/user";
import {
  MAX_POST_ATTACHMENTS,
  POST_ATTACHMENT_ACCEPT,
  attachmentImageBox,
  describeAttachmentProblem,
  isImageAttachment,
  reserveAttachmentImageBoxes,
  uploadPostAttachments,
} from "@/features/upload";
import { unwrapResource, uploadApi } from "@/shared/api";
import { apiUrl } from "@/shared/api/client";
import { formatFileSize } from "@/shared/lib/utils/fileSize";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { trimHtmlWhitespace } from "@/shared/lib/utils/bbcodeInteractive";
import { useAuthStore } from "@/shared/stores/auth";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";
import { symbols } from "@/shared/lib/utils/icons";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { scrollBlockIntoView } from "@/shared/lib/scroll";
import { useToast } from "@/shared/lib/composables/useToast";
import { useQuoteAction } from "@/shared/lib/composables/useQuoteComposer";
import { notifyFailure } from "@/shared/lib/errors";

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
const toast = useToast();

// 15-minute author edit window — a client-side affordance matching the
// Comment block; moderators+ may edit/delete regardless (PostIntention).
const EDIT_TIME_LIMIT_MINUTES = 15;

// Quoting. The action exists only where there is a composer to answer in, and
// the room's composer is what provides it — a post shown on a rating listing,
// where there is nothing to write into, has no button.
const { canQuote, quote } = useQuoteAction();

function quotePost() {
  const postId = props.post?.id;
  if (!postId) return;
  return quote(() => gameApi.getPostQuote(postId));
}

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
const hasDiceRolls = computed(
  () => props.post?.diceRolls && props.post.diceRolls.length > 0,
);
// After a successful inline edit the server returns the freshly rendered
// HTML; keep it as a local override so the post updates in place without the
// parent having to refetch (mirrors the reviewCount/rating override idiom).
const gameTextOverride = ref<string | null>(null);
const metaTextOverride = ref<string | null>(null);
const editedOverride = ref(false);

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

/**
 * Splits a dice roll into the muted "dNN: R +B" prefix and the bold green
 * "= T" total, built in script so the template never needs adjacent
 * mustaches (whitespace-condense would otherwise glue "+2= 6" together).
 */
function diceLinePrefix(roll: DiceRoll): string {
  const values = (roll.results ?? []).map((die) => die.value);
  const bonusPart = roll.bonus
    ? ` ${roll.bonus > 0 ? "+" : ""}${roll.bonus}`
    : "";
  // Several dice are shown as the throw itself, "3d6: 4 2 6", so the reader can
  // check the total; one die needs no list beside its own value.
  const thrown = values.length > 1 ? values.join(" ") : (values[0] ?? "");
  return `${roll.rolls}d${roll.edges}: ${thrown}${bonusPart}`;
}

function diceLineTotal(roll: DiceRoll): string {
  const sum = (roll.results ?? []).reduce(
    (running: number, die) => running + die.value,
    0,
  );
  return `= ${sum + (roll.bonus || 0)}`;
}

// Rating — "Рейтинг: +N" format, bold colored link
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
// Review-form gating (doc 4.2.2.14). The backend is authoritative
// (PostReviewService): own post → forbidden, one review per post, newbies may
// pick only "=" (need ≥100 posts for ±), one review per game every 3 days.
// The client mirrors the deterministic gates and surfaces the server's reason
// for the rest (cooldown) on submit.
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
const showReviewForm = computed(
  () =>
    authStore.isAuthenticated &&
    !isPostAuthor.value &&
    !alreadyVoted.value &&
    !isDeleted.value,
);

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
const isEdited = computed(
  () => editedOverride.value || (props.post?.edits?.length ?? 0) > 0,
);

const isEditingPost = ref(false);
const editGameText = ref("");
const editMetaText = ref("");
const savingPost = ref(false);

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
}

async function saveEditPost() {
  if (!postId.value || !editGameText.value.trim() || savingPost.value) return;
  savingPost.value = true;
  const { data, error } = await gameApi.updatePost(postId.value, {
    gameText: editGameText.value.trim(),
    metagameText: editMetaText.value.trim() || undefined,
  });
  savingPost.value = false;
  if (error) {
    notifyFailure(error, "Не удалось сохранить пост");
    return;
  }
  // Reflect the server-rendered result in place.
  gameTextOverride.value = data?.resource?.gameText ?? editGameText.value;
  metaTextOverride.value = data?.resource?.metagameText ?? "";
  editedOverride.value = true;
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
  const { error } = await uploadApi.deleteUpload(attachment.id);
  attachmentBusy.value = false;
  if (error) {
    notifyFailure(error, "Не удалось убрать вложение");
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
const deletingPost = ref(false);

function requestDeletePost() {
  showDeleteConfirm.value = true;
}

async function confirmDeletePost() {
  if (!postId.value || deletingPost.value) return;
  deletingPost.value = true;
  const { error } = await gameApi.deletePost(postId.value);
  deletingPost.value = false;
  showDeleteConfirm.value = false;
  if (error) {
    notifyFailure(error, "Не удалось удалить пост");
    return;
  }
  isDeleted.value = true;
  emit("deleted", postId.value);
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
      reviews.value.push(created);
      newReviewText.value = "";
      newReviewSign.value = canPickSignedReview.value ? 1 : 0;
      // Keep the visible rating/reviewCount consistent with the new review
      // until the parent list is refetched, via local overrides (post prop
      // itself is read-only data owned by the store/API response).
      reviewCountOverride.value = reviewCount.value + 1;
      ratingOverride.value = (postRating.value ?? 0) + sign;
    } else if (error) {
      // The draft text is preserved (not cleared) so the user can retry.
      // 403 (own post / newbie) and 429 (per-game cooldown) are already
      // surfaced by the global response interceptor — only add a message
      // for the cases it does not cover.
      const status = (error as { status?: number }).status;
      if (status === 409) {
        notifyFailure(error, "Вы уже оценили этот пост");
      } else if (status !== 403 && status !== 429) {
        toast.error("Не удалось отправить оценку");
      }
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
  if (!reviews.value.some((r) => r.id === id)) return;
  reviews.value = reviews.value.filter((r) => r.id !== id);
  reviewCountOverride.value = Math.max(0, reviewCount.value - 1);
  ratingOverride.value = (postRating.value ?? 0) - sign;
}
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
    <div class="post-card">
      <!-- Soft-deleted placeholder (after an author/moderator delete) -->
      <div v-if="isDeleted" class="post-deleted">Пост удален</div>

      <template v-else>
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
              </template>
              <template v-else-if="authorGameRole">
                <span class="game-role">{{ authorGameRole }}</span>
                <UserLink v-if="hasAuthor" :user="author!" hide-badge />
              </template>
              <template v-else-if="hasAuthor">
                <UserLink :user="author!" />
              </template>
              <template v-else>
                <span class="system-text">Системное</span>
              </template>

              <!-- Avatar: no-default for characters (no avatar = no image).
                 size=150 — the actual render width (the 160px column minus
                 margin); CSS stretches it to 100% width. With size=150 the browser
                 takes medium (400px) at any DPR — a large, sharp portrait. -->
              <AvatarImg
                v-if="hasCharacter"
                :picture="character?.picture"
                :alt="characterName || ''"
                :size="150"
                img-class="avatar"
                no-default
              />

              <!-- Date -->
              <span class="post-date">{{ formattedDate }}</span>

              <!-- Rating: "Рейтинг: <bold value>" -->
              <span class="rating-line"
                >Рейтинг:
                <!--
              --><button
                  v-if="hasReviews"
                  type="button"
                  class="rating-value"
                  :class="ratingColorClass"
                  :aria-expanded="showReviews"
                  :aria-controls="reviewsCollapseId"
                  :aria-label="`Рейтинг ${ratingText}, показать оценки`"
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
              <BBCodeEditor
                v-model="editMetaText"
                context="post"
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
                <div v-if="attachmentError" class="attachment-error">
                  {{ attachmentError }}
                </div>
              </div>

              <div class="edit-actions">
                <button
                  class="edit-btn save"
                  :disabled="savingPost || !editGameText.trim()"
                  @click="saveEditPost"
                >
                  Сохранить
                </button>
                <button class="edit-btn" @click="cancelEditPost">Отмена</button>
              </div>
            </div>

            <TruncatedContent
              v-else
              :truncatable="shouldTruncate"
              :max-height="dynamicMaxHeight"
              :watch-key="postTextHtml"
            >
              <div class="game-text">
                <content-text
                  :html="postTextHtml"
                  :search-query="searchQuery"
                />
              </div>

              <div v-if="hasDiceRolls" class="dice-rolls">
                <div
                  v-for="(roll, rollIndex) in post.diceRolls"
                  :key="rollIndex"
                  class="dice-roll"
                >
                  <span class="dice-result"
                    >{{ diceLinePrefix(roll) }}
                    <span class="dice-total">{{
                      diceLineTotal(roll)
                    }}</span></span
                  >
                  <span v-if="roll.comment" class="dice-comment">{{
                    roll.comment
                  }}</span>
                </div>
              </div>

              <!-- A picture is shown as one, and everything else stays a name
                   and a size. The address is the content endpoint, which decides
                   who may have the bytes on every request — so drawing it here
                   hands out no access the reader did not already have, and a
                   file in a closed room stays in it. What the file is comes from
                   the server's own reading of it, not from its name.

                   Name and size stay under the picture: they are what the link
                   to the file is, and the map an author attaches is rarely named
                   by what it shows. -->
              <div v-if="attachments.length" class="attachments">
                <div
                  v-for="file in attachments"
                  :key="file.id"
                  class="attachment-entry"
                >
                  <!-- The declared pair reserves the box before the bytes
                       arrive, the same pair a picture of this post in the text
                       gets; an attachment with no measured size simply goes
                       without, which is the browser's own behaviour. -->
                  <img
                    v-if="isImageAttachment(file)"
                    class="attachment-image"
                    :src="attachmentHref(file)"
                    :alt="file.fileName"
                    :width="attachmentImageBox(file)?.width"
                    :height="attachmentImageBox(file)?.height"
                    loading="lazy"
                  />
                  <a
                    class="attachment"
                    :href="attachmentHref(file)"
                    target="_blank"
                    rel="noopener"
                  >
                    <span class="attachment-name">{{ file.fileName }}</span>
                    <span class="attachment-size">{{
                      formatFileSize(file.sizeBytes)
                    }}</span>
                  </a>
                </div>
              </div>

              <div v-if="hasMetagameText" class="metagame-text">
                <content-text
                  :html="postMetagameHtml"
                  :search-query="searchQuery"
                />
              </div>
            </TruncatedContent>
          </div>
        </div>

        <!-- Post footer: number or anchor icon (inside card, like DM2 td[colspan=3]) -->
        <div class="post-footer">
          <span
            v-if="
              !isEditingPost &&
              (canQuote || canEditPost || canDeletePost || isEdited)
            "
            class="post-controls"
          >
            <button
              v-if="canQuote"
              type="button"
              class="post-action-btn"
              @click="quotePost"
            >
              Цитировать
            </button>
            <button
              v-if="canEditPost"
              type="button"
              class="post-action-btn"
              @click="startEditPost"
            >
              Редактировать
            </button>
            <button
              v-if="canDeletePost"
              type="button"
              class="post-action-btn delete"
              @click="requestDeletePost"
            >
              Удалить
            </button>
            <span v-if="isEdited" class="post-edited">(отредактировано)</span>
          </span>

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
      :show="showDeleteConfirm"
      title="Удалить пост?"
      message="Пост будет удален. Это действие можно отменить только через модерацию."
      confirm-label="Удалить"
      danger
      :loading="deletingPost"
      @confirm="confirmDeletePost"
      @update:show="showDeleteConfirm = $event"
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
        <ul
          v-if="hasReviews || (showReviews && authStore.isAuthenticated)"
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
          <li v-if="showReviews && showReviewForm" class="review-form">
            <div class="sign-selector">
              <button
                v-if="canPickSignedReview"
                class="sign-btn"
                :class="{
                  active: newReviewSign === 1,
                  positive: newReviewSign === 1,
                }"
                aria-label="Положительная оценка"
                @click="newReviewSign = 1"
              >
                +
              </button>
              <button
                class="sign-btn"
                :class="{
                  active: newReviewSign === 0,
                  neutral: newReviewSign === 0,
                }"
                aria-label="Нейтральная оценка"
                @click="newReviewSign = 0"
              >
                =
              </button>
              <button
                v-if="canPickSignedReview"
                class="sign-btn"
                :class="{
                  active: newReviewSign === -1,
                  negative: newReviewSign === -1,
                }"
                aria-label="Отрицательная оценка"
                @click="newReviewSign = -1"
              >
                −
              </button>
            </div>
            <div class="review-input-col">
              <textarea
                v-model="newReviewText"
                class="review-input"
                placeholder="Текст оценки..."
                aria-label="Текст оценки"
                rows="2"
              ></textarea>
              <SecondaryText v-if="!canPickSignedReview" class="review-hint">
                Новичкам доступна только нейтральная оценка (нужно 100 постов
                для "+" и "−")
              </SecondaryText>
            </div>
            <button
              class="submit-btn"
              :disabled="!newReviewText.trim() || submittingReview"
              @click="submitReview"
            >
              Отправить
            </button>
          </li>

          <!-- Ineligible notice: own post or already voted -->
          <li
            v-else-if="showReviews && authStore.isAuthenticated"
            class="review-notice"
          >
            <SecondaryText>
              {{
                isPostAuthor
                  ? "Нельзя оценивать собственный пост"
                  : "Вы уже оценили этот пост"
              }}
            </SecondaryText>
          </li>
        </ul>
      </div>
    </div>
  </article>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Animations" as *

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
.post-card
  padding: 5px
  background-color: $bg-element
  border: 1px dashed $border

// Two-column layout: DM2 table (firstrow 180px + 10px spacer + thirdrow auto)
.post-columns
  display: flex
  gap: 10px
  align-items: flex-start

// ──────────────────────────────────────────────────────────────────────────────
// Left column — DM2: td.firstrow { width: 140px }
// Widened to 160px so LongestLoginPossible fits on one line
// ──────────────────────────────────────────────────────────────────────────────
.post-meta
  flex-shrink: 0
  width: 160px
  text-align: left
  font-size: $font-size

// DM2: div.p { margin: 11px 5px } inside td.firstrow
.meta-inner
  margin: 11px 5px

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

// Date: normal text color (not muted)
.post-date
  color: $text

.system-text
  font-style: italic
  color: $text-muted

// ──────────────────────────────────────────────────────────────────────────────
// Rating: "Рейтинг: <bold +N>"
// ──────────────────────────────────────────────────────────────────────────────
// "Рейтинг:" label in normal text color, value is colored
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
// exactly like the inline link it replaced
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
  &.positive:hover
    color: $accent-green-hover
  &.negative:hover
    color: $accent-red-hover
  &.neutral:hover
    color: $link-hover

// ──────────────────────────────────────────────────────────────────────────────
// Right column — DM2: td.thirdrow { valign: top; width: 100% }
// No min-height, no explicit line-height (browser default ~1.2)
// ──────────────────────────────────────────────────────────────────────────────
.post-body
  flex: 1
  min-width: 0
  padding-top: $small
  word-wrap: break-word
  word-break: break-word
  overflow-wrap: break-word
  color: $text

// Game text: DM2 div.dmtxt { margin: 11px 5px } + #main .content { padding-right: 20px }
.game-text
  color: $text
  margin: 11px 15px 11px 5px
  text-align: justify
  hyphens: auto
  -webkit-hyphens: auto

// Metagame text: DM2 div.p.postcomment { color: #7B532B }
.metagame-text
  color: $text-meta
  margin: 11px 15px 11px 5px
  text-align: justify
  hyphens: auto
  -webkit-hyphens: auto

  b, strong, i, em, u, s, strike, del, pre, code, li, span, div
    color: inherit

// Dice rolls: DM2 .diceRollsbg { padding: 0 2px } + .diceLine { margin-top: 4px }
.dice-rolls
  padding: 0 2px

.dice-roll
  padding: 2px 0
  margin-top: 4px

// DM2: .diceLine SPAN { border: 1px solid; padding: 2px; font-size: 16px; bold }
.dice-result
  font-size: $font-size
  font-weight: bold
  color: $heading
  padding: 2px
  border: 1px solid $border

.dice-total
  font-weight: bold
  color: $accent-green

.dice-comment
  color: $text-muted
  font-style: italic

// ──────────────────────────────────────────────────────────────────────────────
// Post footer — DM2: td[colspan=3] height=0, span.comment float:right
// ──────────────────────────────────────────────────────────────────────────────
.post-footer
  display: flex
  justify-content: flex-end
  padding-right: 7px

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
  color: $link
  font-size: $tertiary-font-size
  text-decoration: none
  &:hover
    color: $link-hover

.post-link
  color: $link
  font-size: $secondary-font-size
  text-decoration: none
  &:hover
    color: $link-hover

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
.review-form
  list-style: none
  margin-top: $small
  display: flex
  gap: $small
  align-items: flex-start

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
  min-height: 40px
  padding: $minor
  border: 1px solid $border
  font-size: $font-size
  font-family: inherit
  resize: vertical
  color: $text
  background: $bg-page

.submit-btn
  margin-top: $small
  +button

.review-input-col
  flex: 1
  display: flex
  flex-direction: column
  gap: $tiny

.review-input-col .review-input
  flex: none

.review-hint
  font-size: $tertiary-font-size

.review-notice
  list-style: none
  margin-top: $small

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
  margin: 11px 15px 11px 5px

.edit-label
  color: $text-muted
  font-size: $secondary-font-size

.edit-actions
  display: flex
  gap: $small
  margin-top: $small

.edit-btn
  +button
  &.save
    font-weight: bold

// Controls sit at the left of the footer; the number/link stays pinned right.
.post-controls
  display: inline-flex
  align-items: center
  gap: $small
  margin-right: auto

.post-action-btn
  padding: 0 $tiny
  font-size: $secondary-font-size
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted
  &:hover
    color: $link
  &.delete:hover
    color: $accent-red

.post-edited
  color: $text-muted
  font-size: $tertiary-font-size
  font-style: italic

// ──────────────────────────────────────────────────────────────────────────────
// Attachments — read view under the game text, edit view inside the editor
// ──────────────────────────────────────────────────────────────────────────────
.attachments
  display: flex
  flex-wrap: wrap
  gap: $small
  padding: 0 2px
  margin-top: $small

// One attachment: its picture, when it is one, over the link that names it.
// max-width and min-width are what keep a wide file inside the column instead
// of stretching the row it wraps in.
.attachment-entry
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $tiny
  max-width: 100%
  min-width: 0

// Drawn to the same contract as a picture in the post's text, declared once in
// _BbcodeContent.sass: the width/height pair the template puts on reserves the
// box, max-width shrinks the whole box with the column, and the height follows
// the ratio rather than being clamped on its own — with both sides declared the
// two caps would cut each side separately and squash the picture. The cap reads
// the same custom property the text images do, so the collapsed state of
// TruncatedContent shrinks this picture along with them.
.attachment-image
  display: block
  max-width: 100%
  max-height: var(--bb-image-max-height, 500px)
  height: auto

.attachment
  display: inline-flex
  align-items: baseline
  gap: $tiny
  padding: 2px $tiny
  border: 1px solid $border
  border-radius: $border-radius
  text-decoration: none

  &:hover .attachment-name
    text-decoration: underline

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

.attachment-error
  color: $accent-red
  font-size: $secondary-font-size
</style>
