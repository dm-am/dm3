<script setup lang="ts">
import { computed } from "vue";
import { useUserStore, useUiStore, useMessagingStore } from "@/stores";
import { storeToRefs } from "pinia";
import GuestActions from "@/views/layout/header/GuestActions.vue";
import SiteStatistics from "@/views/layout/header/SiteStatistics.vue";
import { ColorSchema, UserRole } from "@/api/models/community";

const userStore = useUserStore();
const { user } = storeToRefs(userStore);
const { signOut } = userStore;

const uiStore = useUiStore();
const { theme } = storeToRefs(uiStore);
const { toggleTheme } = uiStore;

const messagingStore = useMessagingStore();
const { totalUnreadCount } = storeToRefs(messagingStore);

const isDarkTheme = computed(() => theme.value === ColorSchema.Dark);

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
            :to="{ name: 'profile', params: { login: user.login } }"
            class="username"
            :class="{ 'has-unread': hasUnread }"
            data-testid="user-menu"
          >
            {{ user.login }} </router-link
          >&nbsp;<span class="muted">(</span
          ><router-link
            :to="{ name: 'messenger' }"
            title="Непрочитанные сообщения"
            data-testid="unread-messages"
            >{{ totalUnreadCount }}</router-link
          ><span class="muted">/</span
          ><router-link :to="{ name: 'notifications' }" title="Уведомления"
            data-testid="notifications"
            >0</router-link
          ><span class="muted">)</span>
          |
          <router-link :to="{ name: 'account' }" class="settings-link" title="Настройки аккаунта">⚙</router-link>
          |
          <a @click="signOut" data-testid="logout-button">Выйти</a>
        </template>
        <template v-else>
          <guest-actions />
        </template>
      </div>
    </div>
    <div class="top-menu">
      <router-link class="link" :to="{ name: 'about' }">О проекте</router-link>
      <router-link class="link" :to="{ name: 'rules' }">Правила</router-link>
      <router-link class="link" :to="{ name: 'games-active' }">Игры</router-link>
      <router-link class="link" :to="{ name: 'blogs' }">Блоги</router-link>
      <router-link class="link" :to="{ name: 'community' }"
        >Сообщество</router-link
      >
      <router-link class="link" :to="{ name: 'forum-index' }"
        >Форум</router-link
      >
      <router-link class="link" :to="{ name: 'globalChat' }">Чат</router-link>
      <router-link
        v-if="isMentor || isNewbie"
        class="link"
        :to="{ name: 'forum', params: { id: 'Для новичков' } }"
        >Для новичков</router-link
      >
      <router-link v-if="isModerator" class="link" :to="{ name: 'moderation' }"
        >Модерация</router-link
      >
    </div>
    <div class="stats-col">
      <site-statistics />
    </div>
    <div class="theme-col">
      <label
        class="theme-switch"
        :title="isDarkTheme ? 'Включить светлую тему' : 'Включить темную тему'"
      >
        <input type="checkbox" :checked="isDarkTheme" @change="toggleTheme" />
        <span class="slider" />
      </label>
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
  width: calc($sidebar-width - 80px)
  flex-shrink: 0
  display: flex
  align-items: center
  justify-content: flex-start
  padding-bottom: 9px
  padding-right: $big
  box-sizing: border-box
  align-self: stretch

.theme-col
  width: 80px
  flex-shrink: 0
  display: flex
  align-items: center
  justify-content: center
  padding-bottom: 9px
  padding-right: $big
  box-sizing: border-box
  align-self: stretch

.theme-switch
  position: relative
  display: inline-block
  width: 44px
  height: 22px
  cursor: pointer

  input
    opacity: 0
    width: 0
    height: 0

  .slider
    position: absolute
    top: 0
    left: 0
    right: 0
    bottom: 0
    border-radius: 22px
    background-color: $bg-page
    border: 1px solid $border

    &:before
      content: ''
      position: absolute
      width: 16px
      height: 16px
      left: 2px
      bottom: 2px
      border-radius: 50%
      background: $text-muted

    &:after
      content: ''
      position: absolute
      width: 12px
      height: 12px
      left: -1px
      bottom: 5px
      border-radius: 50%
      background: $bg-page
      z-index: 1
      opacity: 0

  input:checked + .slider
    background: $bg-page

  input:checked + .slider:before,
  input:checked + .slider:after
    transform: translateX(22px)

  input:checked + .slider:after
    opacity: 1

  &:hover .slider
    border-color: $text-muted

// Animation override (must be after theme rules)
html .theme-switch .slider:before
  transition: transform 0.3s ease-in-out, background 0.3s ease !important

html .theme-switch .slider:after
  transition: transform 0.3s ease-in-out !important
</style>
