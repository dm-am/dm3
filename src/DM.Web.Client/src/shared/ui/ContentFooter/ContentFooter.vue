<script setup lang="ts">
import { ref, computed, toRef } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useContentAuthor } from "@/shared/lib/composables/useContentAuthor";
import type { UserRole } from "@/shared/api/models/community";

interface Author {
  username: string;
  role?: UserRole;
  lastActivityUtc?: string;
  smallPictureUrl?: string;
}

interface Like {
  username: string;
}

const props = withDefaults(
  defineProps<{
    author?: Author | null;
    createdUtc?: string;
    modifiedUtc?: string;
    likes?: Like[];
    number?: number;
    anchorPrefix?: string;
    compact?: boolean;
    canEdit?: boolean;
    canDelete?: boolean;
    canWarn?: boolean;
    canHide?: boolean;
  }>(),
  {
    anchorPrefix: "comment",
    compact: false,
    canEdit: false,
    canDelete: false,
    canWarn: false,
    canHide: false,
  }
);

const emit = defineEmits<{
  like: [];
  unlike: [];
  edit: [];
  delete: [];
  warn: [];
  hide: [];
}>();

// Create reactive content object for composable
const content = computed(() => ({
  author: props.author,
  createdUtc: props.createdUtc,
  modifiedUtc: props.modifiedUtc,
}));

const {
  isAuthorOnline,
  roleBadge,
  formattedDate,
  formattedEditDate,
  isEdited,
  canLike,
  currentUser,
} = useContentAuthor(content);

const showLikesPopup = ref(false);

const likesCount = computed(() => props.likes?.length ?? 0);

const isLikedByMe = computed(() => {
  if (!currentUser.value) return false;
  return props.likes?.some((u) => u.username === currentUser.value?.username);
});

const anchorLink = computed(() => {
  if (!props.number) return "";
  return `#${props.anchorPrefix}-${props.number}`;
});

function toggleLike() {
  if (isLikedByMe.value) {
    emit("unlike");
  } else {
    emit("like");
  }
}

function copyAnchorLink() {
  if (!anchorLink.value) return;
  navigator.clipboard.writeText(
    window.location.origin + window.location.pathname + anchorLink.value
  );
}
</script>

<template>
  <div class="content-footer">
    <!-- Author info -->
    <span v-if="author" class="author-info">
      <span class="author-label">Автор: </span>
      <router-link
        :to="{ name: 'profile', params: { username: author.username } }"
        class="author-link"
      >{{ author.username }}</router-link>
      <span v-if="roleBadge" class="role-badge">
        [<Tooltip :text="roleBadge.title"><b>{{ roleBadge.label }}</b></Tooltip>]
      </span>
      <span class="status-badge">
        [<span :class="isAuthorOnline ? 'online' : 'offline'">{{ isAuthorOnline ? "online" : "offline" }}</span>]
      </span>
      <span class="date-info">
        , {{ formattedDate }}
        <template v-if="isEdited">
          | Отредактировано {{ formattedEditDate }}
        </template>
      </span>
    </span>

    <!-- Actions -->
    <span class="actions" :class="{ 'full-mode': !compact }">
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
          <span class="like-icon">♥</span>
          <span v-if="likesCount > 0" class="likes-count">{{ likesCount }}</span>
        </button>

        <div v-if="showLikesPopup && likesCount > 0" class="likes-popup">
          <div v-for="liker in likes" :key="liker.username" class="liker">
            {{ liker.username }}
          </div>
        </div>
      </span>

      <button v-if="canEdit" class="action-btn" @click="emit('edit')">
        {{ compact ? "ред." : "Редактировать" }}
      </button>
      <button v-if="canDelete" class="action-btn delete-btn" @click="emit('delete')">
        {{ compact ? "удл." : "Удалить" }}
      </button>
      <button v-if="canWarn" class="action-btn warn-btn" @click="emit('warn')">
        {{ compact ? "пред." : "Предупреждение" }}
      </button>
      <button v-if="canHide" class="action-btn" @click="emit('hide')">
        Скрыть
      </button>
    </span>

    <!-- Number anchor -->
    <span v-if="number" class="number-wrapper">
      <Tooltip text="Скопировать ссылку">
        <button class="number-btn" @click="copyAnchorLink">
          {{ number }}
        </button>
      </Tooltip>
    </span>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/ZIndex"

.content-footer
  display: flex
  align-items: baseline
  flex-wrap: nowrap
  gap: 0 $tiny
  margin-top: $small
  font-size: $tertiary-font-size
  color: $text-muted
  line-height: 1.6
  width: 100%

.author-info
  display: inline

.author-label
  color: $text-muted

.author-link
  color: $link
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

.role-badge
  display: inline
  color: $text-muted
  margin-left: 1px

  b
    font-weight: bold
    color: $accent-green
    cursor: help

.status-badge
  color: $text-muted
  margin-left: 1px

  .online
    color: $accent-green

  .offline
    color: $text-muted

.date-info
  color: $text-muted

.actions
  display: inline-flex
  align-items: center
  gap: $small
  margin-left: $small

  &.full-mode
    margin-left: 0
    margin-top: $small

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

.action-btn
  padding: 0 $tiny
  font-size: $secondary-font-size
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted

  &:hover
    color: $link

  &.delete-btn:hover
    color: $accent-red

  &.warn-btn:hover
    color: $accent-red

.number-wrapper
  margin-left: auto

.number-btn
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
