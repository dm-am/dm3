<script setup lang="ts">
import { ref } from "vue";
import { storeToRefs } from "pinia";
import { useRoute, useRouter } from "vue-router";
import { useMessagingStore } from "@/entities/message";
import { useAuthStore, AvatarImg } from "@/entities/user";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import ChatPreview from "./ChatPreview.vue";
import Paging from "@/shared/ui/Paging/Paging.vue";
import communityApi from "@/shared/api/communityApi";
import type { User } from "@/shared/api/models/community";
import { symbols } from "@/shared/lib/utils/icons";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { SvgIcon } from "@/shared/ui/Icon";
import { EmptyState } from "@/shared/ui";

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();
const { chats } = storeToRefs(messagingStore);
const { user: currentUser } = storeToRefs(useAuthStore());

const searchQuery = ref("");
const searchResults = ref<User[]>([]);
const isSearching = ref(false);
const showResults = ref(false);
let searchTimeout: ReturnType<typeof setTimeout> | null = null;

function extractPage(value: string | null | undefined): number {
  const num = parseInt(value ?? "1", 10);
  return isNaN(num) || num < 1 ? 1 : num;
}

// The shared Paging widget writes the page as ?number= (codebase-wide
// query-key convention) — read the same key back.
useFetchData(
  () =>
    messagingStore.fetchChats(
      extractPage(route.query.number as string | undefined),
    ),
  [],
  [
    {
      query: (q) => q.number,
      callback: (page) =>
        messagingStore.fetchChats(extractPage(page as string | undefined)),
    },
  ],
);

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
    const { data } = await communityApi.searchUsers(query, 6);
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
  router.push({ name: "direct-message", params: { username: user.username } });
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
</script>

<template>
  <div class="messenger-list">
    <div class="search-section">
      <div class="search-container">
        <SvgIcon name="search" class="search-icon" />
        <input
          v-model="searchQuery"
          type="text"
          class="the-input"
          placeholder="Поиск собеседника"
          @input="onSearchInput"
          @focus="onSearchFocus"
          @blur="onSearchBlur"
        />
        <button
          v-if="searchQuery"
          type="button"
          class="clear-input-btn"
          @click.stop="clearSearch"
        >
          {{ symbols.close }}
        </button>
      </div>

      <div
        v-if="showResults && (searchResults.length > 0 || isSearching)"
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
      />

      <Paging
        v-if="chats.paging"
        :paging="chats.paging"
        :to="{ name: 'messenger' }"
        use-query
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />
    </div>

    <EmptyState
      v-else
      icon="envelope"
      title="Нет переписок"
      hint="Найдите собеседника через поиск выше"
    />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Filters"
@import "@/assets/styles/ZIndex"

.messenger-list
  display: flex
  flex-direction: column

.search-section
  position: relative
  margin-bottom: $medium

// Reuse filter search container styles from _Filters.sass
+filter-search-container

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
  text-align: center
  color: $text-muted

.chats-list
  display: flex
  flex-direction: column
  gap: $tiny
</style>
