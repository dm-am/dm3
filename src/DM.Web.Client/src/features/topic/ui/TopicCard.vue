<script setup lang="ts">
/**
 * TopicCard — the presentational "topic bubble" card.
 *
 * Single source of the topic-card markup and styles, shared by:
 *   - TopicView.vue (forum topics: full navigation + viewer-dependent actions)
 *   - PublicationCard.vue, which reaches it through the @x/publication door
 *     (a blog publication rendered in the exact same visual shell, with
 *     non-applicable navigation and actions degraded to plain text / static
 *     indicators). That reuse is temporary and PublicationCard owns it: when
 *     the publication gets a design of its own the change happens there, and
 *     this card stays a forum concern.
 *
 * The component takes only display-ready values (no domain DTOs) because
 * the forum Topic type is built from Served<...> branded fields that cannot
 * be fabricated client-side — mapping a Publication into a Topic would
 * require unsafe casts, while this card accepts both domains structurally.
 */
import { computed } from "vue";
import type { RouteLocationRaw } from "vue-router";
import type { User, Username, UserRole } from "@/shared/api/models/community";
import { getRoleBadge } from "@/shared/config/roles";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import dayjs from "dayjs";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { SvgIcon } from "@/shared/ui/Icon";
import {
  initBbcodeInteractive,
  cleanupBbcodeInteractive,
  trimHtmlWhitespace,
} from "@/shared/lib/utils/bbcodeInteractive";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";
import { getLikesTooltip } from "@/shared/lib/utils/chat";

/** Minimal author shape the footer renders: profile link + role badge +
 * online indicator. Both User and UserRef satisfy it structurally. */
type CardAuthor = {
  username: Username;
  role: UserRole;
  lastActivityUtc: string | null;
};

const props = withDefaults(
  defineProps<{
    /** Card title text. Omit when the owning page already renders the
     * entity name as its own section heading — a second in-card heading
     * would be a visible duplicate. */
    title?: string;
    /** Title link target; when null the title renders as plain text
     * (entity has no standalone page route, or the card already sits on
     * that page — topic page). */
    titleTo?: RouteLocationRaw | null;
    /** Server-rendered BBCode HTML of the card body. */
    contentHtml: string;
    /** Content author; null renders the "[удален]" placeholder. */
    author?: CardAuthor | null;
    /** Creation/publication moment (UTC ISO). */
    createdUtc?: string | null;
    /** Last edit moment (UTC ISO); renders the "Отредактировано" suffix. */
    modifiedUtc?: string | null;
    /** Total comments count shown in the footer. */
    commentsCount?: number;
    /** Comments link target; when null the count renders as plain text. */
    commentsTo?: RouteLocationRaw | null;
    /** Unread comments count (authenticated forum view only). */
    unreadCommentsCount?: number;
    /** Unread-comments deep link; null disables the unread affordance. */
    unreadTo?: RouteLocationRaw | null;
    /** Users who liked the content (usernames drive the tooltip). */
    likes?: Array<{ username: Username }>;
    /** Whether the like button is interactive for the current viewer. */
    canLike?: boolean;
    /** Whether the current viewer already liked the content. */
    isLikedByMe?: boolean;
    /** Whether the moderator warn action is available. */
    canWarn?: boolean;
    /** Topic is closed — renders the lock + "Топик закрыт" badge (doc 4.2.2.17). */
    isClosed?: boolean;
    /** Author/moderator lifecycle affordances (forum topic only). */
    canEdit?: boolean;
    canDelete?: boolean;
    /** Moderator close/open toggle. */
    canClose?: boolean;
    /** Enable content truncation (for embedded/list contexts) */
    truncatable?: boolean;
    /** Max height before truncation (px) */
    maxHeight?: number;
    /** Heading level for the title — "h1" when the card owns the page's
     * main heading, "h3" (default) when embedded in a list. */
    headingLevel?: "h1" | "h2" | "h3";
    /** Preview-only card: suppresses the interactive footer actions
     * (like + warn). Used by the home news list, which shows topics as a
     * plain read-only preview. */
    previewOnly?: boolean;
  }>(),
  {
    title: undefined,
    titleTo: null,
    author: null,
    createdUtc: null,
    modifiedUtc: null,
    commentsCount: 0,
    commentsTo: null,
    unreadCommentsCount: 0,
    unreadTo: null,
    likes: () => [],
    canLike: false,
    isLikedByMe: false,
    canWarn: false,
    isClosed: false,
    canEdit: false,
    canDelete: false,
    canClose: false,
    truncatable: false,
    maxHeight: 150,
    headingLevel: "h3",
    previewOnly: false,
  },
);

const emit = defineEmits<{
  toggleLike: [];
  warn: [];
  edit: [];
  delete: [];
  toggleClose: [];
}>();

