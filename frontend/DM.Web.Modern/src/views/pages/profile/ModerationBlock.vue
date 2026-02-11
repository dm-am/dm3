<script setup lang="ts">
import type { ModerationProfile } from "@/api/models/moderation";
import ModerationIpInfo from "./moderation/ModerationIpInfo.vue";
import ModerationLinkedProfiles from "./moderation/ModerationLinkedProfiles.vue";
import ModerationNotes from "./moderation/ModerationNotes.vue";
import ModerationViolations from "./moderation/ModerationViolations.vue";

const props = defineProps<{
  profile: ModerationProfile;
  targetLogin: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();
</script>

<template>
  <div class="moderation-block">
    <div class="moderation-block_header">
      <h3>Модерация</h3>
    </div>

    <moderation-ip-info
      v-if="profile.permissions.canViewIpAddresses"
      :email="profile.email"
      :ip-addresses="profile.ipAddresses"
      :login-history="profile.loginHistory"
    />

    <moderation-linked-profiles
      v-if="profile.permissions.canViewLinkedProfiles"
      :profiles="profile.linkedProfiles"
    />

    <moderation-notes
      v-if="profile.permissions.canViewModNotes"
      :notes="profile.moderatorNotes"
      :can-create="profile.permissions.canCreateModNote"
      :target-login="targetLogin"
      @updated="emit('updated')"
    />

    <moderation-violations
      :violations="profile.violations"
      :permissions="profile.permissions"
      :target-login="targetLogin"
      @updated="emit('updated')"
    />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.moderation-block
  margin-top: $big
  padding: $medium
  border: 1px solid $border-accent-red
  border-radius: $border-radius
  background: $bg-element

.moderation-block_header
  margin-bottom: $medium

  h3
    margin: 0
    color: $accent-red
    font-size: $title-font-size
</style>
