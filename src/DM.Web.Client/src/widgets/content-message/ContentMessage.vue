<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from "vue";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/community";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import defaultPicture from "@/assets/images/userpic.png";
import dayjs from "dayjs";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";

export interface MessageData {
  id: string;
  createdUtc: string;
  modifiedUtc?: string | null;
  author: User;
  text: string;
  isRemoved: boolean;
  isHiddenByBlacklist?: boolean;
  likes: User[];
}

const props = withDefaults(
  defineProps<{
    message: MessageData;
    isPublic?: boolean;
    maxHeight?: number;
    compact?: boolean;
  }>(),
  {
    isPublic: true,
    maxHeight: 300,
    compact: false,
  },
);

const emit = defineEmits<{
  edit: [id: string, text: string];
  delete: [id: string];
  like: [id: string];
  unlike: [id: string];
  warn: [id: string];
}>();

const EDIT_TIME_LIMIT_MINUTES = 15;

const { user: currentUser } = storeToRefs(useUserStore());

// State
const isEditing = ref(false);
const editText = ref("");
const isExpanded = ref(false);
const showDeletedContent = ref(false);
const contentRef = ref<HTMLElement | null>(null);
const isOverflowing = ref(false);
const showLikesPopup = ref(false);

// Computed
const authorPicture = computed(
  () => props.message.author?.smallPictureUrl || defaultPicture,
);

const formattedTime = computed(() => {
  if (!props.message.createdUtc) return "";
  return dayjs(props.message.createdUtc).format("HH:mm");
});

const formattedDate = computed(() => {
  if (!props.message.createdUtc) return "";
  return dayjs(props.message.createdUtc).format("DD.MM.YYYY HH:mm");
});

const authorRole = computed(() => props.message.author?.role);

