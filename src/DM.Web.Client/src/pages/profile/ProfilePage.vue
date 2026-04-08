<script setup lang="ts">
import { computed, toRef } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useCommunityStore, UserRole, type Username } from "@/entities/user";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useModeratedProfile } from "@/shared/lib/composables/useModeratedProfile";
import { useProfileEdit } from "@/shared/lib/composables/useProfileEdit";
import ModerationBlock from "./ModerationBlock.vue";
import ProfileHeader from "./ProfileHeader.vue";
import ProfileViolations from "./ProfileViolations.vue";
import ProfilePersonalInfo from "./ProfilePersonalInfo.vue";
import ProfileContacts from "./ProfileContacts.vue";
import UserProfileNote from "./UserProfileNote.vue";
import ProfileAbout from "./ProfileAbout.vue";
import ProfileGames from "./ProfileGames.vue";
import ProfileBlogs from "./ProfileBlogs.vue";
import ProfileBestPost from "./ProfileBestPost.vue";
import { ProfileSkeleton } from "@/shared/ui/Skeleton";

const route = useRoute();
const communityStore = useCommunityStore();
const { selectedUser: user, loadingProfile } = storeToRefs(communityStore);
const { trySelectProfile, fetchEditableUser } = communityStore;

// Profile not found state
const profileNotFound = computed(() => !loadingProfile.value && !user.value);

// System user (Robot Administrator) - special profile
const isSystemUser = computed(() => user.value?.role === UserRole.System);

// Username ref for composables
const username = computed(() => route.params.username as string);

// Moderated profile (for moderators)
const { moderatedProfile, refresh: refreshModeration } = useModeratedProfile(
  () => route.params.username as string,
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
} = useProfileEdit(toRef(() => route.params.username as string));

// Fetch profile data
useFetchData(async () => {
  const success = await trySelectProfile(route.params.username as Username);
  if (success && canEdit.value) {
    await fetchEditableUser(route.params.username as Username);
  }
}, [
  {
    param: (p) => p.username,
    callback: async (usernameParam) => {
      const success = await trySelectProfile(usernameParam as Username);
      if (success && canEdit.value) {
        await fetchEditableUser(usernameParam as Username);
      }
    },
  },
]);

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
  <div v-if="loadingProfile" class="profile-page">
    <ProfileSkeleton />
  </div>

  <!-- Not found state -->
  <div v-else-if="profileNotFound" class="profile-not-found">
    <h2>Пользователь не найден</h2>
    <p>
      Пользователь <strong>{{ route.params.username }}</strong> не существует
      или был удален.
    </p>
    <router-link to="/">На главную</router-link>
  </div>

  <!-- System user profile (Robot Administrator) -->
  <div v-else-if="isSystemUser && user" class="profile-page system-profile">
    <div class="system-profile-content">
      <div class="system-avatar">
        <span class="system-icon">🤖</span>
      </div>
      <h1 class="system-title">
        {{ user.username }}
        <span class="role-badge"><span class="bracket">[</span><span class="letter">Р</span><span class="bracket">]</span></span>
      </h1>
      <p class="system-description">
        Системный пользователь для автоматических действий.
        Автоматически выдает баны за нарушения и выполняет служебные операции.
      </p>
      <div class="system-info">
        <p>Этот профиль не принадлежит реальному человеку.</p>
      </div>
    </div>
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
    <profile-violations :username="username as Username" />

    <!-- Moderation block (for moderators only) -->
    <moderation-block
      v-if="moderatedProfile"
      :profile="moderatedProfile"
      :target-username="username"
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
        <profile-games :username="username" />

        <!-- Blogs section -->
        <profile-blogs :username="username" />

        <!-- Best post section -->
        <profile-best-post :username="username as Username" />
      </div>

      <div class="content-sidebar">
        <!-- Personal info -->
        <profile-personal-info
          :user="user"
          :isEditMode="isEditMode"
          @updateField="handleFieldUpdate"
        />

        <!-- Contacts -->
        <profile-contacts :user="user" :isEditMode="isEditMode" />

        <!-- Personal note (only for authenticated users) -->
        <user-profile-note :username="username as Username" />
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

// System user (Robot Administrator) profile
.system-profile
  .system-profile-content
    text-align: center
    padding: $big * 2
    max-width: 500px
    margin: 0 auto

  .system-avatar
    margin-bottom: $medium

  .system-icon
    font-size: 4rem

  .system-title
    margin: 0 0 $medium
    color: $text
    display: flex
    align-items: center
    justify-content: center
    gap: $small

  .role-badge
    .bracket
      color: $text-muted
    .letter
      font-weight: bold
      color: $accent-green

  .system-description
    color: $text-muted
    line-height: 1.6
    margin-bottom: $medium

  .system-info
    background: $bg-element-accent
    padding: $medium
    border-radius: $border-radius
    color: $text-muted
    font-size: 0.9em

    p
      margin: 0

@media (max-width: 768px)
  .profile-content
    grid-template-columns: 1fr
    gap: $medium

  .content-sidebar
    order: -1
</style>
