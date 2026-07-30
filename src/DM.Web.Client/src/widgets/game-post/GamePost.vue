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
import { storeToRefs } from "pinia";
import type { Post, PostReview } from "@/entities/game";
import { gameApi, GameLink, PostReviewItem, RoomLink } from "@/entities/game";
import {
  ContentText,
  SecondaryText,
  Tooltip,
  TruncatedContent,
} from "@/shared/ui";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { UserLink, AvatarImg, userIsModerator } from "@/entities/user";
import { trimHtmlWhitespace } from "@/shared/lib/utils/bbcodeInteractive";
import { useAuthStore } from "@/shared/stores/auth";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";
import { symbols } from "@/shared/lib/utils/icons";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const props = withDefaults(
  defineProps<{
    post: Post;
    number?: number;
    /** Featured post mode — shows navigation breadcrumb, anchor icon instead of number */
    showNavigation?: boolean;
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
// or eat the collapsed budget. Pure transforms, no DOM mutation.
const postTextHtml = computed(() =>
  trimHtmlWhitespace(effectiveGameText.value),
);
const postMetagameHtml = computed(() =>
  trimHtmlWhitespace(effectiveMetaText.value),
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
function diceLinePrefix(roll: {
  dice: number;
  result: number;
  bonus?: number;
}): string {
  const bonusPart = roll.bonus
    ? ` ${roll.bonus > 0 ? "+" : ""}${roll.bonus}`
    : "";
  return `d${roll.dice}: ${roll.result}${bonusPart}`;
}

function diceLineTotal(roll: { result: number; bonus?: number }): string {
  return `= ${roll.result + (roll.bonus || 0)}`;
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

function copyAnchorLink() {
  navigator.clipboard.writeText(
    window.location.origin + window.location.pathname + postAnchor.value,
  );
}

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

function startEditPost() {
  // Seed from the currently displayed text (mirrors the Comment edit block;
  // the dual-mode BBCodeEditor round-trips the rendered HTML).
  editGameText.value = effectiveGameText.value ?? "";
  editMetaText.value = effectiveMetaText.value ?? "";
  isEditingPost.value = true;
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
    reviewsError.value = "Не удалось загрузить отзывы";
    return;
  }
  reviews.value = data?.resources ?? [];
  reviewsLoaded.value = true;
}

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
    if (data) {
      reviews.value.push(data);
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
        toast.error("Не удалось отправить отзыв");
      }
    }
  } finally {
    submittingReview.value = false;
  }
}
</script>

<template>
  <article
    :id="`post-${postId}`"
    class="game-post"
    :class="{ featured: hasNavigation }"
  >
    <!-- Navigation breadcrumb (featured post only). `post.room.game`
         is a full sidebar-tier GameRef so GameLink / RoomLink render
         the same tooltip UX as the sidebar without a second fetch.
         Plain link colors (no green/gray status tinting) — the sidebar
         coloring is a sidebar affordance, not a post one. -->
    <div v-if="hasNavigation" class="post-nav">
      <GameLink :game="post.room!.game!" />
      <span class="nav-separator" aria-hidden="true"> > </span>
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
                <router-link
                  v-if="canLinkCharacter"
                  class="character-name"
                  :to="{
                    name: 'game-characters',
                    params: {
                      id: post.room?.game?.publicId || post.room?.game?.id,
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
                  :aria-label="`Рейтинг ${ratingText}, показать отзывы`"
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
              <label class="edit-label">Игровой текст</label>
              <BBCodeEditor
                v-model="editGameText"
                context="post"
                placeholder="Игровой текст поста..."
                :min-height="120"
                :is-moderator="isModerator"
              />
              <label class="edit-label">Метаигровой текст</label>
              <BBCodeEditor
                v-model="editMetaText"
                context="post"
                placeholder="Метаигровой комментарий (необязательно)..."
                :min-height="60"
                :max-height="200"
                :is-moderator="isModerator"
              />
              <div class="edit-actions">
                <button
                  class="edit-btn save"
                  :disabled="savingPost || !editGameText.trim()"
                  @click="saveEditPost"
                >
                  Сохранить
                </button>
                <button class="edit-btn" @click="cancelEditPost">
                  Отменить
                </button>
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
                  v-for="roll in post.diceRolls"
                  :key="roll.id"
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
            v-if="!isEditingPost && (canEditPost || canDeletePost || isEdited)"
            class="post-controls"
          >
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
            <router-link
              class="post-link"
              :to="{
                name: 'game-room',
                params: { id: gameId, num: roomNumber },
                hash: postAnchor,
              }"
              >{{ symbols.returnArrow }}</router-link
            >
          </Tooltip>
          <a
            v-else-if="number"
            class="post-number"
            :href="postAnchor"
            @click.prevent="copyAnchorLink"
            >{{ number }}</a
          >
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
          Загрузка…
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
            :highlight="
              !!highlightUsername &&
              review.author?.username === highlightUsername
            "
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
                @click="newReviewSign = -1"
              >
                −
              </button>
            </div>
            <div class="review-input-col">
              <textarea
                v-model="newReviewText"
                class="review-input"
                placeholder="Текст отзыва…"
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
@import "@/assets/styles/Inputs"
@import "@/assets/styles/Animations"

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

.post-number
  color: $text-muted
  font-size: $tertiary-font-size
  text-decoration: none
  &:hover
    color: $link

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

.reviews-section
  list-style: disc
  margin: 0
  padding-top: $small
  padding-left: 25px

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
</style>