// Role badges - same as Comment.vue
const roleBadge = computed(() => {
  switch (authorRole.value) {
    case UserRole.Admin:
      return { label: "А", title: "Администратор" };
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

const tooltipText = computed(() => {
  if (!props.message.createdUtc) return "";
  let result = `Отправлено: ${dayjs(props.message.createdUtc).format("DD.MM.YYYY HH:mm")}`;
  if (props.message.modifiedUtc) {
    result += `\nОтредактировано: ${dayjs(props.message.modifiedUtc).format("DD.MM.YYYY HH:mm")}`;
  }
  return result;
});

const isEdited = computed(() => !!props.message.modifiedUtc);

const isModerator = computed(() => {
  if (!currentUser.value) return false;
  return (currentUser.value.roles ?? []).some((r) =>
    [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(r),
  );
});

const isAuthor = computed(
  () => currentUser.value?.username === props.message.author?.username,
);

const canEdit = computed(() => {
  if (!currentUser.value) return false;
  if (props.message.isRemoved) return false;
  if (isModerator.value) return true;
  if (!isAuthor.value) return false;

  const timeSinceCreation = dayjs().diff(
    dayjs(props.message.createdUtc),
    "minute",
    true,
  );
  return timeSinceCreation <= EDIT_TIME_LIMIT_MINUTES;
});

const canDelete = computed(() => canEdit.value);

const canWarn = computed(() => isModerator.value && props.isPublic);

const isLikedByMe = computed(() => {
  if (!currentUser.value) return false;
  return props.message.likes?.some(
    (u) => u.username === currentUser.value?.username,
  );
});

const canLike = computed(() => {
  if (!currentUser.value) return false;
  return !isAuthor.value;
});

const likesCount = computed(() => props.message.likes?.length ?? 0);

const messageAnchor = computed(() => `#message-${props.message.id}`);

const ONLINE_THRESHOLD_MINUTES = 5;
const isAuthorOnline = computed(() => {
  const lastActivityUtc = props.message.author?.lastActivityUtc;
  if (!lastActivityUtc) return false;
  const minutesSinceOnline = dayjs().diff(
    dayjs(lastActivityUtc),
    "minute",
    true,
  );
  return minutesSinceOnline <= ONLINE_THRESHOLD_MINUTES;
});

const hasCutTag = computed(() => props.message.text?.includes("[cut]"));

const displayText = computed(() => {
  if (!props.message.text) return "";

  if (hasCutTag.value && !isExpanded.value) {
    const cutIndex = props.message.text.indexOf("[cut]");
    return props.message.text.substring(0, cutIndex);
  }

  return props.message.text;
});

const needsTruncation = computed(() => hasCutTag.value || isOverflowing.value);

// Methods
function startEdit() {
  editText.value = props.message.text || "";
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
}

function saveEdit() {
  if (editText.value.trim()) {
    emit("edit", props.message.id, editText.value);
  }
  isEditing.value = false;
}

function handleEditKeydown(e: KeyboardEvent) {
  if (e.key === "Enter" && e.ctrlKey) {
    e.preventDefault();
    saveEdit();
  } else if (e.key === "Escape") {
    cancelEdit();
  }
}

function toggleLike() {
  if (isLikedByMe.value) {
    emit("unlike", props.message.id);
  } else {
    emit("like", props.message.id);
  }
}

function handleDelete() {
  emit("delete", props.message.id);
}

function handleWarn() {
  emit("warn", props.message.id);
}

function toggleExpand() {
  isExpanded.value = !isExpanded.value;
}

function toggleDeletedContent() {
  showDeletedContent.value = !showDeletedContent.value;
}

function copyAnchorLink() {
  navigator.clipboard.writeText(
    window.location.origin + window.location.pathname + messageAnchor.value,
  );
}

function checkContentHeight() {
  if (contentRef.value && !hasCutTag.value) {
    isOverflowing.value = contentRef.value.scrollHeight > props.maxHeight;
  }
}

onMounted(() => {
  checkContentHeight();
  // Initialize interactive BBCode elements (spoilers, NSFW toggles)
  nextTick(() => {
    initBbcodeInteractive(contentRef.value);
  });
});

watch(
  () => props.message.text,
  () => {
    checkContentHeight();
    // Re-initialize interactive elements when content changes
    nextTick(() => {
      initBbcodeInteractive(contentRef.value);
    });
  },
);
</script>

<template>
  <div
    :id="`message-${message.id}`"
    class="content-message"
    :class="{
      hidden: message.isHiddenByBlacklist,
      removed: message.isRemoved && !showDeletedContent,
      compact,
    }"
  >
    <!-- Hidden by blacklist placeholder -->
    <template v-if="message.isHiddenByBlacklist">
      <div class="hidden-placeholder">
        <span class="hidden-text"
          >Контент от заблокированного пользователя скрыт</span
        >
      </div>
    </template>

    <!-- Deleted message placeholder -->
    <template v-else-if="message.isRemoved && !showDeletedContent">
      <div class="deleted-placeholder">
        <span class="deleted-text">Сообщение удалено</span>
        <button
          v-if="isModerator"
          class="show-deleted-btn"
          @click="toggleDeletedContent"
        >
          Показать
        </button>
      </div>
    </template>

    <!-- Normal message content -->
    <template v-else>
      <!-- Avatar -->
      <router-link
        :to="{ name: 'profile', params: { username: message.author.username } }"
        class="avatar-link"
      >
        <img
          :src="authorPicture"
          :alt="message.author.username"
          class="avatar"
        />
      </router-link>

      <div class="message-body">
        <!-- Header (non-compact mode only) -->
        <div v-if="!compact" class="message-header">
          <router-link
            :to="{
              name: 'profile',
              params: { username: message.author.username },
            }"
            class="author-name"
            :class="{ online: isAuthorOnline }"
          >
            {{ message.author.username }}
          </router-link>
          <span v-if="roleBadge" class="role-badge">
            [<Tooltip :text="roleBadge.title"><b>{{ roleBadge.label }}</b></Tooltip>]
          </span>
          <Tooltip :text="tooltipText">
            <span class="message-time">
              {{ formattedTime
              }}<span v-if="isEdited" class="edited-marker"> (ред.)</span>
            </span>
          </Tooltip>

          <!-- Hide deleted button for moderators -->
          <button
            v-if="message.isRemoved && isModerator"
            class="hide-deleted-btn"
            @click="toggleDeletedContent"
          >
            Скрыть
          </button>
        </div>

        <!-- Content (compact mode) -->
        <div
          v-if="compact && !isEditing"
          ref="contentRef"
          class="message-content bbcode-content"
          :class="{ collapsed: needsTruncation && !isExpanded }"
          :style="{
            maxHeight: needsTruncation && !isExpanded ? `${maxHeight}px` : 'none',
          }"
          v-html="displayText"
        />

        <!-- Expand/collapse button (compact mode) -->
        <button
          v-if="compact && !isEditing && needsTruncation"
          class="expand-btn"
          @click="toggleExpand"
        >
          {{ isExpanded ? "Свернуть" : "Читать далее" }}
        </button>

        <!-- Compact mode footer -->
        <div v-if="compact && !isEditing" class="message-footer">
          <!-- Author info (compact mode) -->
          <span class="author-label">Автор:</span>
          <router-link
            :to="{ name: 'profile', params: { username: message.author.username } }"
            class="author-link"
          >
            {{ message.author.username }}
          </router-link>
          <span v-if="roleBadge" class="role-badge">
            [<Tooltip :text="roleBadge.title"><b>{{ roleBadge.label }}</b></Tooltip>]
          </span>
          <span class="status-badge">
            [<span :class="isAuthorOnline ? 'online' : 'offline'">{{
              isAuthorOnline ? "online" : "offline"
            }}</span>]
          </span>
          <Tooltip :text="tooltipText">
            <span class="message-date">, {{ formattedDate }}<span v-if="isEdited" class="edited-marker"> (ред.)</span></span>
          </Tooltip>

          <!-- Actions -->
          <span class="message-actions">
            <!-- Likes -->
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
                <span class="like-icon">♥</span><span v-if="likesCount > 0" class="likes-count">{{ likesCount }}</span>
              </button>

              <div v-if="showLikesPopup && likesCount > 0" class="likes-popup">
                <div
                  v-for="liker in message.likes"
                  :key="liker.username"
                  class="liker"
                >
                  {{ liker.username }}
                </div>
              </div>
            </span>

            <button v-if="canEdit" class="action-btn" @click="startEdit">ред.</button>
            <button v-if="canDelete" class="action-btn delete-btn" @click="handleDelete">удл.</button>
            <button v-if="canWarn" class="action-btn warn-btn" @click="handleWarn">пред.</button>
          </span>

          <!-- Hide deleted button (compact) -->
          <button
            v-if="message.isRemoved && isModerator"
            class="hide-deleted-btn"
            @click="toggleDeletedContent"
          >
            Скрыть
          </button>

          <!-- Anchor link -->
          <Tooltip text="Скопировать ссылку">
            <button class="anchor-btn" @click="copyAnchorLink">
              <SvgIcon name="anchor" />
            </button>
          </Tooltip>
        </div>

        <!-- Content (non-compact mode) -->
        <template v-if="!compact">
          <template v-if="isEditing">
            <div class="edit-container">
              <textarea
                v-model="editText"
                class="edit-textarea"
                rows="4"
                @keydown="handleEditKeydown"
              />
              <div class="edit-actions">
                <button class="action-btn save-btn" @click="saveEdit">
                  Сохранить
                </button>
                <button class="action-btn cancel-btn" @click="cancelEdit">
                  Отменить
                </button>
              </div>
            </div>
          </template>

          <template v-else>
            <div
              ref="contentRef"
              class="message-content bbcode-content"
              :class="{ collapsed: needsTruncation && !isExpanded }"
              :style="{
                maxHeight:
                  needsTruncation && !isExpanded ? `${maxHeight}px` : 'none',
              }"
              v-html="displayText"
            />

            <!-- Expand/collapse button for long messages -->
            <button
              v-if="needsTruncation"
              class="expand-btn"
              @click="toggleExpand"
            >
              {{ isExpanded ? "Свернуть" : "Читать далее" }}
            </button>

            <!-- Footer -->
            <div class="message-footer">
              <!-- Likes -->
              <div
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
                  <span class="like-icon">♥</span>
                  <span v-if="likesCount > 0" class="likes-count">{{
                    likesCount
                  }}</span>
                </button>

                <!-- Likes popup -->
                <div
                  v-if="showLikesPopup && likesCount > 0"
                  class="likes-popup"
                >
                  <div
                    v-for="liker in message.likes"
                    :key="liker.username"
                    class="liker"
                  >
                    {{ liker.username }}
                  </div>
                </div>
              </div>

              <!-- Actions -->
              <span class="message-actions full-mode">
                <button v-if="canEdit" class="action-btn" @click="startEdit">
                  Редактировать
                </button>
                <button
                  v-if="canDelete"
                  class="action-btn delete-btn"
                  @click="handleDelete"
                >
                  Удалить
                </button>
                <button
                  v-if="canWarn"
                  class="action-btn warn-btn"
                  @click="handleWarn"
                >
                  Предупреждение
                </button>
              </span>

              <!-- Anchor link -->
              <Tooltip text="Скопировать ссылку">
                <button class="anchor-btn" @click="copyAnchorLink">
                  <SvgIcon name="anchor" />
                </button>
              </Tooltip>
            </div>
          </template>
        </template>

        <!-- Compact edit mode -->
        <template v-if="compact && isEditing">
          <div class="edit-container compact-edit">
            <textarea
              v-model="editText"
              class="edit-textarea"
              rows="2"
              @keydown="handleEditKeydown"
            />
            <div class="edit-actions">
              <button class="action-btn save-btn" @click="saveEdit">OK</button>
              <button class="action-btn cancel-btn" @click="cancelEdit">
                ✕
              </button>
            </div>
          </div>
        </template>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"
