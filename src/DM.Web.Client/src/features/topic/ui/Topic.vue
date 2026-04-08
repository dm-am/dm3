<script setup lang="ts">
import { ref, computed } from "vue";
import { storeToRefs } from "pinia";
import type { Topic } from "@/entities/forum";
import { useUserStore } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { UserRole } from "@/shared/api/models/community";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useContentTruncation } from "@/shared/lib/composables";
import dayjs from "dayjs";
import defaultPicture from "@/assets/images/userpic.png";
import {
  initBbcodeInteractive,
  cleanupBbcodeInteractive,
} from "@/shared/lib/utils/bbcodeInteractive";

const props = withDefaults(
  defineProps<{
    topic: Topic;
    /** Enable content truncation (for news list) */
    truncatable?: boolean;
    /** Max height before truncation (px) */
    maxHeight?: number;
  }>(),
  {
    truncatable: false,
    maxHeight: 150,
  },
);

const emit = defineEmits<{
  like: [id: string];
  unlike: [id: string];
  warn: [id: string];
}>();

const ONLINE_THRESHOLD_MINUTES = 5;

const { user: currentUser } = storeToRefs(useUserStore());
const { isCompactMode } = storeToRefs(useUiStore());

const showLikesPopup = ref(false);

// Computed
const authorPicture = computed(
  () => props.topic.author?.picture?.mediumUrl || props.topic.author?.picture?.smallUrl || defaultPicture,
);

const formattedDate = computed(() => {
  if (!props.topic.createdUtc) return "";
  return dayjs(props.topic.createdUtc).format("DD.MM.YYYY HH:mm");
});

const formattedEditDate = computed(() => {
  if (!props.topic.modifiedUtc) return "";
  return dayjs(props.topic.modifiedUtc).format("DD.MM.YYYY в HH:mm");
});

const isEdited = computed(() => !!props.topic.modifiedUtc);

