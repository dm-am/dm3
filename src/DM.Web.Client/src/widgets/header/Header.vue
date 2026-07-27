<script setup lang="ts">
import { computed, ref } from "vue";
import { useUserStore, userIsModerator } from "@/entities/user";
import { useMessagingStore } from "@/entities/message";
import { useNotificationStore } from "@/entities/notification";
import { useUiStore } from "@/shared/stores/ui";
import { storeToRefs } from "pinia";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import GuestActions from "./GuestActions.vue";
import SiteStatistics from "./SiteStatistics.vue";

const uiStore = useUiStore();
const userStore = useUserStore();
const { user } = storeToRefs(userStore);
const { signOut, signOutAll } = userStore;

const messagingStore = useMessagingStore();
const { totalUnreadCount } = storeToRefs(messagingStore);

const notificationStore = useNotificationStore();
const { unreadCount: unreadNotificationsCount } =
  storeToRefs(notificationStore);

const isModerator = computed(() => userIsModerator(user.value));

const hasUnread = computed(() => totalUnreadCount.value > 0);
const hasUnreadNotifications = computed(
  () => unreadNotificationsCount.value > 0,
);

// "Выйти" opens a small menu offering both a local sign-out and a
// sign-out-everywhere action (doc 4.2.1.1).
const logoutMenuOpen = ref(false);
function toggleLogoutMenu() {
  logoutMenuOpen.value = !logoutMenuOpen.value;
}
function closeLogoutMenu() {
  logoutMenuOpen.value = false;
}
async function handleSignOut() {
  closeLogoutMenu();
  await signOut();
}
async function handleSignOutAll() {
  closeLogoutMenu();
  await signOutAll();
}
</script>