@import "src/assets/styles/ZIndex"

.content-message
  display: flex
  gap: $small
  padding: $small
  position: relative
  border: 1px dashed $border
  background-color: $bg-element

  &:not(:last-child)
    margin-bottom: $small

  &.removed,
  &.hidden
    background-color: $bg-element-accent

.hidden-placeholder,
.deleted-placeholder
  display: flex
  align-items: center
  gap: $small
  padding: $medium
  width: 100%
  color: $text-muted

.hidden-text,
.deleted-text
  font-style: italic

.show-deleted-btn,
.hide-deleted-btn
  padding: $tiny $small
  font-size: $secondary-font-size
  border: none
  border-radius: $tiny
  cursor: pointer
  background-color: $bg-element-accent
  color: $text-muted

  &:hover
    color: $link

.avatar-link
  flex-shrink: 0

.avatar
  width: $grid-step * 8
  height: $grid-step * 8
  border-radius: 50%
  object-fit: cover

.message-body
  flex: 1
  min-width: 0
  overflow: hidden

.message-header
  display: flex
  align-items: baseline
  gap: $small
  margin-bottom: $tiny
  flex-wrap: wrap

.author-name
  text-decoration: none

  &.online
    color: $accent-green

    &:hover
      opacity: 0.8
      text-decoration: underline

  &.offline
    color: $text-muted

    &:hover
      color: $link-nav-hover
      text-decoration: underline

