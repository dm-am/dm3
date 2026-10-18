<script setup lang="ts">
/**
 * ChatsList: the list of conversations, at the right of the messenger beside
 * the open correspondence: the search of a person to write to on top, the
 * rows under it.
 *
 * Narrow (`narrow`), the list keeps the pictures and the counts of unread;
 * the search is its magnifier, which widens the list back and puts the caret
 * in the field.
 *
 * The list follows the reader's mail as it comes: a new message in any of
 * his conversations reads the page of the list again, so its row moves up
 * with its count.
 */
import { nextTick, onMounted, onUnmounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useRoute, useRouter } from "vue-router";
import { useMessagingStore } from "@/entities/message";
import { useAuthStore } from "@/entities/user";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import ChatPreview from "./ChatPreview.vue";
import { listPageQuery } from "./listPage";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { userApi } from "@/entities/user";
import type { User } from "@/shared/api/models/community";
import { symbols } from "@/shared/lib/utils/icons";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { SvgIcon } from "@/shared/ui/Icon";
import { Tooltip } from "@/shared/ui/Tooltip";
import { EmptyState } from "@/shared/ui/EmptyState";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ChatPreviewSkeleton } from "@/shared/ui/Skeleton";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import {
  NotificationType,
  type SignalRNotification,
} from "@/shared/api/models/notifications";

withDefaults(
  defineProps<{
    /** The narrow list: pictures and counts of unread. */
    narrow?: boolean;
  }>(),
  { narrow: false },
);

const emit = defineEmits<{
  /** The narrow list asks to have its words back. */
  widen: [];
}>();

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();
const { chats, loadingChats, error: chatsError } = storeToRefs(messagingStore);
const { user: currentUser } = storeToRefs(useAuthStore());

const searchQuery = ref("");
const searchResults = ref<User[]>([]);
const isSearching = ref(false);
const showResults = ref(false);
const searchInput = ref<HTMLInputElement | null>(null);
let searchTimeout: ReturnType<typeof setTimeout> | null = null;

function extractPage(value: string | null | undefined): number {
  const num = parseInt(value ?? "1", 10);
  return isNaN(num) || num < 1 ? 1 : num;
}

// The shared Paging widget writes the page as ?number= (codebase-wide
// query-key convention) — read the same key back.
function loadChats() {
  return messagingStore.fetchChats(
    extractPage(route.query.number as string | undefined),
  );
}

useFetchData(
  loadChats,
  [],
  [
    {
      query: (q) => q.number,
      callback: () => loadChats(),
    },
  ],
);

// ─────────────────────────────────────────────────────────────
// New mail: the page of the list read again, once for a burst of pushes.
// ─────────────────────────────────────────────────────────────
const REREAD_DELAY_MS = 1000;
let rereadTimer: ReturnType<typeof setTimeout> | null = null;
let unsubscribe: (() => void) | null = null;
const { onNotification } = useGlobalSignalR();

function onMail(notification: SignalRNotification) {
  if (notification.eventType !== NotificationType.NewMessage) return;
  if (rereadTimer) clearTimeout(rereadTimer);
  rereadTimer = setTimeout(() => {
    rereadTimer = null;
    loadChats();
  }, REREAD_DELAY_MS);
}

onMounted(() => {
  unsubscribe = onNotification(onMail);
});

onUnmounted(() => {
  unsubscribe?.();
  if (rereadTimer) clearTimeout(rereadTimer);
});

function getInterlocutor(chat: { participants: User[] }) {
  return chat.participants.find(
    (p) => p.username !== currentUser.value?.username,
  );
}

// Paging scrolls the chats list back into view (not the page top)
const chatsListRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return chatsListRef.value;
}

async function searchUsers(query: string) {
  if (query.length < 1) {
    searchResults.value = [];
    return;
  }
  isSearching.value = true;
  try {
    const { data } = await userApi.searchUsers(query, 6);
    searchResults.value = data?.resources ?? [];
  } finally {
    isSearching.value = false;
  }
}

function onSearchInput() {
  if (searchTimeout) clearTimeout(searchTimeout);
  if (searchQuery.value.length > 0) {
    showResults.value = true;
    searchTimeout = setTimeout(() => searchUsers(searchQuery.value), 300);
  } else {
    searchResults.value = [];
    showResults.value = false;
  }
}

function selectUser(user: User) {
  searchQuery.value = "";
  searchResults.value = [];
  showResults.value = false;
  router.push({
    name: "direct-message",
    params: { username: user.username },
    query: listPageQuery(route.query),
  });
}

function onSearchFocus() {
  if (searchQuery.value.length > 0) {
    showResults.value = true;
  }
}

function onSearchBlur() {
  setTimeout(() => {
    showResults.value = false;
  }, 200);
}

function clearSearch() {
  searchQuery.value = "";
  searchResults.value = [];
  showResults.value = false;
}