// getLikesTooltip only reads usernames; its declared User[] parameter is
// wider than what it consumes, hence the cast from the structural subset.
const likersTooltip = computed(() => getLikesTooltip(props.likes as User[]));

const formattedDate = computed(() =>
  props.createdUtc ? formatDateFull(props.createdUtc) : "",
);

const formattedEditDate = computed(() =>
  props.modifiedUtc ? formatDateFull(props.modifiedUtc) : "",
);

const isEdited = computed(() => !!props.modifiedUtc);

const likesCount = computed(() => props.likes.length);

const isAuthorOnline = computed(() => {
  const lastActivityUtc = props.author?.lastActivityUtc;
  if (!lastActivityUtc) return false;
  return (
    dayjs().diff(dayjs(lastActivityUtc), "minute", true) <=
    ONLINE_THRESHOLD_MINUTES
  );
});

const roleBadge = computed(() => getRoleBadge(props.author?.role));

// Card body, pre-trimmed of leading/trailing whitespace. Pure transform —
// TruncatedContent never mutates slot DOM.
const cardContentHtml = computed(() => trimHtmlWhitespace(props.contentHtml));

// Truncation is delegated to <TruncatedContent>. BBCode interactive elements
// (spoilers, NSFW toggles) need to be (re)initialized whenever the content
// DOM is mounted or replaced — the component forwards its inner content
// element here via the onContentMounted callback.
function initCardBbcode(el: HTMLElement) {
  cleanupBbcodeInteractive(el);
  initBbcodeInteractive(el);
}
</script>

<template>
  <div class="topic">
    <!-- Title -->
    <component :is="headingLevel" v-if="title" class="topic-title">
      <router-link v-if="titleTo" :to="titleTo">
        {{ title }}
      </router-link>
      <template v-else>{{ title }}</template>
      <template v-if="isClosed"
        >{{ " "
        }}<span class="closed-badge">
          <SvgIcon name="locked" class="closed-icon" />Топик закрыт</span
        ></template
      >
      <!-- Optional owner-injected title tail (e.g. the digest topics'
           all-statistics link). -->
      <slot name="title-extra" />
    </component>

    <!-- Standalone closed badge when the card renders without a title. -->
    <div v-else-if="isClosed" class="closed-badge closed-badge-standalone">
      <SvgIcon name="locked" class="closed-icon" />Топик закрыт
    </div>

    <!-- Content. Skipped entirely for topics with no text (legal per the
         backend contract — only the title is required; e.g. the period
         digests, whose content is the injected leaderboards below). -->
    <div v-if="cardContentHtml" class="topic-description">
      <TruncatedContent
        :truncatable="truncatable"
        :max-height="maxHeight"
        :watch-key="cardContentHtml"
        :on-content-mounted="initCardBbcode"
      >
        <div class="topic-text bbcode-content" v-html="cardContentHtml" />
      </TruncatedContent>
    </div>

    <!-- Structured content the owner injects below the text (e.g. the
         period-digest leaderboards inside "Итоги …" topics). -->
    <slot name="after-content" />

    <!-- Footer: Author info + Actions -->
    <div class="topic-footer">
      <span class="author-info"
        >Автор:
        <router-link
          v-if="author"
          :to="{
            name: 'profile',
            params: { username: author.username },
          }"
          class="author-link"
          >{{ author.username }}</router-link
        ><span v-else class="author-deleted">[удален]</span
        ><template v-if="roleBadge">
          [<Tooltip :text="roleBadge.label"
            ><b class="role-letter">{{ roleBadge.letter }}</b></Tooltip
          >]</template
        ><template v-if="author">
          [<span :class="isAuthorOnline ? 'online' : 'offline'">{{
            isAuthorOnline ? "online" : "offline"
          }}</span
          >]</template
        >, {{ formattedDate
        }}<template v-if="isEdited">
          | Отредактировано {{ formattedEditDate }}</template
        >
        <!-- Product decision (2026-07-11): the card counter is the viewer's
             UNREAD count — including an honest 0 when everything is read.
             Guests have no read tracking, so for them (unreadTo is null)
             everything is unread and the counter shows the total. The
             "total (unread)" pair is a table convention, not a card one.
             With 0 unread the ?unread=1 deep link is pointless, so the
             number links to the plain comments target instead. -->
        | Комментарии (<router-link
          v-if="unreadTo && unreadCommentsCount"
          :to="unreadTo"
          class="unread-link"
          >{{ unreadCommentsCount }}</router-link
        ><router-link
          v-else-if="commentsTo"
          :to="commentsTo"
          class="comments-link"
          >{{ unreadTo ? unreadCommentsCount : commentsCount }}</router-link
        ><span v-else>{{ unreadTo ? unreadCommentsCount : commentsCount }}</span
        >)</span
      >

      <!-- Actions (suppressed for preview-only cards, e.g. home news list) -->
      <span v-if="!previewOnly" class="topic-actions">
        <span v-if="canLike || likesCount > 0" class="likes-container">
          <!-- The likers list is viewer-independent information: the
               interactive button carries the same tooltip as the static
               badge (suppressed while there is nobody to list). -->
          <Tooltip
            v-if="canLike"
            :text="likersTooltip"
            :disabled="likesCount === 0"
          >
            <button
              class="like-btn"
              :class="{ liked: isLikedByMe }"
              :aria-label="
                likesCount > 0 ? `Нравится: ${likesCount}` : 'Нравится'
              "
              @click="emit('toggleLike')"
            >
              <SvgIcon
                :name="isLikedByMe ? 'heartFilled' : 'heartEmpty'"
                class="like-icon"
              />
              <span v-if="likesCount > 0" class="likes-count">{{
                likesCount
              }}</span>
            </button>
          </Tooltip>
          <Tooltip
            v-else
            :text="likersTooltip"
            focusable
            class="like-static"
            :aria-label="`Нравится: ${likesCount}`"
          >
            <SvgIcon
              :name="isLikedByMe ? 'heartFilled' : 'heartEmpty'"
              class="like-icon"
            />
            <span class="likes-count">{{ likesCount }}</span>
          </Tooltip>
        </span>
        <button v-if="canEdit" class="action-btn" @click="emit('edit')">
          Редактировать
        </button>
        <button v-if="canClose" class="action-btn" @click="emit('toggleClose')">
          {{ isClosed ? "Открыть" : "Закрыть" }}
        </button>
        <button
          v-if="canDelete"
          class="action-btn delete-btn"
          @click="emit('delete')"
        >
          Удалить
        </button>
        <button
          v-if="canWarn"
          class="action-btn warn-btn"
          @click="emit('warn')"
        >
          Предупреждение
        </button>
      </span>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/BbcodeContent"