<template>
  <header class="header">
    <!-- Compact mobile row (<= $bp-shell): burger + logo + counters/user menu.
         Greeting/"Выйти" and site statistics move into the burger drawer
         (App.vue) — desktop columns below stay untouched, just hidden here. -->
    <div class="mobile-bar">
      <button
        type="button"
        class="burger-btn"
        aria-haspopup="true"
        :aria-expanded="uiStore.isMobileDrawerOpen"
        aria-label="Открыть меню"
        @click="uiStore.toggleDrawer()"
      >
        <span class="burger-icon" aria-hidden="true">
          <span class="bar" /><span class="bar" /><span class="bar" />
        </span>
      </button>

      <router-link
        class="logo mobile-logo"
        :to="{ name: 'home' }"
        aria-label="Dungeon Master — на главную"
      />

      <div v-if="user && user.username" class="mobile-right">
        <Tooltip :text="`Непрочитанных сообщений: ${totalUnreadCount}`">
          <router-link
            :to="{ name: 'messenger' }"
            class="mobile-counter"
            :aria-label="`Сообщения, непрочитанных: ${totalUnreadCount}`"
          >
            <SvgIcon name="envelope" />
            <span v-if="hasUnread" class="counter-value">{{
              totalUnreadCount
            }}</span>
          </router-link>
        </Tooltip>
        <Tooltip
          :text="`Непросмотренных уведомлений: ${unreadNotificationsCount}`"
        >
          <router-link
            :to="{ name: 'notifications' }"
            class="mobile-counter"
            :class="{ 'has-unread': hasUnreadNotifications }"
            :aria-label="`Уведомления, непросмотренных: ${unreadNotificationsCount}`"
          >
            <SvgIcon name="bell" />
            <span v-if="hasUnreadNotifications" class="counter-value">{{
              unreadNotificationsCount
            }}</span>
          </router-link>
        </Tooltip>
      </div>
    </div>

    <div class="user-info">
      <router-link
        class="logo"
        :to="{ name: 'home' }"
        aria-label="Dungeon Master — на главную"
      />
      <div class="user-actions">
        <template v-if="user && user.username">
          Здравствуй,
          <router-link
            :to="{ name: 'profile', params: { username: user.username } }"
            class="username"
            :class="{ 'has-unread': hasUnread }"
            data-testid="user-menu"
          >
            {{ user.username }} </router-link
          >&nbsp;<span class="muted" aria-hidden="true">(</span
          ><Tooltip :text="`Непрочитанных сообщений: ${totalUnreadCount}`"
            ><router-link
              :to="{ name: 'messenger' }"
              :aria-label="`Непрочитанных сообщений: ${totalUnreadCount}`"
              data-testid="unread-messages"
              >{{ totalUnreadCount }}</router-link
            ></Tooltip
          ><span class="muted" aria-hidden="true">/</span
          ><Tooltip
            :text="`Непросмотренных уведомлений: ${unreadNotificationsCount}`"
            ><router-link
              :to="{ name: 'notifications' }"
              :class="{ 'has-unread': hasUnreadNotifications }"
              :aria-label="`Непросмотренных уведомлений: ${unreadNotificationsCount}`"
              data-testid="notifications"
              >{{ unreadNotificationsCount }}</router-link
            ></Tooltip
          ><span class="muted" aria-hidden="true">)</span>
          |
          <router-link class="settings-link" :to="{ name: 'account' }"
            >Настройки</router-link
          >
          |
          <span class="logout-menu">
            <button
              type="button"
              class="action-link"
              :aria-expanded="logoutMenuOpen"
              aria-haspopup="true"
              @click="toggleLogoutMenu"
              data-testid="logout-button"
            >
              Выйти
            </button>
            <template v-if="logoutMenuOpen">
              <!-- Invisible backdrop closes the menu on any outside click -->
              <button
                type="button"
                class="logout-backdrop"
                aria-hidden="true"
                tabindex="-1"
                @click="closeLogoutMenu"
              />
              <ul class="logout-dropdown" role="menu">
                <li role="none">
                  <button
                    type="button"
                    class="logout-item"
                    role="menuitem"
                    @click="handleSignOut"
                    data-testid="logout-current"
                  >
                    Выйти
                  </button>
                </li>
                <li role="none">
                  <button
                    type="button"
                    class="logout-item"
                    role="menuitem"
                    @click="handleSignOutAll"
                    data-testid="logout-all"
                  >
                    Выйти со всех устройств
                  </button>
                </li>
              </ul>
            </template>
          </span>
        </template>
        <template v-else>
          <GuestActions />
        </template>
      </div>
    </div>
    <nav class="top-menu" aria-label="Основные разделы">
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
      <router-link class="link" :to="{ name: 'global-chat' }">Чат</router-link>
      <!-- Always visible to everyone (owner rule) — the newbies board is the
           site's front door for prospective players, not a newbie-only tool. -->
      <router-link
        class="link"
        :to="{ name: 'forum', params: { alias: 'newbies' } }"
        >Для новичков</router-link
      >
      <router-link v-if="isModerator" class="link" :to="{ name: 'moderation' }"
        >Модерация</router-link
      >
    </nav>
    <div class="stats-col">
      <SiteStatistics />
    </div>
  </header>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

// Bottom padding that aligns the three header columns on a shared baseline.
$baseline-pad: 9px

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
  padding-bottom: $baseline-pad
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
  background: transparent url('@/assets/images/logos/logo.svg') no-repeat
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

// Keyboard-accessible control that looks exactly like a link
.action-link
  vertical-align: baseline
  +inline-link-button

.settings-link
  color: $link
  &:hover
    color: $link-hover

// "Выйти" dropdown: the trigger stays inline; the menu is absolutely
// positioned below it. A full-viewport transparent backdrop catches any
// outside click to dismiss it.
.logout-menu
  position: relative
  display: inline-block

.logout-backdrop
  position: fixed
  inset: 0
  z-index: 40
  border: none
  background: transparent
  padding: 0
  cursor: default

.logout-dropdown
  position: absolute
  top: 100%
  right: 0
  z-index: 41
  margin-top: $tiny
  min-width: 200px
  list-style: none
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15)
  overflow: hidden

