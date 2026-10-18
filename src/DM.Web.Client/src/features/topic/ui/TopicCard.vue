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
import { getActivePinia } from "pinia";
import type { RouteLocationRaw } from "vue-router";
import type { Username, UserRole } from "@/shared/api/models/community";
import { getRoleBadge } from "@/shared/config/roles";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import dayjs from "dayjs";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { SvgIcon } from "@/shared/ui/Icon";
import {
  CaptionLine,
  CaptionPart,
  CaptionAction,
} from "@/shared/ui/CaptionLine";
import { LikeBadge } from "@/shared/ui/LikeBadge";
import {
  initBbcodeInteractive,
  cleanupBbcodeInteractive,
  trimHtmlWhitespace,
} from "@/shared/lib/utils/bbcodeInteractive";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";
import { RemovedAccountLink } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";

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
    /** Content author; null renders the "Аккаунт удален" placeholder. */
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
    /** Where the viewer continues reading; null (a guest) falls back to the
     * plain comments link. Independent of the unread count on purpose: with
     * nothing unread this target answers with the topic's last comment. */
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
    /** Edit page of the content: "Редактировать" is a link there instead
     * of the `edit` event (a publication is edited on a page of its own). */
    editTo?: RouteLocationRaw | null;
    canDelete?: boolean;
    /** Enable content truncation (for embedded/list contexts) */
    truncatable?: boolean;
    /** Max height before truncation (px) */
    maxHeight?: number;
    /** Heading level for the title — "h1" when the card owns the page's
     * main heading, "h3" (default) when embedded in a list. */
    headingLevel?: "h1" | "h2" | "h3";
    /** Preview-only card: suppresses the footer actions and the like.
     * Used by the home news list, which shows topics as a plain read-only
     * preview. */
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
    editTo: null,
    canDelete: false,
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
}>();

const formattedDate = computed(() =>
  props.createdUtc ? formatDateFull(props.createdUtc) : "",
);

const formattedEditDate = computed(() =>
  props.modifiedUtc ? formatDateFull(props.modifiedUtc) : "",
);

const isEdited = computed(() => !!props.modifiedUtc);

// Nothing to press and nobody to list: a heart would stand for nothing, so
// the author's own unliked content and a guest's view of it show none.
const showLike = computed(() => props.canLike || props.likes.length > 0);

const isAuthorOnline = computed(() => {
  const lastActivityUtc = props.author?.lastActivityUtc;
  if (!lastActivityUtc) return false;
  return (
    dayjs().diff(dayjs(lastActivityUtc), "minute", true) <=
    ONLINE_THRESHOLD_MINUTES
  );
});

const roleBadge = computed(() => getRoleBadge(props.author?.role));

// "Перенос слов" of the settings panel, as over the text of a comment. The
// switch lives in the store the app installs; a card drawn where none is
// installed, a card on its own, keeps the switch's default, on.
const pinia = getActivePinia();
const ui = pinia ? useUiStore(pinia) : null;
const typesetting = computed(() => ({
  hyphenated: ui ? ui.hyphenateWords : true,
}));

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
        <div
          class="topic-text bbcode-content"
          :class="typesetting"
          v-html="cardContentHtml"
        />
      </TruncatedContent>
    </div>

    <!-- Structured content the owner injects below the text (e.g. the
         period-digest leaderboards inside "Итоги …" topics). -->
    <slot name="after-content" />

    <!-- Footer: one caption line, who and when, then what the viewer may do,
         the like last of all. It wraps between parts at every width, as it
         did on a desktop before 5 October: at 1366 a moderator's caption may
         wrap, the like or the actions too going down by the length of the
         name, and a phone breaks after the comma and keeps the date whole. Nothing of it is put away for want of room (owner,
         12 October 2026). -->
    <div class="topic-footer">
      <CaptionLine>
        <CaptionPart :joint="createdUtc ? ',' : undefined"
          >Автор:
          <router-link
            v-if="author"
            :to="{
              name: 'profile',
              params: { username: author.username },
            }"
            class="author-link"
            >{{ author.username }}</router-link
          ><RemovedAccountLink v-else /><template v-if="roleBadge">
            [<Tooltip :text="roleBadge.label"
              ><b class="role-letter">{{ roleBadge.letter }}</b></Tooltip
            >]</template
          ><template v-if="author">
            [<span :class="isAuthorOnline ? 'online' : 'offline'">{{
              isAuthorOnline ? "online" : "offline"
            }}</span
            >]</template
          ></CaptionPart
        >
        <span v-if="createdUtc">{{ formattedDate }}</span>
        <span v-if="isEdited">Отредактировано {{ formattedEditDate }}</span>
        <!-- Product decision (2026-07-11): the card counter is the viewer's
             UNREAD count, including an honest 0 when everything is read.
             Guests have no read tracking, so for them (unreadTo is null)
             everything is unread and the counter shows the total. The
             "total (unread)" pair is a table convention, not a card one.
             The TARGET does not depend on that number. It used to: at 0 unread
             the link fell back to the top of the topic, and the counter is
             zeroed the moment the topic opens, so the second click on the same
             link always landed at the beginning. The resolver answers with the
             last comment instead. -->
        <span
          >Комментарии (<router-link
            v-if="unreadTo"
            :to="unreadTo"
            class="comments-link"
            >{{ unreadCommentsCount }}</router-link
          ><router-link
            v-else-if="commentsTo"
            :to="commentsTo"
            class="comments-link"
            >{{ commentsCount }}</router-link
          ><span v-else>{{ commentsCount }}</span
          >)</span
        >
        <!-- Actions and the like (none on a preview card, the home news). -->
        <template v-if="!previewOnly">
          <CaptionAction v-if="editTo" :to="editTo"
            >Редактировать</CaptionAction
          >
          <CaptionAction v-else-if="canEdit" @click="emit('edit')"
            >Редактировать</CaptionAction
          >
          <!-- No "Закрыть" here: whether a topic is closed is a setting of
               the topic, changed where the rest of it is, in the edit form. -->
          <CaptionAction v-if="canDelete" @click="emit('delete')"
            >Удалить</CaptionAction
          >
          <CaptionAction v-if="canWarn" @click="emit('warn')"
            >Предупреждение</CaptionAction
          >
          <LikeBadge
            v-if="showLike"
            :likes="likes"
            :liked="isLikedByMe"
            :interactive="canLike"
            @toggle="emit('toggleLike')"
          />
        </template>
      </CaptionLine>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *

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

// The block of the text and of its "показать полностью" on the step of the text.
.topic-description
  word-wrap: break-word
  word-break: break-word
  overflow-wrap: break-word
  color: $text
  line-height: 1.25

// The text of a topic or a publication stands as the text of a comment
// (CommentItem.vue, .comment-text), DM2 showing the topic as its comment No 1:
// 16px on a step of 20, flush on both sides at every width, and hyphenated
// while its switch is on.
.topic-text
  +body-text

// .topic-text uses the global .bbcode-content class for typography.
// Truncation & expand-link are owned by <TruncatedContent>.

// The caption line owns its size and colour; the footer repeats the size so
// the line it stands on is the caption's height and not the card text's.
.topic-footer
  margin-top: $small
  font-size: $tertiary-font-size

.author-link
  color: $link

  &:hover
    color: $link-hover

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
