<script setup lang="ts">
/**
 * The "Модерация" tab of a profile. The sections go by how often they are
 * wanted, the two actions on the account last; each gates itself on what the
 * server lets the viewer see or do.
 */
import { computed } from "vue";
import type { ModeratedProfile } from "@/shared/api/models/moderation";
import {
  useAuthStore,
  userIsAdmin,
  userIsSeniorModerator,
} from "@/entities/user";
import { BlockTitle } from "@/shared/ui/Layout";
import { ButtonRow } from "@/shared/ui/ButtonRow";
import ModerationViolations from "./ModerationViolations.vue";
import ModerationNotes from "./ModerationNotes.vue";
import ModerationWatch from "./ModerationWatch.vue";
import ModerationIpInfo from "./ModerationIpInfo.vue";
import ModerationLinkedProfiles from "./ModerationLinkedProfiles.vue";
import ModerationTwoFactor from "./ModerationTwoFactor.vue";
import ModerationAccountRemoval from "./ModerationAccountRemoval.vue";

const props = defineProps<{
  profile: ModeratedProfile;
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

const auth = useAuthStore();

// The actions on the account are a senior moderator's, the second factor an
// administrator's alone, and neither is offered on the viewer's own profile.
const showAccount = computed(
  () =>
    userIsSeniorModerator(auth.user) &&
    auth.user?.username.toLowerCase() !== props.targetUsername.toLowerCase(),
);
</script>

<template>
  <div class="moderation-tab">
    <ModerationViolations
      :violations="profile.violations"
      :permissions="profile.permissions"
      :target-username="targetUsername"
      @updated="emit('updated')"
    />
    <ModerationNotes
      v-if="profile.permissions.canViewModNotes"
      :notes="profile.moderatorNotes"
      :can-create="profile.permissions.canCreateModNote"
      :target-username="targetUsername"
      @updated="emit('updated')"
    />
    <ModerationWatch
      :under-watch="profile.isUnderModerationWatch"
      :permissions="profile.permissions"
      :target-username="targetUsername"
      @updated="emit('updated')"
    />
    <ModerationIpInfo
      v-if="profile.permissions.canViewIpAddresses"
      :email="profile.email"
      :ip-addresses="profile.ipAddresses"
      :login-history="profile.loginHistory"
    />
    <ModerationLinkedProfiles
      v-if="profile.permissions.canViewLinkedProfiles"
      :profiles="profile.linkedProfiles"
    />
    <section v-if="showAccount">
      <BlockTitle>Аккаунт</BlockTitle>
      <!-- One line on every width (ButtonRow). -->
      <ButtonRow>
        <ModerationTwoFactor
          v-if="userIsAdmin(auth.user)"
          :target-username="targetUsername"
          @updated="emit('updated')"
        />
        <ModerationAccountRemoval
          :target-username="targetUsername"
          @updated="emit('updated')"
        />
      </ButtonRow>
    </section>
  </div>
</template>

<style scoped lang="sass">
// The first section right under the tab strip, the rest a heading's step apart.
.moderation-tab > :first-child :deep(h2)
  margin-top: 0
</style>