.message-time
  font-size: $secondary-font-size
  color: $text-muted
  cursor: help

.edited-marker
  margin-left: $tiny
  color: $text-muted

// .message-content uses global .bbcode-content class
.message-content
  overflow: hidden
  color: $text

  &.collapsed
    overflow: hidden
    position: relative

    &::after
      content: ""
      position: absolute
      bottom: 0
      left: 0
      right: 0
      height: $grid-step * 10
      background: linear-gradient(transparent, var(--bg-element))

.expand-btn
  padding: $tiny $small
  margin-top: $small
  font-size: $secondary-font-size
  border: none
  border-radius: $tiny
  cursor: pointer
  background-color: $bg-element-accent
  color: $link

  &:hover
    background-color: $bg-element

.edit-container
  margin-top: $small

.edit-textarea
  width: 100%
  padding: $small
  border: 1px dashed $border
  background-color: $input-bg
  color: $text
  border-radius: $border-radius
  resize: vertical
  font-family: inherit
  font-size: inherit
  min-height: $grid-step * 20
  box-sizing: border-box

  &:focus
    outline: none
    border-style: solid
    border-color: $button-border-hover

.edit-actions
  display: flex
  gap: $small
  margin-top: $small

.message-footer
  display: flex
  align-items: baseline
  flex-wrap: wrap
  gap: 0 $tiny
  margin-top: $small
  font-size: $secondary-font-size
  color: $text-muted
  line-height: 1.6

.author-label
  color: $text-muted

.author-link
  color: $link
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

// Role badges: [А], [С], [М], [Н], [Р] - gray brackets, green bold letter
.role-badge
  display: inline
  color: $text-muted

  b
    font-weight: bold
    color: $accent-green
    cursor: help

.status-badge
  color: $text-muted

  .online
    color: $accent-green

  .offline
    color: $text-muted

.message-date
  color: $text-muted
  cursor: help

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

.like-icon
  font-size: 1em

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

.message-actions
  display: inline-flex
  align-items: center
  gap: $small
  margin-left: $small

  &.full-mode
    margin-left: 0
    margin-top: $small

.action-btn
  padding: 0 $tiny
  font-size: $secondary-font-size
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted

  &:hover
    color: $link

  &.save-btn
    background-color: $button-bg
    color: $button-text
    padding: $tiny $small
    border-radius: $tiny

  &.cancel-btn
    padding: $tiny $small

  &.delete-btn:hover
    color: $accent-red

  &.warn-btn:hover
    color: $accent-red

.anchor-btn
  margin-left: auto
  padding: 0 $tiny
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted
  display: inline-flex
  align-items: center

  svg
    width: 14px
    height: 14px

  &:hover
    color: $link

// Compact mode styles (matches Comment.vue)
.content-message.compact
  display: block

  // Hide avatar in compact mode
  .avatar-link
    display: none

  .compact-edit
    margin-top: $tiny

    .edit-textarea
      min-height: $grid-step * 8
      padding: $tiny $small

    .edit-actions
      margin-top: $tiny
      gap: $tiny

    .action-btn
      padding: $tiny $small
</style>
