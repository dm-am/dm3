<script setup lang="ts">
import { ref } from "vue";
import { storeToRefs } from "pinia";
import { useRoute, useRouter } from "vue-router";
import { useMessagingStore, useUserStore } from "@/stores";
import { useFetchData } from "@/composables/useFetchData";
import { extractNumberParam } from "@/router";
import ConversationPreview from "./ConversationPreview.vue";
import ThePaging from "@/components/ThePaging.vue";
import communityApi from "@/api/requests/communityApi";
import type { User } from "@/api/models/community";
import defaultAvatar from "@/assets/images/userpic.png";

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();
const { conversations, loadingConversations } = storeToRefs(messagingStore);
const { user: currentUser } = storeToRefs(useUserStore());

const searchQuery = ref("");
const searchResults = ref<User[]>([]);
const isSearching = ref(false);
const showResults = ref(false);
let searchTimeout: ReturnType<typeof setTimeout> | null = null;

useFetchData(
  () =>
    messagingStore.fetchConversations(
      extractNumberParam(route.params.n as string),
    ),
  [
    {
      param: (p) => p.n,
      callback: (n) =>
        messagingStore.fetchConversations(extractNumberParam(n as string)),
    },
  ],
);

function getInterlocutor(conv: { participants: User[] }) {
  return conv.participants.find((p) => p.login !== currentUser.value?.login);
}

async function searchUsers(query: string) {
  if (query.length < 1) {
    searchResults.value = [];
    return;
  }
  isSearching.value = true;
  try {
    const { data } = await communityApi.searchUsers(query);
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
  router.push({ name: "direct-message", params: { login: user.login } });
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
</script>

<template>
  <div class="messenger-list">
    <div class="search-section">
      <div class="search-container">
        <svg
          class="search-icon"
          viewBox="0 0 24 24"
          width="18"
          height="18"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
        >
          <circle cx="11" cy="11" r="8" />
          <path d="M21 21l-4.35-4.35" />
        </svg>
        <input
          v-model="searchQuery"
          type="text"
          class="search-input"
          placeholder="Поиск собеседника"
          @input="onSearchInput"
          @focus="onSearchFocus"
          @blur="onSearchBlur"
        />
      </div>

      <div
        v-if="showResults && (searchResults.length > 0 || isSearching)"
        class="search-results"
      >
        <div v-if="isSearching" class="search-loading">
          ...
        </div>
        <div
          v-for="user in searchResults"
          :key="user.login"
          class="search-result-item"
          @mousedown.prevent="selectUser(user)"
        >
          <img
            :src="user.smallPictureUrl || defaultAvatar"
            class="result-avatar"
          />
          <span class="result-name">{{ user.login }}</span>
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

    <div v-if="conversations?.resources.length" class="conversations-list">
      <conversation-preview
        v-for="conv in conversations.resources"
        :key="conv.id"
        :conversation="conv"
        :interlocutor="getInterlocutor(conv)"
        :current-user="currentUser"
      />

      <the-paging
        v-if="conversations.paging"
        :paging="conversations.paging"
        :to="{ name: 'messenger' }"
      />
    </div>

    <div v-else class="empty-state">
      <svg
        class="empty-icon"
        viewBox="0 0 64 64"
        width="64"
        height="64"
        fill="none"
        stroke="currentColor"
        stroke-width="1.5"
      >
        <rect x="8" y="12" width="48" height="36" rx="4" />
        <path d="M8 20l24 16 24-16" />
      </svg>
      <div class="empty-title">Нет переписок</div>
      <div class="empty-hint">Найдите собеседника через поиск выше</div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.messenger-list
  display: flex
  flex-direction: column

.search-section
  position: relative
  margin-bottom: $medium

.search-container
  position: relative
  display: flex
  align-items: center

.search-icon
  position: absolute
  left: $medium
  color: $text-muted
  pointer-events: none

.search-input
  width: 100%
  padding: $small $medium $small ($medium + 26px)
  border: 1px dashed $border
  border-radius: $border-radius
  background-color: $input-bg
  color: $text
  font-family: inherit
  font-size: $font-size
  outline: none
  box-sizing: border-box

  &::placeholder
    color: $text-muted

  &:focus
    border-style: solid
    border-color: $button-border-hover

.search-results
  position: absolute
  top: 100%
  left: 0
  right: 0
  margin-top: $tiny
  border: 1px dashed
  border-radius: $border-radius
  border-color: $border
  background-color: $bg-element
  box-shadow: 0 4px 12px var(--shadow-color)
  z-index: 100
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
  transition: background-color 0.1s ease

  &:hover
    background-color: $bg-highlight-blue

.result-avatar
  width: 36px
  height: 36px
  border-radius: 50%
  object-fit: cover

.result-name
  color: $text
  font-weight: 500

.search-empty
  padding: $medium
  text-align: center
  color: $text-muted

.conversations-list
  display: flex
  flex-direction: column
  gap: $tiny

.empty-state
  display: flex
  flex-direction: column
  align-items: center
  justify-content: center
  padding: $big * 2
  text-align: center

.empty-icon
  color: $text-muted
  opacity: 0.5
  margin-bottom: $medium

.empty-title
  font-size: $title-font-size
  font-weight: 500
  color: $text
  margin-bottom: $small

.empty-hint
  color: $text-muted
</style>