/** The magnifier of the narrow list: the words back, the caret in the field. */
async function openSearch() {
  emit("widen");
  await nextTick();
  searchInput.value?.focus();
}
</script>

<template>
  <div class="messenger-list" :class="{ 'messenger-list--narrow': narrow }">
    <div class="search-section">
      <Tooltip v-if="narrow" text="Поиск собеседника" placement="left">
        <button
          type="button"
          class="search-open"
          aria-label="Поиск собеседника"
          @click="openSearch"
        >
          <SvgIcon name="search" />
        </button>
      </Tooltip>
      <div v-else class="search-container">
        <SvgIcon name="search" class="search-icon" />
        <input
          ref="searchInput"
          v-model="searchQuery"
          type="text"
          class="the-input"
          placeholder="Поиск собеседника"
          aria-label="Поиск собеседника"
          @input="onSearchInput"
          @focus="onSearchFocus"
          @blur="onSearchBlur"
        />
        <button
          v-if="searchQuery"
          type="button"
          class="clear-input-btn"
          aria-label="Очистить поиск"
          @click.stop="clearSearch"
        >
          {{ symbols.close }}
        </button>
      </div>

      <div
        v-if="
          !narrow && showResults && (searchResults.length > 0 || isSearching)
        "
        class="search-results"
      >
        <div v-if="isSearching" class="search-loading">...</div>
        <div
          v-for="user in searchResults"
          :key="user.username"
          class="search-result-item"
          @mousedown.prevent="selectUser(user)"
        >
          <AvatarImg
            :picture="user.picture"
            :alt="user.username"
            :size="24"
            img-class="result-avatar"
          />
          <span
            class="result-name"
            v-html="highlightMatch(user.username, searchQuery)"
          />
        </div>
        <div
          v-if="
            !isSearching && searchResults.length === 0 && searchQuery.length > 0
          "
          class="search-empty"
        >
          Никого не найдено
        </div>
      </div>
    </div>

    <div v-if="chats?.resources.length" ref="chatsListRef" class="chats-list">
      <chat-preview
        v-for="chat in chats.resources"
        :key="chat.id"
        :chat="chat"
        :interlocutor="getInterlocutor(chat)"
        :current-user="currentUser"
        :narrow="narrow"
      />

      <Paging
        v-if="chats.paging"
        :paging="chats.paging"
        :to="{ name: route.name, params: route.params }"
        use-query
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />
    </div>

    <!-- Loading. A first open has nothing to keep on screen, and the empty
         state below answered "Нет переписок" while the request was still on
         the wire. The narrow list waits with pictures alone. -->
    <template v-else-if="loadingChats">
      <div v-if="narrow" class="narrow-skeleton" aria-hidden="true">
        <div v-for="i in 5" :key="i" class="narrow-skeleton-avatar" />
      </div>
      <ChatPreviewSkeleton v-else />
    </template>

    <!-- A failed load must not be presented as fake emptiness — the idiom is
         written out in pages/forum/CommentsList. Before this a 500 read as
         "у вас нет переписок", with nothing to press and only a page reload
         to recover. -->
    <ErrorState
      v-else-if="chatsError"
      :message="chatsError"
      :retry="loadChats"
    />

    <EmptyState
      v-else
      title="Нет переписок"
      hint="Найдите собеседника через поиск выше"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Skeleton" as *
@use "@/assets/styles/ZIndex" as *

.messenger-list
  display: flex
  flex-direction: column
  min-width: 0

.search-section
  position: relative
  margin-bottom: $medium

// Reuse filter search container styles from _Filters.sass
+filter-search-container

// The magnifier of the narrow list: a square control of the field's height,
// as wide as the picture under it.
.search-open
  +button
  &
    width: $grid-step * 12
    height: $control-height
    padding: 0
    display: flex
    align-items: center
    justify-content: center
  svg
    width: 16px
    height: 16px

.messenger-list--narrow .search-section
  padding: 0 $small

.search-results
  position: absolute
  top: 100%
  left: 0
  right: 0
  margin-top: $tiny
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  box-shadow: 0 4px 12px $shadow-color
  z-index: $z-dropdown
  max-height: 300px
  overflow-y: auto

.search-loading
  display: flex
  justify-content: center
  padding: $medium

.search-result-item
  display: flex
  align-items: center
  gap: $small
  padding: $small $medium
  cursor: pointer

  &:hover
    background-color: $bg-element-accent

.result-avatar
  width: 24px
  height: 24px
  object-fit: cover

.result-name
  color: $text
  font-weight: 500

.search-empty
  padding: $medium
  color: $text-muted

.chats-list
  display: flex
  flex-direction: column
  gap: $tiny

// The narrow list's wait: the squares of the pictures, as the rows lay them.
.narrow-skeleton
  display: flex
  flex-direction: column
  gap: $tiny

.narrow-skeleton-avatar
  width: $grid-step * 12
  height: $grid-step * 12
  margin: $small
  +skeleton-shimmer($square: true)
</style>
