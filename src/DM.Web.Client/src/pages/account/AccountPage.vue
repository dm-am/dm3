<template>
  <page-title>Настройки аккаунта</page-title>

  <div v-if="user" class="account-page">
    <!--
      /account = только account-level: безопасность, сессии, уведомления,
      приватность, привязки ботов, смена логина.
      Аватар, статус, имя, биография, контакты — profile-attrs, редактируются
      на странице профиля (/profile/me → «Редактировать»).
    -->
    <p class="profile-link-hint">
      Аватар, статус, имя, биография и контакты редактируются на
      <router-link
        :to="{ name: 'profile', params: { username: user.username } }"
        class="profile-link"
      >
        странице профиля</router-link
      >.
    </p>

    <account-invitations-section />
    <account-security-section :user="user" />
    <account-sessions-section />
    <account-security-history-section />
    <account-settings-section :user="user" />
    <account-username-change-section :user="user" />
    <account-blacklist-section />
    <account-notifications-section />
    <account-bot-links-section />
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useUserStore } from "@/entities/user";
import AccountInvitationsSection from "./sections/AccountInvitationsSection.vue";
import AccountSecuritySection from "./sections/AccountSecuritySection.vue";
import AccountSessionsSection from "./sections/AccountSessionsSection.vue";
import AccountSecurityHistorySection from "./sections/AccountSecurityHistorySection.vue";
import AccountSettingsSection from "./sections/AccountSettingsSection.vue";
import AccountUsernameChangeSection from "./sections/AccountUsernameChangeSection.vue";
import AccountBlacklistSection from "./sections/AccountBlacklistSection.vue";
import AccountNotificationsSection from "./sections/AccountNotificationsSection.vue";
import AccountBotLinksSection from "./sections/AccountBotLinksSection.vue";

const userStore = useUserStore();
const user = computed(() => userStore.user);
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.account-page
  max-width: 800px
  margin: 0 auto

.profile-link-hint
  margin: 0 0 $medium 0
  padding: $small $medium
  background-color: $bg-element
  border-radius: $border-radius
  font-size: $secondary-font-size
  color: $text-muted

.profile-link
  color: $link
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline
</style>
