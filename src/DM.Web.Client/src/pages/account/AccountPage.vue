<template>
  <page-title>Настройки аккаунта</page-title>

  <div v-if="user" class="account-page">
    <!-- The account only; what the profile shows is edited on the profile. -->
    <p class="profile-link-hint">
      Аватар, статус, имя, пол, местоположение, день рождения, контакты и "О
      себе" правятся
      <router-link
        :to="{ name: 'profile', params: { username: user.username } }"
        class="profile-link"
        >в профиле</router-link
      >
      кнопкой "Редактировать"
    </p>

    <!-- Invitations first: they wait for an answer. -->
    <account-invitations-section />
    <account-settings-section />
    <account-notifications-section />
    <account-blacklist-section />
    <account-username-change-section :user="user" />
    <account-security-section
      :user="user"
      @password-changed="onPasswordChanged"
      @email-change-requested="onEmailChangeRequested"
    />
    <!-- Before the device list: confirming the factor ends every other session there. -->
    <account-two-factor-section />
    <account-sessions-section ref="sessionsSection" />
    <account-security-history-section ref="securityHistorySection" />
    <!-- The account is removed by moderation alone, so there is a sentence and no control. -->
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
 * The two blocks the security block changes from a distance. Each reads
 * itself and owns its data, so each exposes a re-read and the page holds it.
 */
const sessionsSection = ref<SessionsBlock | null>(null);
const securityHistorySection = ref<SecurityHistoryBlock | null>(null);

/** A new password ends every other session and writes the journal in one call; both blocks read again. */
function onPasswordChanged() {
  sessionsSection.value?.reload();
  securityHistorySection.value?.reload();
}

/** Asking for a new address writes the journal only: no session is touched. */
function onEmailChangeRequested() {
  securityHistorySection.value?.reload();
}
</script>

<style scoped lang="sass">
// The reading column of the account sections, narrower than the content area.
$account-column-width: 800px

.account-page
  max-width: $account-column-width
  margin: 0 auto

.profile-link-hint
  margin: 0 0 $medium 0
  padding: $small $medium
  background-color: $bg-element
  font-size: $secondary-font-size
  color: $text-muted

// A footnote, not a section: there is no control for a card to frame.
.removal-hint
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted
</style>