.topic
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

.topic-title
  margin: 0 0 $small 0
  padding-bottom: $small
  border-bottom: 1px dashed $border
  font-size: $font-size
  font-weight: bold

  a
    color: $link
    // No local text-decoration override — the global a:hover rule in
    // Reset.sass provides the underline on hover.

    &:hover
      color: $link-hover

.topic-description
  word-wrap: break-word
  word-break: break-word
  overflow-wrap: break-word
  color: $text
  line-height: 1.6

// .topic-text uses the global .bbcode-content class for typography.
// Truncation & expand-link are owned by <TruncatedContent>.

.topic-footer
  display: flex
  align-items: baseline
  flex-wrap: wrap
  gap: $small
  margin-top: $small
  font-size: $tertiary-font-size
  color: $text-muted

.author-info
  color: $text-muted

.author-link
  color: $link

  &:hover
    color: $link-hover

.author-deleted
  font-style: italic

.role-letter
  font-weight: bold
  color: $accent-green
  cursor: help

// Online status is SELECTABLE bracketed text ("[online]" / "[offline]"),
// the original-site idiom — never a status dot (owner rule; per COMM-6
// the label stays English, not Russian).
.online
  color: $accent-green

.offline
  color: $text-muted

.comments-link
  color: $link
  &:hover
    color: $link-hover

.unread-link
  color: $link
  &:hover
    color: $link-hover

.topic-actions
  display: inline-flex
  align-items: center
  gap: $small
  margin-left: $small

.likes-container
  position: relative
  display: inline-flex

.like-btn
  display: inline-flex
  align-items: center
  gap: 2px
  padding: 0 $tiny
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted
  font-size: $secondary-font-size

  &:hover
    color: $accent-red

  &.liked
    color: $accent-red

// Non-interactive like indicator (own content / guest / preview): shows the
// count without a clickable affordance, matching CommentItem.vue's .like-static.
.like-static
  display: inline-flex
  align-items: center
  gap: 2px
  padding: 0 $tiny
  color: $text-muted
  font-size: $secondary-font-size
  cursor: help

.likes-count
  font-weight: bold

.action-btn
  padding: 0 $tiny
  font-size: $secondary-font-size
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted

  &:hover
    color: $link

  &.warn-btn:hover
    color: $accent-red

  &.delete-btn:hover
    color: $accent-red

// Closed-topic badge (lock + "Топик закрыт"), sits next to the heading. The gap
// in front of it is the " " text node in the title, not a margin: a margin is
// drawn but not copied, and the title used to reach the clipboard glued.
.closed-badge
  display: inline-flex
  align-items: center
  gap: $tiny
  color: $accent-red
  font-size: $secondary-font-size
  font-weight: normal
  vertical-align: middle

.closed-badge-standalone
  margin-bottom: $small

.closed-icon
  width: 0.9em
  height: 0.9em
</style>