.logout-item
  display: block
  width: 100%
  padding: $small $medium
  border: none
  background: none
  font: inherit
  font-size: $secondary-font-size
  text-align: left
  white-space: nowrap
  color: $text
  cursor: pointer

  &:hover
    background-color: $bg-element-accent
    color: $link-hover

.top-menu
  flex-grow: 1
  display: flex
  align-items: center
  align-self: stretch
  padding: 0 $big $baseline-pad
  column-gap: $big
  box-sizing: border-box

.link
  font-size: $menu-font-size
  font-weight: normal
  color: $link-nav
  // Never break a single menu item into two lines; on narrow viewports
  // the media tiers below compact spacing/typography instead
  white-space: nowrap

  &:hover
    text-decoration: none
    color: $link-nav-hover

.stats-col
  width: $sidebar-width
  flex-shrink: 0
  display: flex
  align-items: center
  justify-content: flex-start
  padding-bottom: $baseline-pad
  padding-right: $big
  box-sizing: border-box
  align-self: stretch

// Compact mobile row (<= $bp-shell) — hidden on desktop, see the media
// query at the bottom. Matches ScrollNav's bordered-square control idiom.
.mobile-bar
  display: none
  align-items: center
  gap: $small
  width: 100%
  height: $header-row-height
  padding: 0 $small
  box-sizing: border-box

.burger-btn
  display: flex
  align-items: center
  justify-content: center
  flex-shrink: 0
  width: 44px
  height: 44px
  border: none
  background: none
  color: $text
  cursor: pointer

.burger-icon
  display: flex
  flex-direction: column
  justify-content: space-between
  width: 20px
  height: 14px

.bar
  display: block
  height: 2px
  border-radius: 1px
  background-color: currentColor

// Overrides the desktop .logo sizing for the compact row (source order
// after .logo below gives it precedence at the same specificity)
.mobile-logo
  flex-shrink: 0
  height: 28px
  width: 130px
  margin: 0

.mobile-right
  display: flex
  align-items: center
  gap: $small
  flex-shrink: 0
  // Pushed to the far edge regardless of the logo's natural width — burger
  // and logo stay left-aligned next to each other
  margin-left: auto

.mobile-counter
  position: relative
  display: flex
  align-items: center
  justify-content: center
  width: 44px
  height: 44px
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  color: $text-muted
  box-sizing: border-box

  svg
    width: 18px
    height: 18px

  &:hover
    color: $text
    text-decoration: none

  &.has-unread
    color: $accent-green
    border-color: $accent-green

.counter-value
  position: absolute
  top: -4px
  right: -4px
  min-width: 16px
  height: 16px
  padding: 0 3px
  display: flex
  align-items: center
  justify-content: center
  box-sizing: border-box
  border-radius: 8px
  background-color: $accent-green
  color: $text-on-green
  font-size: 10px
  font-weight: bold
  line-height: 1

// Progressive top-menu compaction: the side columns are fixed at
// $sidebar-width each (they align with the page sidebars), so on
// 1366–1699px viewports only the menu can give up space. Each tier
// shrinks font/margins just enough to keep all eight links on one
// line and the stats column unclipped. >=1700px stays untouched.
@media (max-width: 1699px)
  .top-menu
    column-gap: $medium
  .link
    font-size: 20px

@media (max-width: 1489px)
  .top-menu
    column-gap: 10px
  .link
    font-size: 17px

@media (max-width: 1339px)
  .top-menu
    padding: 0 $medium $baseline-pad
    column-gap: $small
  .link
    font-size: 15px

// Shell breakpoint — the three-column desktop header (logo+greeting / menu /
// stats) no longer fits; it is replaced outright by the compact .mobile-bar
// (burger + logo + counters). The greeting/"Выйти" and SiteStatistics move
// into the burger drawer instead of reflowing here (App.vue renders them).
@media (max-width: $bp-shell)
  .header
    height: auto

  .mobile-bar
    display: flex

  .user-info,
  .top-menu,
  .stats-col
    display: none
</style>
