<template>
  <page-title>Настройки аккаунта</page-title>

  <div v-if="user" class="account-page">
    <!--
      /account = account-level only: security, sessions, notifications
      (and the bots that deliver them), privacy, username change.
      Avatar, status, name, bio, contacts are profile attrs, edited
      on the profile page (/profile/me → "Редактировать").
    -->
    <p class="profile-link-hint">
      Аватар, статус, имя, биография и контакты редактируются на
      <router-link
        :to="{ name: 'profile', params: { username: user.username } }"
        class="profile-link"
      >
        странице профиля</router-link
      >
    </p>

    <account-invitations-section />
    <account-security-section
      :user="user"
      @password-changed="onPasswordChanged"
      @email-change-requested="onEmailChangeRequested"
    />
    <!-- Between the credentials it belongs with and the device list its
         confirmation empties: confirming the factor ends every other session,
         and the block below is where that shows. -->
    <account-two-factor-section />
    <account-sessions-section ref="sessionsSection" />
    <account-security-history-section ref="securityHistorySection" />
    <account-settings-section :user="user" />
    <account-username-change-section :user="user" />
    <account-blacklist-section />
    <account-notifications-section />
    <!-- The account is removed by moderation and by nothing else, so this page
         has nothing to offer but the sentence saying who removes it and on
         what. A control here would promise an action the site no longer
         performs. -->
    <p class="removal-hint">
      Аккаунт удаляет модерация по обращению в поддержку
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useAuthStore } from "@/entities/user";
import AccountInvitationsSection from "./sections/AccountInvitationsSection.vue";
import AccountSecuritySection from "./sections/AccountSecuritySection.vue";
import AccountTwoFactorSection from "./sections/AccountTwoFactorSection.vue";
import AccountSessionsSection from "./sections/AccountSessionsSection.vue";
import AccountSecurityHistorySection from "./sections/AccountSecurityHistorySection.vue";
import AccountSettingsSection from "./sections/AccountSettingsSection.vue";
import AccountUsernameChangeSection from "./sections/AccountUsernameChangeSection.vue";
import AccountBlacklistSection from "./sections/AccountBlacklistSection.vue";
import AccountNotificationsSection from "./sections/AccountNotificationsSection.vue";

const userStore = useAuthStore();
const user = computed(() => userStore.user);

type SessionsBlock = InstanceType<typeof AccountSessionsSection>;
type SecurityHistoryBlock = InstanceType<typeof AccountSecurityHistorySection>;

/**
 * The two blocks that the security block changes from a distance.
 *
 * Both read themselves once, in onMounted, and both own their data: the page
 * has none of it to hand down. So each of them exposes a way to read itself
 * again and the page holds the handle. It is the smallest of the ways the
 * project has to do this: a store would move state
 * nobody else reads out of the block that reads it, and a `:key` would throw
 * a whole block away to fetch what one request brings back.
 */
const sessionsSection = ref<SessionsBlock | null>(null);
const securityHistorySection = ref<SecurityHistoryBlock | null>(null);

/**
 * Changing the password ends every other session and writes the journal entry
 * in one server call (PasswordChangeService), and neither of those shows in the
 * block that did it. Until both are re-read, the device list still offers to
 * sign out of devices that are already signed out.
 */
function onPasswordChanged() {
  sessionsSection.value?.reload();
  securityHistorySection.value?.reload();
}

/**
 * Asking for a new address writes a journal entry too, and only that: the
 * address itself moves when the link in the letter is followed, and no session
 * is touched, so the device list has nothing to re-read.
 */
function onEmailChangeRequested() {
  securityHistorySection.value?.reload();
}
</script>

<style scoped lang="sass">
.account-page
  max-width: 800px
  margin: 0 auto

.profile-link-hint
  margin: 0 0 $medium 0
  padding: $small $medium
  background-color: $bg-element
  font-size: $secondary-font-size
  color: $text-muted

// A footnote, not a section: no card, no heading and no rounding, because
// there is no control here for any of that to frame.
.removal-hint
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted
</style>
