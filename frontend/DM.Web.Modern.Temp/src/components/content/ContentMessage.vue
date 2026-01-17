<script setup lang="ts">
import { ref, computed, onMounted, watch } from "vue";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/stores";
import { UserRole, type User } from "@/api/models/community";
import defaultPicture from "@/assets/images/userpic.png";
import dayjs from "dayjs";

export interface MessageData {
  id: string;
  createdUtc: string;
  modifiedUtc: string | null;
  author: User;
  text: string;
  isRemoved: boolean;
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
  }
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
const needsSpoiler = ref(false);
const showLikesPopup = ref(false);

// Computed
const authorPicture = computed(
  () => props.message.author?.smallPictureUrl || defaultPicture
);

const formattedTime = computed(() => {
  if (!props.message.createdUtc) return "";
  return dayjs(props.message.createdUtc).format("HH:mm");
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
  return currentUser.value.roles.some((r) =>
    [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(r)
  );
});

const isAuthor = computed(
  () => currentUser.value?.login === props.message.author?.login
);

const canEdit = computed(() => {
  if (!currentUser.value) return false;
  if (props.message.isRemoved) return false;
  if (isModerator.value) return true;
  if (!isAuthor.value) return false;

  const timeSinceCreation = dayjs().diff(
    dayjs(props.message.createdUtc),
    "minute",
    true
  );
  return timeSinceCreation <= EDIT_TIME_LIMIT_MINUTES;
});

const canDelete = computed(() => canEdit.value);

const canWarn = computed(() => isModerator.value && props.isPublic);

const isLikedByMe = computed(() => {
  if (!currentUser.value) return false;
  return props.message.likes?.some((u) => u.login === currentUser.value?.login);
});

const canLike = computed(() => {
  if (!currentUser.value) return false;
  return !isAuthor.value;
});

const likesCount = computed(() => props.message.likes?.length ?? 0);

const messageAnchor = computed(() => `#message-${props.message.id}`);

const ONLINE_THRESHOLD_MINUTES = 15;
const isAuthorOnline = computed(() => {
  const onlineUtc = props.message.author?.onlineUtc;
  if (!onlineUtc) return false;
  const minutesSinceOnline = dayjs().diff(
    dayjs(onlineUtc),
    "minute",
    true
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

const shouldShowSpoiler = computed(
  () => hasCutTag.value || needsSpoiler.value
);

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
  navigator.clipboard.writeText(window.location.origin + window.location.pathname + messageAnchor.value);
}

function checkContentHeight() {
  if (contentRef.value && !hasCutTag.value) {
    needsSpoiler.value = contentRef.value.scrollHeight > props.maxHeight;
  }
}

onMounted(() => {
  checkContentHeight();
});

watch(() => props.message.text, () => {
  checkContentHeight();
});
</script>

<template>
  <div
    :id="`message-${message.id}`"
    class="content-message"
    :class="{ removed: message.isRemoved && !showDeletedContent, compact }"
  >
    <!-- Deleted message placeholder -->
    <template v-if="message.isRemoved && !showDeletedContent">
      <div class="deleted-placeholder">
        <span class="deleted-text">Сообщение удалено</span>
        <button v-if="isModerator" class="show-deleted-btn" @click="toggleDeletedContent">
          Показать
        </button>
      </div>
    </template>

    <!-- Normal message content -->
    <template v-else>
      <!-- Avatar -->
      <router-link
        :to="{ name: 'profile', params: { login: message.author.login } }"
        class="avatar-link"
      >
        <img :src="authorPicture" :alt="message.author.login" class="avatar" />
      </router-link>

      <div class="message-body">
        <!-- Header -->
        <div class="message-header">
          <router-link
            :to="{ name: 'profile', params: { login: message.author.login } }"
            class="author-name"
            :class="{ online: isAuthorOnline, offline: !isAuthorOnline }"
          >
            {{ message.author.login }}
          </router-link>
          <span class="message-time" :title="tooltipText">
            {{ formattedTime }}<span v-if="isEdited" class="edited-marker">(ред.)</span>
          </span>

          <!-- Likes inline in compact mode -->
          <div
            v-if="compact && (canLike || likesCount > 0)"
            class="likes-container inline-likes"
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
              <span v-if="likesCount > 0" class="likes-count">{{ likesCount }}</span>
            </button>
            <div v-if="showLikesPopup && likesCount > 0" class="likes-popup">
              <div v-for="liker in message.likes" :key="liker.login" class="liker">
                {{ liker.login }}
              </div>
            </div>
          </div>

          <!-- Compact actions (visible on hover) -->
          <div v-if="compact" class="compact-actions">
            <button v-if="canEdit" class="compact-action-btn" @click="startEdit" title="Редактировать">✎</button>
            <button v-if="canDelete" class="compact-action-btn delete" @click="handleDelete" title="Удалить">✕</button>
            <button v-if="canWarn" class="compact-action-btn warn" @click="handleWarn" title="Предупреждение">⚠</button>
          </div>

          <!-- Hide deleted button for moderators -->
          <button
            v-if="message.isRemoved && isModerator"
            class="hide-deleted-btn"
            @click="toggleDeletedContent"
          >
            Скрыть
          </button>
        </div>

        <!-- Compact message content (separate line, Teams-style) -->
        <div v-if="compact && !isEditing" class="compact-content" v-html="displayText" />

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
              class="message-content"
              :class="{ collapsed: shouldShowSpoiler && !isExpanded }"
              :style="{ maxHeight: shouldShowSpoiler && !isExpanded ? `${maxHeight}px` : 'none' }"
              v-html="displayText"
            />

            <!-- Spoiler button -->
            <button
              v-if="shouldShowSpoiler"
              class="spoiler-btn"
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
                  <span v-if="likesCount > 0" class="likes-count">{{ likesCount }}</span>
                </button>

                <!-- Likes popup -->
                <div v-if="showLikesPopup && likesCount > 0" class="likes-popup">
                  <div
                    v-for="liker in message.likes"
                    :key="liker.login"
                    class="liker"
                  >
                    {{ liker.login }}
                  </div>
                </div>
              </div>

              <!-- Actions -->
              <div class="message-actions">
                <button v-if="canEdit" class="action-btn" @click="startEdit">
                  Редактировать
                </button>
                <button v-if="canDelete" class="action-btn delete-btn" @click="handleDelete">
                  Удалить
                </button>
                <button v-if="canWarn" class="action-btn warn-btn" @click="handleWarn">
                  Предупреждение
                </button>
              </div>

              <!-- Anchor link -->
              <button class="anchor-btn" :title="messageAnchor" @click="copyAnchorLink">
                🔗
              </button>
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
              <button class="action-btn cancel-btn" @click="cancelEdit">✕</button>
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

.content-message
  display: flex
  gap: $small
  padding: $small
  position: relative

  &:not(:last-child)
    border-bottom: 1px solid
    +theme(border-bottom-color, $border)

  &.removed
    +theme(background-color, $panel-background)

.deleted-placeholder
  display: flex
  align-items: center
  gap: $small
  padding: $medium
  width: 100%
  +theme(color, $secondary-text)

.deleted-text
  font-style: italic

.show-deleted-btn,
.hide-deleted-btn
  padding: $tiny $small
  font-size: $secondary-font-size
  border: none
  border-radius: $tiny
  cursor: pointer
  +theme(background-color, $control-background)
  +theme(color, $secondary-text)

  &:hover
    +theme(color, $active-text)

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
    +theme(color, $link-online)

    &:hover
      opacity: 0.8
      text-decoration: underline

  &.offline
    +theme(color, $secondary-text)

    &:hover
      +theme(color, $secondary-text-hover)
      text-decoration: underline

.message-time
  font-size: $secondary-font-size
  +theme(color, $secondary-text)

.edited-marker
  margin-left: $tiny
  +theme(color, $secondary-text)

.message-content
  word-wrap: break-word
  word-break: break-word
  overflow-wrap: break-word
  overflow: hidden
  +theme(color, $text)

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
      background: linear-gradient(transparent, var(--panel-background, #fff))

.spoiler-btn
  padding: $tiny $small
  margin-top: $small
  font-size: $secondary-font-size
  border: none
  border-radius: $tiny
  cursor: pointer
  +theme(background-color, $control-background)
  +theme(color, $active-text)

  &:hover
    +theme(background-color, $panel-background-hover)

.edit-container
  margin-top: $small

.edit-textarea
  width: 100%
  padding: $small
  border: 1px solid
  +theme(border-color, $border)
  +theme(background-color, $input-background)
  +theme(color, $text)
  border-radius: $border-radius
  resize: vertical
  font-family: inherit
  font-size: inherit
  min-height: $grid-step * 20

  &:focus
    outline: none
    +theme(border-color, $active-text)

.edit-actions
  display: flex
  gap: $small
  margin-top: $small

.message-footer
  display: flex
  align-items: center
  gap: $medium
  margin-top: $small

.likes-container
  position: relative

.like-btn
  display: flex
  align-items: center
  gap: $tiny
  padding: $tiny $small
  border: none
  border-radius: $tiny
  cursor: pointer
  +theme(background-color, $control-background)
  +theme(color, $secondary-text)

  &:hover:not(:disabled)
    +theme(color, $negative-text)

  &.liked
    +theme(color, $negative-text)

  &:disabled
    cursor: default
    opacity: 0.6

.like-icon
  font-size: 1.1em

.likes-count
  font-weight: bold

.likes-popup
  position: absolute
  bottom: 100%
  left: 0
  padding: $small
  border-radius: $border-radius
  z-index: 100
  min-width: $grid-step * 30
  +theme(background-color, $panel-background-highlight)
  +theme(border, $border, 1px solid)
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15)

.liker
  padding: $tiny 0
  +theme(color, $text)

.message-actions
  display: flex
  gap: $small

.action-btn
  padding: $tiny $small
  font-size: $secondary-font-size
  border: none
  border-radius: $tiny
  cursor: pointer
  +theme(background-color, $control-background)
  +theme(color, $secondary-text)

  &:hover
    +theme(color, $active-text)

  &.save-btn
    +theme(background-color, $button-background)
    +theme(color, $button-text)

  &.delete-btn:hover
    +theme(color, $negative-text)

  &.warn-btn:hover
    +theme(color, $anticipation)

.anchor-btn
  margin-left: auto
  padding: $tiny
  border: none
  background: transparent
  cursor: pointer
  opacity: 0.5
  font-size: 0.9em

  &:hover
    opacity: 1

// Compact mode styles (Teams-like)
.content-message.compact
  padding: $small
  gap: $small
  align-items: flex-start

  .avatar
    width: $grid-step * 5
    height: $grid-step * 5
    margin-top: 2px

  .message-header
    display: flex
    align-items: baseline
    gap: $small
    margin-bottom: 0
    flex-wrap: nowrap

  .author-name
    flex-shrink: 0

  .message-time
    flex-shrink: 0
    font-size: 11px

  .compact-content
    margin-top: $tiny
    +theme(color, $text)
    line-height: 1.4
    word-break: break-word
    overflow-wrap: break-word

    :deep(p)
      margin: 0

      &:not(:last-child)
        margin-bottom: $tiny

  .inline-likes
    flex-shrink: 0
    margin-left: auto

    .like-btn
      padding: 0 $tiny
      background: transparent

  .compact-actions
    display: none
    gap: $tiny
    margin-left: $tiny

  &:hover .compact-actions
    display: flex

  .compact-action-btn
    padding: 0 $tiny
    border: none
    background: transparent
    cursor: pointer
    font-size: 12px
    +theme(color, $secondary-text)

    &:hover
      +theme(color, $active-text)

    &.delete:hover
      +theme(color, $negative-text)

    &.warn:hover
      +theme(color, $anticipation)

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
