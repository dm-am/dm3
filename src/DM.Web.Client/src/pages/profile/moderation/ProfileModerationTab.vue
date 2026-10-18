<script setup lang="ts">
/**
 * The "Модерация" tab of a profile: what a moderator opens a profile for,
 * under the headings every section of the profile has ("Контакты").
 *
 * It used to be a red, collapsed bar of its own between the actions of the
 * profile and its contacts, which pushed the profile down by a screen and a
 * half when opened and kept the everyday actions under the addresses, the
 * linked accounts and an editor that stood open. The strip of the tabs is the
 * navigation of a profile, and moderation is one more place in it.
 *
 * The sections go by how often they are wanted: the violations and their two
 * actions, the notes, the watch, then the sign-ins and the accounts they tie
 * this one to, and last, apart and behind their windows, the two actions on
 * the account itself. Each section gates itself on what the server lets the
 * viewer see or do, and a section with nothing to show is not drawn.
 */
import { computed } from "vue";
import type { ModeratedProfile } from "@/shared/api/models/moderation";
import { userIsSeniorModerator } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
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

// The two actions on the account are a senior moderator's and an
// administrator's (the second factor the administrator's alone), and neither
// is offered on the viewer's own profile. Each button gates itself; the
// heading over them is drawn when at least one of them is.
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
    <section v-if="showAccount" class="mod-section">
      <BlockTitle>Аккаунт</BlockTitle>
      <!-- Both in one line on every width (ButtonRow). -->
      <ButtonRow class="mod-account_actions">
        <ModerationTwoFactor
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
// The first section stands right under the strip of the tabs, the rest a
// heading's own step apart (BlockTitle).
.moderation-tab > :first-child :deep(h2)
  margin-top: 0
</style>
