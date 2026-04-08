<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from "vue";
import { storeToRefs } from "pinia";
import type { Comment } from "@/shared/api/models/common/comment";
import { useUserStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/community";
import { Tooltip } from "@/shared/ui/Tooltip";
import dayjs from "dayjs";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";
import defaultPicture from "@/assets/images/userpic.png";

const props = withDefaults(
  defineProps<{
    comment: Comment;
    compact?: boolean;
    number?: number;
  }>(),
  {
    compact: true,
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
const ONLINE_THRESHOLD_MINUTES = 5;

const { user: currentUser } = storeToRefs(useUserStore());

// State
const isEditing = ref(false);
const editText = ref("");
const isExpanded = ref(false);
const showDeletedContent = ref(false);
const contentRef = ref<HTMLElement | null>(null);
const isOverflowing = ref(false);
const showLikesPopup = ref(false);
const maxHeight = 300;

// Computed
const authorPicture = computed(
  () => props.comment.author?.picture?.mediumUrl || props.comment.author?.picture?.smallUrl || defaultPicture,
);

const formattedDate = computed(() => {
  if (!props.comment.createdUtc) return "";
  return dayjs(props.comment.createdUtc).format("DD.MM.YYYY HH:mm");
});

const formattedEditDate = computed(() => {
  if (!props.comment.modifiedUtc) return "";
  return dayjs(props.comment.modifiedUtc).format("DD.MM.YYYY в HH:mm");
});

const isEdited = computed(() => !!props.comment.modifiedUtc);

const isModerator = computed(() => {
  if (!currentUser.value) return false;
  return (currentUser.value.roles ?? []).some((r) =>
    [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(r),
  );
});

const isAuthor = computed(
  () => currentUser.value?.username === props.comment.author?.username,
);

const canEdit = computed(() => {
  if (!currentUser.value) return false;
  if (props.comment.isRemoved) return false;
  if (isModerator.value) return true;
  if (!isAuthor.value) return false;

  const timeSinceCreation = dayjs().diff(
    dayjs(props.comment.createdUtc),
    "minute",
    true,
  );
  return timeSinceCreation <= EDIT_TIME_LIMIT_MINUTES;
});

const canDelete = computed(() => canEdit.value);

const canWarn = computed(() => isModerator.value);

const isLikedByMe = computed(() => {
  if (!currentUser.value) return false;
  return props.comment.likes?.some(
    (u) => u.username === currentUser.value?.username,
  );
});

const canLike = computed(() => {
  if (!currentUser.value) return false;
  return !isAuthor.value;
});

const likesCount = computed(() => props.comment.likes?.length ?? 0);

const commentAnchor = computed(() => `#comment-${props.comment.id}`);

const isAuthorOnline = computed(() => {
  const lastActivityUtc = props.comment.author?.lastActivityUtc;
  if (!lastActivityUtc) return false;
  return dayjs().diff(dayjs(lastActivityUtc), "minute", true) <= ONLINE_THRESHOLD_MINUTES;
});

const roleBadge = computed(() => {
  switch (props.comment.author?.role) {
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

const hasCutTag = computed(() => props.comment.text?.includes("[cut]"));

const displayText = computed(() => {
  if (!props.comment.text) return "";
  if (hasCutTag.value && !isExpanded.value) {
    const cutIndex = props.comment.text.indexOf("[cut]");
    return props.comment.text.substring(0, cutIndex);
  }
  return props.comment.text;
});

const needsTruncation = computed(() => hasCutTag.value || isOverflowing.value);

// Methods
function startEdit() {
  editText.value = props.comment.text || "";
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
}

function saveEdit() {
  if (editText.value.trim()) {
    emit("edit", props.comment.id, editText.value);
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
    emit("unlike", props.comment.id);
  } else {
    emit("like", props.comment.id);
  }
}

function handleDelete() {
  emit("delete", props.comment.id);
}

function handleWarn() {
  emit("warn", props.comment.id);
}

function toggleExpand() {
  isExpanded.value = !isExpanded.value;
}

function toggleDeletedContent() {
  showDeletedContent.value = !showDeletedContent.value;
}

function copyAnchorLink() {
  navigator.clipboard.writeText(
    window.location.origin + window.location.pathname + commentAnchor.value,
  );
}

function checkContentHeight() {
  if (contentRef.value && !hasCutTag.value) {
    isOverflowing.value = contentRef.value.scrollHeight > maxHeight;
  }
}

onMounted(() => {
  checkContentHeight();
  nextTick(() => {
    initBbcodeInteractive(contentRef.value);
  });
});

watch(
  () => props.comment.text,
  () => {
    checkContentHeight();
    nextTick(() => {
      initBbcodeInteractive(contentRef.value);
    });
  },
);
</script>

<template>
  <div
    :id="`comment-${comment.id}`"
    class="comment"
    :class="{ removed: comment.isRemoved && !showDeletedContent, compact: compact }"
  >
    <!-- Deleted comment placeholder -->
    <template v-if="comment.isRemoved && !showDeletedContent">
      <div class="deleted-placeholder">
        <span class="deleted-text">Комментарий удален</span>
        <button v-if="isModerator" class="show-deleted-btn" @click="toggleDeletedContent">
          Показать
        </button>
      </div>
    </template>

    <!-- Normal comment content -->
    <template v-else>
      <!-- Avatar (non-compact only) -->
      <router-link
        v-if="!compact && comment.author"
        :to="{ name: 'profile', params: { username: comment.author.username } }"
        class="avatar-link"
      >
        <img :src="authorPicture" :alt="comment.author.username" class="avatar" />
      </router-link>

      <div class="comment-body">
        <!-- Edit mode -->
        <template v-if="isEditing">
          <div class="edit-container">
            <textarea
              v-model="editText"
              class="edit-textarea"
              rows="4"
              @keydown="handleEditKeydown"
            />
            <div class="edit-actions">
              <button class="action-btn save-btn" @click="saveEdit">Сохранить</button>
              <button class="action-btn cancel-btn" @click="cancelEdit">Отменить</button>
            </div>
          </div>
        </template>

        <!-- View mode -->
        <template v-else>
          <div
            ref="contentRef"
            class="comment-text bbcode-content"
            :class="{ collapsed: needsTruncation && !isExpanded }"
            :style="{ maxHeight: needsTruncation && !isExpanded ? `${maxHeight}px` : 'none' }"
            v-html="displayText"
          />
          <button v-if="needsTruncation" class="expand-btn" @click="toggleExpand">
            {{ isExpanded ? "Свернуть" : "Читать далее" }}
          </button>
        </template>

        <!-- Footer: Author info + Actions + Number -->
        <div class="comment-footer">
          <span v-if="comment.author" class="author-info"
            >Автор: <router-link
              :to="{ name: 'profile', params: { username: comment.author.username } }"
              class="author-link"
            >{{ comment.author.username }}</router-link
            ><template v-if="roleBadge"
              > [<Tooltip :text="roleBadge.title"><b class="role-letter">{{ roleBadge.label }}</b></Tooltip>]</template
            > [<span :class="isAuthorOnline ? 'online' : 'offline'">{{ isAuthorOnline ? "online" : "offline" }}</span
            >], {{ formattedDate }}<template v-if="isEdited"
              > | Отредактировано {{ formattedEditDate }}</template
          ></span>

          <!-- Actions -->
          <span class="comment-actions">
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
                <div v-for="liker in comment.likes" :key="liker.username" class="liker">
                  {{ liker.username }}
                </div>
              </div>
            </span>
            <button v-if="canEdit" class="action-btn" @click="startEdit">ред.</button>
            <button v-if="canDelete" class="action-btn delete-btn" @click="handleDelete">удл.</button>
            <button v-if="canWarn" class="action-btn warn-btn" @click="handleWarn">пред.</button>
            <button
              v-if="comment.isRemoved && isModerator"
              class="action-btn"
              @click="toggleDeletedContent"
            >
              Скрыть
            </button>
          </span>

          <!-- Number (anchor link) -->
          <Tooltip v-if="number" text="Скопировать ссылку">
            <button class="comment-number" @click="copyAnchorLink">{{ number }}</button>
          </Tooltip>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"
@import "src/assets/styles/ZIndex"

.comment
  display: flex
  gap: $medium
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

  &.removed
    background-color: $bg-element-accent

  &.compact
    display: block

.deleted-placeholder
  display: flex
  align-items: center
  gap: $small
  padding: $small 0
  color: $text-muted
  width: 100%

.deleted-text
  font-style: italic

.show-deleted-btn
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
  width: 64px
  height: 64px
  border-radius: 50%
  object-fit: cover

.comment-body
  flex: 1
  min-width: 0

// .comment-text uses global .bbcode-content class
.comment-text
  overflow: hidden
  color: $text
  line-height: 1.6

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
  margin-bottom: $small

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

.comment-footer
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

.comment-actions
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

.comment-number
  margin-left: auto
  padding: 0
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted
  font-size: inherit
  font-family: inherit

  &:hover
    color: $link
</style>
