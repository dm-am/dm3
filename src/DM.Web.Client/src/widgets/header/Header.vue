<script setup lang="ts">
import { computed } from "vue";
import { useUserStore, UserRole } from "@/entities/user";
import { useMessagingStore } from "@/entities/message";
import { storeToRefs } from "pinia";
import { Tooltip } from "@/shared/ui/Tooltip";
import GuestActions from "./GuestActions.vue";
import SiteStatistics from "./SiteStatistics.vue";

const userStore = useUserStore();
const { user } = storeToRefs(userStore);
const { signOut } = userStore;

const messagingStore = useMessagingStore();
const { totalUnreadCount } = storeToRefs(messagingStore);

const isModerator = computed(
  () =>
    user.value?.roles?.some((r) =>
      [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(
        r,
      ),
    ) ?? false,
);

const isMentor = computed(
  () =>
    user.value?.roles?.some((r) =>
      [UserRole.Admin, UserRole.SeniorModerator, UserRole.Mentor].includes(r),
    ) ?? false,
);

const isNewbie = computed(() => user.value?.isNewbie ?? false);

const hasUnread = computed(() => totalUnreadCount.value > 0);
</script>

<template>
  <div class="header">
    <div class="user-info">
      <router-link class="logo" :to="{ name: 'home' }" />
      <div class="user-actions">
        <template v-if="user">
          Здравствуй,
          <router-link
            :to="{ name: 'profile', params: { username: user.username } }"
            class="username"
            :class="{ 'has-unread': hasUnread }"
            data-testid="user-menu"
          >
            {{ user.username }} </router-link
          >&nbsp;<span class="muted">(</span
          ><Tooltip :text="`Непрочитанных сообщений: ${totalUnreadCount}`"
            ><router-link
              :to="{ name: 'messenger' }"
              data-testid="unread-messages"
              >{{ totalUnreadCount }}</router-link
            ></Tooltip
          ><span class="muted">/</span
          ><Tooltip text="Уведомления"
            ><router-link
              :to="{ name: 'notifications' }"
              data-testid="notifications"
              >0</router-link
            ></Tooltip
          ><span class="muted">)</span>
          |
          <Tooltip text="Настройки аккаунта"
            ><router-link :to="{ name: 'account' }" class="settings-link"
              >⚙</router-link
            ></Tooltip
          >
          |
          <a @click="signOut" data-testid="logout-button">Выйти</a>
        </template>
        <template v-else>
          <GuestActions />
        </template>
      </div>
    </div>
    <div class="top-menu">
      <router-link class="link" :to="{ name: 'about' }">О проекте</router-link>
      <router-link class="link" :to="{ name: 'rules' }">Правила</router-link>
      <router-link class="link" :to="{ name: 'games' }">Игры</router-link>
      <router-link class="link" :to="{ name: 'blogs' }">Блоги</router-link>
      <router-link class="link" :to="{ name: 'community' }"
        >Сообщество</router-link
      >
      <router-link class="link" :to="{ name: 'forum-index' }"
        >Форум</router-link
      >
      <router-link class="link" :to="{ name: 'globalChat' }">Чат</router-link>
      <router-link
        class="link"
        :to="{ name: 'forum', params: { alias: 'newbies' } }"
        >Для новичков</router-link
      >
      <router-link v-if="isModerator" class="link" :to="{ name: 'moderation' }"
        >Модерация</router-link
      >
    </div>
    <div class="stats-col">
      <SiteStatistics />
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Layout"
@import "src/assets/styles/Themes"

.header
  position: relative
  display: flex
  align-items: center
  box-sizing: border-box
  height: $header-height

.user-info
  width: $sidebar-width
  flex-shrink: 0
  padding-left: $big
  padding-bottom: 9px
  box-sizing: border-box
  white-space: nowrap
  cursor: default
  align-self: stretch
  display: flex
  flex-direction: column
  justify-content: center

.logo
  display: block
  margin-bottom: $tiny
  margin-left: -5px
  height: $header-row-height
  width: 275px
  background: transparent url('@/assets/images/logo.svg') no-repeat
  background-size: contain

.user-actions
  font-size: $secondary-font-size
  color: $text

  .muted
    color: $text-muted

  .has-unread
    font-weight: bold
    color: $accent-green
    &:hover
      color: $accent-green-hover

  .settings-link
    text-decoration: none
    &:hover
      color: $link-hover

.top-menu
  flex-grow: 1
  display: flex
  align-items: center
  align-self: stretch
  padding: 0 $big 9px
  box-sizing: border-box

.link
  margin-right: $big
  font-size: $menu-font-size
  font-weight: normal
  transition: color $animation-time ease
  color: $link-nav

  &:hover
    text-decoration: none
    color: $link-nav-hover

  &.create
    padding: $minor $small
    border-radius: $border-radius
    transition: background $animation-time ease, border-color $animation-time ease, color $animation-time ease
    background: $bg-element
    border: 1px solid $border
    &:hover
      background: $bg-highlight-blue

.stats-col
  width: $sidebar-width
  flex-shrink: 0
  display: flex
  align-items: center
  justify-content: flex-start
  padding-bottom: 9px
  padding-right: $big
  box-sizing: border-box
  align-self: stretch
</style>
