<template>
  <div class="user-autocomplete">
    <template v-if="!selectedUser">
      <input
        v-model="searchQuery"
        type="text"
        class="autocomplete-input"
        :placeholder="placeholder"
        @input="onInput"
        @focus="showDropdown = true"
        @blur="onBlur"
      />
      <div
        v-if="showDropdown && suggestions.length > 0"
        class="autocomplete-dropdown"
      >
        <div
          v-for="user in suggestions"
          :key="user.login"
          class="autocomplete-item"
          @mousedown.prevent="selectUser(user)"
        >
          <img
            :src="user.smallPictureUrl || defaultAvatar"
            class="user-avatar"
          />
          <span class="user-login">{{ user.login }}</span>
        </div>
      </div>
    </template>
    <div v-else class="selected-user">
      <img
        :src="selectedUser.smallPictureUrl || defaultAvatar"
        class="user-avatar"
      />
      <span class="user-login">{{ selectedUser.login }}</span>
      <a class="clear-btn" @click="clearSelection">&times;</a>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onUnmounted } from "vue";
import type { User } from "@/api/models/community";
import communityApi from "@/api/requests/communityApi";
import defaultAvatar from "@/assets/images/userpic.png";

const props = defineProps<{
  modelValue: string;
  placeholder?: string;
}>();

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const searchQuery = ref("");
const suggestions = ref<User[]>([]);
const showDropdown = ref(false);
const selectedUser = ref<User | null>(null);
let searchTimeout: ReturnType<typeof setTimeout> | null = null;
let blurTimeout: ReturnType<typeof setTimeout> | null = null;

onUnmounted(() => {
  if (searchTimeout) clearTimeout(searchTimeout);
  if (blurTimeout) clearTimeout(blurTimeout);
});

async function search(query: string) {
  if (query.length < 1) {
    suggestions.value = [];
    return;
  }
  const { data } = await communityApi.searchUsers(query);
  suggestions.value = data?.resources ?? [];
}

function onInput() {
  if (selectedUser.value) {
    selectedUser.value = null;
    emit("update:modelValue", "");
  }
  if (searchTimeout) clearTimeout(searchTimeout);
  searchTimeout = setTimeout(() => search(searchQuery.value), 300);
}

function selectUser(user: User) {
  selectedUser.value = user;
  searchQuery.value = "";
  suggestions.value = [];
  showDropdown.value = false;
  emit("update:modelValue", user.login);
}

function clearSelection() {
  selectedUser.value = null;
  searchQuery.value = "";
  emit("update:modelValue", "");
}

function onBlur() {
  blurTimeout = setTimeout(() => {
    showDropdown.value = false;
  }, 200);
}

watch(
  () => props.modelValue,
  (newVal) => {
    if (!newVal) {
      selectedUser.value = null;
      searchQuery.value = "";
    }
  },
);
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.user-autocomplete
  position: relative

.autocomplete-input
  width: 100%
  padding: $small
  box-sizing: border-box
  font-family: inherit
  font-size: inherit
  background-color: $input-bg-overlay
  color: $text
  border: 1px dashed $border

  &:focus
    outline: none
    border-style: solid
    border-color: $button-border-hover

.autocomplete-dropdown
  position: absolute
  top: 100%
  left: 0
  right: 0
  z-index: 100
  max-height: 200px
  overflow-y: auto
  border-radius: $border-radius
  background: $bg-element
  border: 1px solid $border

.autocomplete-item
  display: flex
  align-items: center
  padding: $small
  cursor: pointer
  border-bottom: 1px solid $border

  &:hover
    background: $bg-highlight-blue

  &:last-child
    border-bottom: none

.user-avatar
  width: 24px
  height: 24px
  border-radius: 50%
  margin-right: $small

.user-login
  color: $text

.selected-user
  display: flex
  align-items: center
  padding: $small
  background-color: $input-bg-overlay
  border: 1px dashed $border

.clear-btn
  margin-left: auto
  cursor: pointer
  color: $text-muted
  &:hover
    color: $text
</style>
