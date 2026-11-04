<script setup lang="ts">
import { computed, ref } from "vue";
import { useAuthStore } from "@/entities/user";
import { useSessionExit } from "@/features/auth";
import { useMessagingStore } from "@/entities/message";
import { useNotificationStore } from "@/entities/notification";
import { useMenuKeyboard } from "@/shared/lib/composables/useMenuKeyboard";
import { storeToRefs } from "pinia";
import { Tooltip } from "@/shared/ui/Tooltip";

const userStore = useAuthStore();
const { user } = storeToRefs(userStore);
const username = computed(() => user.value?.username ?? "");

const messagingStore = useMessagingStore();
const { totalUnreadCount } = storeToRefs(messagingStore);

const notificationStore = useNotificationStore();
const { unreadCount: unreadNotificationsCount } =
  storeToRefs(notificationStore);

const hasUnread = computed(() => totalUnreadCount.value > 0);
const hasUnreadNotifications = computed(
  () => unreadNotificationsCount.value > 0,
);

// "Выйти" opens a menu: a sign-out here and one everywhere.
const logoutMenuOpen = ref(false);
const logoutTrigger = ref<HTMLElement | null>(null);
const logoutMenu = ref<HTMLElement | null>(null);
function closeLogoutMenu() {
  logoutMenuOpen.value = false;
}

// Announced as a menu, so it answers Escape and the arrow keys like one.
const { handleMenuKeydown } = useMenuKeyboard({
  isOpen: () => logoutMenuOpen.value,
  close: closeLogoutMenu,
  trigger: logoutTrigger,
  menu: logoutMenu,
});

// Signing out is not a navigation, so leaving the page is part of the action.
const { signOut, signOutAll } = useSessionExit();

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
  <span class="greeting" data-testid="greeting">
    Здравствуй,
    <router-link
      :to="{ name: 'profile', params: { username } }"
      class="username"
      :class="{ 'has-unread': hasUnread }"
      data-testid="user-menu"
      >{{ username }}</router-link
    >&nbsp;<span class="muted" aria-hidden="true">(</span
    ><Tooltip :text="`Непрочитанных сообщений: ${totalUnreadCount}`"
      ><router-link
        :to="{ name: 'messenger' }"
        class="count count--messages"
        :aria-label="`Непрочитанных сообщений: ${totalUnreadCount}`"
        data-testid="unread-messages"
        >{{ totalUnreadCount }}</router-link
      ></Tooltip
    ><span class="muted" aria-hidden="true">/</span
    ><Tooltip :text="`Непрочитанных уведомлений: ${unreadNotificationsCount}`"
      ><router-link
        :to="{ name: 'notifications' }"
        class="count count--notifications"
        :class="{ 'has-unread': hasUnreadNotifications }"
        :aria-label="`Непрочитанных уведомлений: ${unreadNotificationsCount}`"
        data-testid="notifications"
        >{{ unreadNotificationsCount }}</router-link
      ></Tooltip
    ><span class="muted" aria-hidden="true">)</span>
    <!-- No "Настройки": the settings are reached from the reader's own profile -->
    |
    <span class="logout-menu" @keydown="handleMenuKeydown">
      <button
        ref="logoutTrigger"
        type="button"
        class="action-link"
        :aria-expanded="logoutMenuOpen"
        aria-haspopup="true"
        @click="logoutMenuOpen = !logoutMenuOpen"
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
        <ul ref="logoutMenu" class="logout-dropdown" role="menu">
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
  </span>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Links" as *

// The whole name, never cut: one line while it fits, wrapping between its words otherwise.
.greeting
  white-space: normal
  max-width: 100%
  text-align: center

  .muted
    color: $text-muted

  .has-unread
    font-weight: bold
    color: $accent-green
    &:hover
      color: $accent-green-hover

// A finger's field around each count, out of the flow; the two meet in the slash.
$count-target-height: 32px
$count-target-width: $tap-target-min
$count-target-meet: 2px

.count
  position: relative

  &::after
    content: ''
    position: absolute
    top: 50%
    height: $count-target-height
    width: calc(100% + #{$count-target-meet})
    min-width: $count-target-width
    transform: translateY(-50%)

.count--messages::after
  right: -$count-target-meet

.count--notifications::after
  left: -$count-target-meet

// Keyboard-accessible control that looks exactly like a link
.action-link
  vertical-align: baseline
  +inline-link-button

// The trigger stays inline, the menu under it; a transparent backdrop dismisses it.
.logout-menu
  position: relative
  display: inline-block

.logout-backdrop
  position: fixed
  inset: 0
  // The scale, not a number below it: lower, the menu went under every dropdown.
  z-index: $z-dropdown
  border: none
  background: transparent
  padding: 0
  cursor: default

.logout-dropdown
  position: absolute
  top: 100%
  right: 0
  z-index: $z-sticky
  margin-top: $tiny
  min-width: 200px
  list-style: none
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  box-shadow: $shadow-panel
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
</style>
