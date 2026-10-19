<script setup lang="ts">
// Display-ready values only: a forum Topic's branded fields cannot be built for a publication.
import { computed } from "vue";
import { getActivePinia } from "pinia";
import type { RouteLocationRaw } from "vue-router";
import type { Username, UserRole } from "@/shared/api/models/community";
import { getRoleBadge } from "@/shared/config/roles";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { SvgIcon } from "@/shared/ui/Icon";
import {
  CaptionLine,
  CaptionPart,
  CaptionAction,
} from "@/shared/ui/CaptionLine";
import { LikeBadge } from "@/shared/ui/LikeBadge";
import { vBbcodeInteractive } from "@/shared/directives";
import { trimHtmlWhitespace } from "@/shared/lib/utils/trimHtmlWhitespace";
import { isUserOnline } from "@/shared/lib/utils/chat";
import { RemovedAccountLink, UserRoleBadge } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";

type CardAuthor = {
  username: Username;
  role: UserRole;
  lastActivityUtc: string | null;
};

const props = withDefaults(
  defineProps<{
    /** Omitted when the page already heads the section with the same name. */
    title?: string;
    /** Null renders the title as plain text. */
    titleTo?: RouteLocationRaw | null;
    contentHtml: string;
    /** Null renders the "Аккаунт удален" placeholder. */
    author?: CardAuthor | null;
    createdUtc?: string | null;
    modifiedUtc?: string | null;
    commentsCount?: number;
    commentsTo?: RouteLocationRaw | null;
    unreadCommentsCount?: number;
    /** Null for a guest; independent of the unread count, which may be 0. */
    unreadTo?: RouteLocationRaw | null;
    likes?: Array<{ username: Username }>;
    canLike?: boolean;
    isLikedByMe?: boolean;
    canWarn?: boolean;
    isClosed?: boolean;
    canEdit?: boolean;
    /** A publication is edited on a page of its own, so this is a link. */
    editTo?: RouteLocationRaw | null;
    canDelete?: boolean;
    truncatable?: boolean;
    maxHeight?: number;
    headingLevel?: "h1" | "h2" | "h3";
    /** Read-only preview of the home news: no actions and no like. */
    previewOnly?: boolean;
  }>(),
  {
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

// "Нравится" with nothing to press and nobody to list would stand for nothing.
const showLike = computed(() => props.canLike || props.likes.length > 0);

const isAuthorOnline = computed(() =>
  isUserOnline(props.author?.lastActivityUtc),
);

const roleBadge = computed(() => getRoleBadge(props.author?.role));

// A card drawn where no store is installed keeps the switch's default, on.
const pinia = getActivePinia();
const ui = pinia ? useUiStore(pinia) : null;
const typesetting = computed(() => ({
  hyphenated: ui ? ui.hyphenateWords : true,
}));

const cardContentHtml = computed(() => trimHtmlWhitespace(props.contentHtml));
</script>

<template>
  <div class="topic">
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
      <slot name="title-extra" />
    </component>

    <div v-else-if="isClosed" class="closed-badge closed-badge-standalone">
      <SvgIcon name="locked" class="closed-icon" />Топик закрыт
    </div>

    <!-- Skipped for topics with no text: only the title is required. -->
    <div v-if="cardContentHtml" class="topic-description">
      <TruncatedContent
        :truncatable="truncatable"
        :max-height="maxHeight"
        :watch-key="cardContentHtml"
      >
        <div
          v-bbcode-interactive="cardContentHtml"
          class="topic-text bbcode-content"
          :class="typesetting"
          v-html="cardContentHtml"
        />
      </TruncatedContent>
    </div>

    <slot name="after-content" />

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
          ><RemovedAccountLink v-else /><UserRoleBadge
            v-if="roleBadge"
            :badge="roleBadge"
            variant="caption"
          /><template v-if="author">
            [<span :class="isAuthorOnline ? 'online' : 'offline'">{{
              isAuthorOnline ? "online" : "offline"
            }}</span
            >]</template
          ></CaptionPart
        >
        <span v-if="createdUtc">{{ formattedDate }}</span>
        <span v-if="isEdited">Отредактировано {{ formattedEditDate }}</span>
        <!-- The viewer's unread count, an honest 0 included; a guest sees the total. -->
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
        <template v-if="!previewOnly">
          <CaptionAction v-if="editTo" :to="editTo"
            >Редактировать</CaptionAction
          >
          <CaptionAction v-else-if="canEdit" @click="emit('edit')"
            >Редактировать</CaptionAction
          >
          <!-- No "Закрыть": closing is a setting of the topic, in its edit form. -->
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

    &:hover
      color: $link-hover

.topic-description
  overflow-wrap: anywhere
  color: $text
  line-height: 1.25

.topic-text
  +body-text


// The footer repeats the size so its line is the caption's height.
.topic-footer
  margin-top: $small
  font-size: $tertiary-font-size

.author-link
  color: $link

  &:hover
    color: $link-hover

// Selectable bracketed text, never a status dot.
.online
  color: $accent-green

.offline
  color: $text-muted

.comments-link
  color: $link
  &:hover
    color: $link-hover

// The gap before it is a " " text node, not a margin: a margin is not copied.
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
