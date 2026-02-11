<script setup lang="ts">
import { computed, toRef } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useCommunityStore } from "@/stores/community";
import { type UserLogin } from "@/api/models/community";
import { useFetchData } from "@/composables/useFetchData";
import { useModerationProfile } from "@/composables/useModerationProfile";
import { useProfileEdit } from "@/composables/useProfileEdit";
import ModerationBlock from "./ModerationBlock.vue";
import ProfileHeader from "./ProfileHeader.vue";
import ProfileViolations from "./ProfileViolations.vue";
import ProfilePersonalInfo from "./ProfilePersonalInfo.vue";
import ProfileContacts from "./ProfileContacts.vue";
import ProfilePersonalNote from "./ProfilePersonalNote.vue";
import ProfileAbout from "./ProfileAbout.vue";
import ProfileGames from "./ProfileGames.vue";
import ProfileBlogs from "./ProfileBlogs.vue";
import ProfileBestPost from "./ProfileBestPost.vue";

const route = useRoute();
const communityStore = useCommunityStore();
const { selectedUser: user, loadingProfile } = storeToRefs(communityStore);
const { trySelectProfile, fetchEditableUser } = communityStore;

// Profile not found state
const profileNotFound = computed(() => !loadingProfile.value && !user.value);

// Login ref for composables
const login = computed(() => route.params.login as string);

// Moderation profile (for moderators)
const { moderationProfile, refresh: refreshModeration } = useModerationProfile(
  () => route.params.login as string,
);

// Edit mode management
const {
  isEditMode,
  canEdit,
  hasChanges,
  isSaving,
  saveError,
  toggleEditMode,
  setField,
  saveChanges,
  cancelEdit,
} = useProfileEdit(toRef(() => route.params.login as string));

// Fetch profile data
useFetchData(
  async () => {
    const success = await trySelectProfile(route.params.login as UserLogin);
    if (success && canEdit.value) {
      await fetchEditableUser(route.params.login as UserLogin);
    }
  },
  [
    {
      param: (p) => p.login,
      callback: async (loginParam) => {
        const success = await trySelectProfile(loginParam as UserLogin);
        if (success && canEdit.value) {
          await fetchEditableUser(loginParam as UserLogin);
        }
      },
    },
  ],
);

// Handle field updates from child components
const handleFieldUpdate = (field: string, value: string) => {
  setField(field as any, value);
};

// Handle save
const handleSave = async () => {
  await saveChanges();
};
</script>

<template>
  <!-- Loading state -->
  <div v-if="loadingProfile" class="profile-loading">
    <p>Загрузка профиля...</p>
  </div>

  <!-- Not found state -->
  <div v-else-if="profileNotFound" class="profile-not-found">
    <h2>Пользователь не найден</h2>
    <p>Пользователь <strong>{{ route.params.login }}</strong> не существует или был удалён.</p>
    <router-link to="/">На главную</router-link>
  </div>

  <!-- Profile content -->
  <div v-else-if="user" class="profile-page">
    <!-- Error message -->
    <div v-if="saveError" class="save-error">
      {{ saveError }}
    </div>

    <!-- Header with avatar, name, status, actions -->
    <profile-header
      :user="user"
      :isEditMode="isEditMode"
      :canEdit="canEdit"
      :hasChanges="hasChanges"
      :isSaving="isSaving"
      @toggleEdit="toggleEditMode"
      @save="handleSave"
      @cancel="cancelEdit"
      @updateField="handleFieldUpdate"
    />

    <!-- Violations (public bans/warnings) -->
    <profile-violations :login="(login as UserLogin)" />

    <!-- Moderation block (for moderators only) -->
    <moderation-block
      v-if="moderationProfile"
      :profile="moderationProfile"
      :target-login="login"
      @updated="refreshModeration"
    />

    <!-- Main content area -->
    <div class="profile-content">
      <div class="content-primary">
        <!-- About section -->
        <profile-about
          :user="user"
          :isEditMode="isEditMode"
          @updateField="handleFieldUpdate"
        />

        <!-- Games section -->
        <profile-games :login="login" />

        <!-- Blogs section -->
        <profile-blogs :login="login" />

        <!-- Best post section -->
        <profile-best-post :login="(login as UserLogin)" />
      </div>

      <div class="content-sidebar">
        <!-- Personal info -->
        <profile-personal-info
          :user="user"
          :isEditMode="isEditMode"
          @updateField="handleFieldUpdate"
        />

        <!-- Contacts -->
        <profile-contacts
          :user="user"
          :isEditMode="isEditMode"
        />

        <!-- Personal note (only for authenticated users) -->
        <profile-personal-note :login="(login as UserLogin)" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-page
  max-width: $grid-step * 240
  margin: 0 auto

.save-error
  background: rgba($accent-red, 0.1)
  border: 1px solid $accent-red
  color: $accent-red
  padding: $small $medium
  border-radius: $border-radius
  margin-bottom: $medium

.profile-content
  display: grid
  grid-template-columns: 1fr $grid-step * 60
  gap: $big

.content-primary
  min-width: 0

.content-sidebar
  min-width: 0

.profile-loading
  text-align: center
  padding: $big
  color: $text-muted

.profile-not-found
  text-align: center
  padding: $big
  max-width: 400px
  margin: 0 auto

  h2
    margin: 0 0 $medium
    color: $text

  p
    margin: 0 0 $medium
    color: $text-muted
    line-height: 1.5

  a
    font-weight: bold

@media (max-width: 768px)
  .profile-content
    grid-template-columns: 1fr
    gap: $medium

  .content-sidebar
    order: -1
</style>