const isModerator = computed(() => {
  if (!currentUser.value) return false;
  return (currentUser.value.roles ?? []).some((r) =>
    [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(r),
  );
});

const isAuthor = computed(
  () => currentUser.value?.username === props.topic.author?.username,
);

const isLikedByMe = computed(() => {
  if (!currentUser.value) return false;
  return props.topic.likes?.some(
    (u) => u.username === currentUser.value?.username,
  );
});

const canLike = computed(() => {
  if (!currentUser.value) return false;
  return !isAuthor.value;
});

const canWarn = computed(() => isModerator.value);

const likesCount = computed(() => props.topic.likes?.length ?? 0);

const isAuthorOnline = computed(() => {
  const lastActivityUtc = props.topic.author?.lastActivityUtc;
  if (!lastActivityUtc) return false;
  return dayjs().diff(dayjs(lastActivityUtc), "minute", true) <= ONLINE_THRESHOLD_MINUTES;
});

const roleBadge = computed(() => {
  switch (props.topic.author?.role) {
    case UserRole.Admin:
      return { label: "A", title: "Администратор" };
    case UserRole.SeniorModerator:
      return { label: "С", title: "Старший модератор" };
    case UserRole.Moderator:
      return { label: "М", title: "Модератор" };
    case UserRole.Mentor:
      return { label: "Н", title: "Наставник" };
    case UserRole.System:
      return { label: "Р", title: "Робот-администратор" };
    default:
      return null;
  }
});

// Content description ref for BBCode initialization
const topicDescription = computed(() => props.topic.description);

// Truncation (unified composable)
const {
  setContentRef,
  contentStyle,
  needsTruncation,
  isExpanded,
  toggleExpand,
  contentRef,
} = useContentTruncation({
  maxHeight: props.maxHeight,
  enabled: computed(() => props.truncatable),
  watchContent: topicDescription,
  onContentMounted: (el) => {
    cleanupBbcodeInteractive(el);
    initBbcodeInteractive(el);
  },
});

// Methods
function toggleLike() {
  if (isLikedByMe.value) {
    emit("unlike", props.topic.id);
  } else {
    emit("like", props.topic.id);
  }
}

function handleWarn() {
  emit("warn", props.topic.id);
}
</script>

<template>
  <div class="topic" :class="{ compact: isCompactMode }">
    <!-- Title -->
    <h3 class="topic-title">
      <router-link :to="{ name: 'topic', params: { alias: topic.board?.alias, num: topic.topicNumber } }">
        {{ topic.title }}
      </router-link>
    </h3>

    <div class="topic-content">
      <!-- Avatar (non-compact only) -->
      <router-link
        v-if="!isCompactMode && topic.author"
        :to="{ name: 'profile', params: { username: topic.author.username } }"
        class="avatar-link"
      >
        <img :src="authorPicture" :alt="topic.author.username" class="avatar" />
      </router-link>

      <div class="topic-body">
        <!-- Description -->
        <div class="topic-description">
          <div
            :ref="setContentRef"
            class="topic-text bbcode-content"
            :class="{ truncatable: needsTruncation }"
            :style="contentStyle"
            v-html="topic.description"
          />
          <a
            v-if="needsTruncation && !isExpanded"
            class="expand-link"
            @click="toggleExpand"
          >... <strong>показать полностью</strong></a>
        </div>

        <!-- Footer: Author info + Actions -->
        <div class="topic-footer">
          <span v-if="topic.author" class="author-info"
            >Автор: <router-link
              :to="{ name: 'profile', params: { username: topic.author.username } }"
              class="author-link"
            >{{ topic.author.username }}</router-link
            ><template v-if="roleBadge"
              > [<Tooltip :text="roleBadge.title"><b class="role-letter">{{ roleBadge.label }}</b></Tooltip>]</template
            > [<span :class="isAuthorOnline ? 'online' : 'offline'">{{ isAuthorOnline ? "online" : "offline" }}</span
            >], {{ formattedDate }}<template v-if="isEdited"
              > | Отредактировано {{ formattedEditDate }}</template
            > | Комментарии: <router-link
              :to="{ name: 'topic', params: { alias: topic.board?.alias, num: topic.topicNumber } }"
              class="comments-link"
            >{{ topic.commentsCount }}</router-link
            ><template v-if="topic.unreadCommentsCount"
              > (<router-link
                :to="{ name: 'topic', params: { alias: topic.board?.alias, num: topic.topicNumber }, query: { unread: 1 } }"
                class="unread-link"
              >{{ topic.unreadCommentsCount }}</router-link
            >)</template
          ></span>

          <!-- Actions -->
          <span class="topic-actions">
            <span
              v-if="canLike || likesCount > 0"
              class="likes-container"
              @mouseenter="showLikesPopup = true"
              @mouseleave="showLikesPopup = false"
            >
              <button
                class="like-btn"
                :class="{ liked: isLikedByMe }"
                :disabled="!canLike"
                @click="toggleLike"
              >
                <span class="like-icon">&#9829;</span>
                <span v-if="likesCount > 0" class="likes-count">{{ likesCount }}</span>
              </button>
              <div v-if="showLikesPopup && likesCount > 0" class="likes-popup">
                <div v-for="liker in topic.likes" :key="liker.username" class="liker">
                  {{ liker.username }}
                </div>
              </div>
            </span>
            <button v-if="canWarn" class="action-btn warn-btn" @click="handleWarn">
              пред.
            </button>
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"
@import "src/assets/styles/ZIndex"

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
    text-decoration: none

    &:hover
      color: $link-hover

.topic-content
  display: flex
  gap: $medium

.topic.compact .topic-content
  display: block

.avatar-link
  flex-shrink: 0

.avatar
  width: 64px
  height: 64px
  border-radius: 50%
  object-fit: cover

.topic-body
  flex: 1
  min-width: 0

.topic-description
  word-wrap: break-word
  word-break: break-word
  overflow-wrap: break-word
  color: $text
  line-height: 1.6

// .topic-text uses global .bbcode-content class
.topic-text
  &.truncatable
    overflow: hidden
    transition: max-height 0.4s ease

.expand-link
  display: inline-block
  margin-top: $tiny
  color: $link
  cursor: pointer

  &:hover
    color: $link-hover

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
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

.role-letter
  font-weight: bold
  color: $accent-green
  cursor: help

.online
  color: $accent-green

.offline
  color: $text-muted

.comments-link
  color: $link
  text-decoration: none
  &:hover
    color: $link-hover

.unread-link
  color: $link
  text-decoration: none
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

  &:hover:not(:disabled)
    color: $accent-red

  &.liked
    color: $accent-red

  &:disabled
    cursor: default
    opacity: 0.6

.likes-count
  font-weight: bold

.likes-popup
  position: absolute
  bottom: 100%
  left: 0
  padding: $small
  border-radius: $border-radius
  z-index: $z-dropdown
  min-width: $grid-step * 30
  background-color: $bg-highlight-blue
  border: 1px solid $border
  box-shadow: 0 2px 8px var(--shadow-color)

.liker
  padding: $tiny 0
  color: $text
  white-space: nowrap

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
</style>
