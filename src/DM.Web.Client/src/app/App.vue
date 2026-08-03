<template>
  <div id="app">
    <!-- Skip-link: accessibility helper for keyboard/screen-reader users.
         Visually hidden until it receives keyboard focus (first Tab on any
         page); Enter moves focus straight to <main id="main">, skipping the
         header (logo, menu, statistics). Mouse users never see it.
         Styles live in Reset.sass (.skip-link). -->
    <a class="skip-link" href="#main">К основному содержимому</a>
    <div class="main" ref="scroll">
      <div class="content-container">
        <div class="content-wrapper">
          <Header />
          <div class="content-body">
            <aside class="sidebar-left">
              <router-view name="left" />
            </aside>
            <main id="main" class="content" tabindex="-1">
              <router-view name="page" />
            </main>
            <aside class="sidebar-right">
              <router-view name="right" />
            </aside>
          </div>
        </div>
        <Footer />
      </div>
    </div>
    <modals-container />
    <ToastContainer />
    <ScrollNav />

    <!-- Mobile off-canvas navigation drawer (<= $bp-shell, burger in Header).
         Content mirrors the desktop header + left sidebar: auth block, main
         site nav, then the same contextual left-sidebar panels (GamePanel /
         BlogPanel / ModerationPanel + boards) so nothing is unreachable on
         mobile. Mounted only while open. -->
    <MobileDrawer v-model="uiStore.isMobileDrawerOpen">
      <div class="drawer-auth">
        <template v-if="userStore.user && userStore.user.username">
          <div class="drawer-greeting">
            Здравствуй,
            <router-link
              :to="{
                name: 'profile',
                params: { username: userStore.user.username },
              }"
              class="username"
              >{{ userStore.user.username }}</router-link
            >
            <span class="separator"> | </span>
            <router-link class="drawer-settings-link" :to="{ name: 'account' }"
              >Настройки</router-link
            >
          </div>
          <div class="drawer-logout">
            <button
              type="button"
              class="action-link"
              data-testid="drawer-logout-button"
              @click="signOut"
            >
              Выйти
            </button>
            <button
              type="button"
              class="action-link"
              data-testid="drawer-logout-all-button"
              @click="signOutAll"
            >
              Выйти со всех устройств
            </button>
          </div>
        </template>
        <template v-else>
          <GuestActions />
        </template>
      </div>

      <nav class="drawer-nav" aria-label="Основные разделы">
        <ul class="drawer-nav-list">
          <li>
            <span class="muted" aria-hidden="true">- </span
            ><router-link :to="{ name: 'about' }">О проекте</router-link>
          </li>
          <li>
            <span class="muted" aria-hidden="true">- </span
            ><router-link :to="{ name: 'rules' }">Правила</router-link>
          </li>
          <li>
            <span class="muted" aria-hidden="true">- </span
            ><router-link :to="{ name: 'games' }">Игры</router-link>
          </li>
          <li>
            <span class="muted" aria-hidden="true">- </span
            ><router-link :to="{ name: 'blogs' }">Блоги</router-link>
          </li>
          <li>
            <span class="muted" aria-hidden="true">- </span
            ><router-link :to="{ name: 'community' }">Сообщество</router-link>
          </li>
          <li>
            <span class="muted" aria-hidden="true">- </span
            ><router-link :to="{ name: 'forum-index' }">Форум</router-link>
          </li>
          <li>
            <span class="muted" aria-hidden="true">- </span
            ><router-link :to="{ name: 'global-chat' }">Чат</router-link>
          </li>
          <!-- Always visible to everyone (owner rule) — mirrors the desktop
               top menu. -->
          <li>
            <span class="muted" aria-hidden="true">- </span
            ><router-link :to="{ name: 'forum', params: { alias: 'newbies' } }"
              >Для новичков</router-link
            >
          </li>
          <li v-if="isModerator">
            <span class="muted" aria-hidden="true">- </span
            ><router-link :to="{ name: 'moderation' }">Модерация</router-link>
          </li>
        </ul>
      </nav>

      <div class="drawer-context">
        <LeftSidebar />
      </div>
    </MobileDrawer>
  </div>
</template>

<script setup lang="ts">
import { useUiStore } from "@/shared/stores/ui";
import {
  useAuthStore,
  userIsModerator,
  signOut,
  signOutAll,
  fetchUser,
} from "@/entities/user";
import { useMessagingStore } from "@/entities/message";
import { useNotificationStore } from "@/entities/notification";
import { setScrollContainer } from "@/shared/lib/scroll";
import { computed, onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { ModalsContainer } from "vue-final-modal";
import { Header, GuestActions } from "@/widgets/header";
import { Footer } from "@/widgets/footer";
import { LeftSidebar } from "@/widgets/sidebar";
import { ToastContainer } from "@/shared/ui/Toast";
import { ScrollNav } from "@/shared/ui/ScrollNav";
import { MobileDrawer } from "@/shared/ui/Drawer";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import { NotificationType } from "@/shared/api/models/notifications";
import type { SignalRNotification } from "@/shared/api/models/notifications";

const uiStore = useUiStore();
const userStore = useAuthStore();
const messagingStore = useMessagingStore();
const notificationStore = useNotificationStore();
const route = useRoute();

const isModerator = computed(() => userIsModerator(userStore.user));

// Close the drawer on every navigation (path change) — reopening it after
// following a link would be surprising, and a stale-open drawer would keep
// `.main` scroll locked underneath the newly navigated page.
watch(
  () => route.path,
  () => uiStore.closeDrawer(),
);

// Template ref for the scrollable content container (".main")
const scroll = ref<HTMLElement | null>(null);
const {
  connect: connectSignalR,
  disconnect: disconnectSignalR,
  onNotification,
} = useGlobalSignalR();

// Map Theme to CSS theme class (now 1:1 mapping)
const themeToClass = (theme: string) => theme;

// theme-color meta content per theme — mirrors --bg-page in
// assets/styles/ThemeVariables.css (Light: #fff, Dark: #222222). Kept as a
// hardcoded map (not read from CSS) since the meta must update synchronously
// with the class swap below, before any style recalculation.
const THEME_COLORS: Record<string, string> = {
  Light: "#ffffff",
  Dark: "#222222",
};

watch(
  () => uiStore.theme,
  (value, oldValue) => {
    const html = document.documentElement;
    // Suppress transitions during theme swap to prevent color fading
    html.classList.add("no-transitions");
    if (oldValue) {
      html.classList.remove(`theme_${themeToClass(oldValue)}`);
    }
    html.classList.add(`theme_${themeToClass(value)}`);
    // Force reflow — browser computes styles with transitions disabled
    void html.offsetHeight;
    html.classList.remove("no-transitions");

    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.setAttribute("content", THEME_COLORS[value]);
  },
  { immediate: true },
);

// Handle SignalR notifications
function handleNotification(notification: SignalRNotification) {
  switch (notification.eventType) {
    case NotificationType.NewMessage:
      // Refresh the unread messages count when a new message arrives. The push
      // is addressed to the participants of the chat; global chat has its own
      // broadcast event and never arrives here. Nothing is stored behind this
      // event, which is why the notification bell is deliberately left alone.
      messagingStore.fetchUnreadCount();
      break;
    case NotificationType.UserAvatarChanged:
      // Live avatar update in open tabs. If the current user changed,
      // refresh the user store. Other users' avatars in chat/
      // comments update on the next render when the DOM repaints
      // (URLs in the payload are immutable, browser cache safe).
      handleAvatarChanged(notification.payload);
      break;
    default:
      // Every other event type is a candidate for a persisted user
      // notification (likes, comments, invitations, subscriptions, etc.) —
      // refresh the header badge. Debounced inside the store so bursts of
      // events collapse into a single request.
      notificationStore.fetchUnreadCount();
      break;
    // Add more event handlers as needed
  }
}

function handleAvatarChanged(payload: Record<string, unknown>) {
  const userId = payload.userId as string | undefined;
  if (!userId) return;
  const currentUserId = userStore.user?.id;
  if (currentUserId === userId) {
    // Re-fetch current user — guarantees settings/visibility
    // are picked up too, not only the picture.
    fetchUser();
  }
}

// Connect/disconnect SignalR based on authentication state
watch(
  () => userStore.isAuthenticated,
  async (isAuthenticated) => {
    if (isAuthenticated) {
      const connected = await connectSignalR();
      if (connected) {
        onNotification(handleNotification);
      }
    } else {
      await disconnectSignalR();
    }
  },
);

onMounted(async () => {
  // Register the scrollable container so paging/router can scroll to top
  setScrollContainer(scroll.value);

  // The user is already initialized from localStorage in the store
  // Refresh data from the server in parallel
  fetchUser();
  messagingStore.fetchUnreadCount(true); // immediate on app start
  notificationStore.fetchUnreadCount(true); // immediate on app start

  // Connect to SignalR if already authenticated
  if (userStore.isAuthenticated) {
    const connected = await connectSignalR();
    if (connected) {
      onNotification(handleNotification);
    }
  }
});
</script>

<style scoped lang="sass">
// Variables from Layout and Themes are injected globally via vite.config.ts additionalData
@import "@/assets/styles/Inputs"

.main
  height: 100%
  min-height: 100%
  overflow-y: scroll
  background-color: $bg-page

.content-container
  position: relative
  display: flex
  flex-direction: column
  min-height: 100vh
  min-width: $bp-shell
  // Below the sidebar breakpoint the left sidebar is hidden and the right
  // one reflows into the normal document flow, so the rigid min-width
  // would only force a horizontal scrollbar — relax it
  @media (max-width: $bp-shell)
    min-width: 0
  &:before
    content: ''
    position: absolute
    left: 0
    right: 0
    top: 0
    height: $header-height
    background: url('@/assets/images/decorations/header-decoration.png') left top repeat-x
    background-size: auto $header-height
    filter: $filter-invert
    // The header collapses to a single compact row on mobile (Header.vue) —
    // shrink the decorative strip to match, so it doesn't bleed into the
    // page content below a much shorter header
    @media (max-width: $bp-shell)
      height: $header-row-height
      background-size: auto $header-row-height

.content-wrapper
  position: relative
  flex: 1 0 auto
  min-width: $bp-shell
  @media (max-width: $bp-shell)
    min-width: 0

.content-body
  display: flex
  padding-bottom: $big
  @media (max-width: $bp-shell)
    // Left sidebar is hidden (reachable via the burger drawer instead); the
    // right sidebar reflows below <main> in normal document order (it is
    // already the last DOM sibling of the three columns)
    flex-direction: column

.sidebar-left
  width: $sidebar-width
  flex-shrink: 0
  padding-left: $big
  box-sizing: border-box
  // Unreachable as a fixed column below the shell breakpoint — its content
  // (GamePanel/BlogPanel/ModerationPanel + boards) is reachable instead via
  // the mobile burger drawer (LeftSidebar rendered a second time there)
  @media (max-width: $bp-shell)
    display: none

.content
  flex-grow: 1
  padding: 0 $big
  box-sizing: border-box

.sidebar-right
  width: $sidebar-width
  flex-shrink: 0
  padding-right: $big
  box-sizing: border-box
  @media (max-width: $bp-shell)
    width: auto
    padding: 0 $big

// Mobile drawer content (auth block / main nav / contextual left panel) —
// mirrors Header.vue's greeting idiom and the sidebar "- " link idiom.
.drawer-auth
  padding-bottom: $medium
  margin-bottom: $medium
  border-bottom: 1px solid $border
  font-size: $secondary-font-size
  color: $text

.username
  font-weight: bold

.separator
  color: $text-muted

.action-link
  vertical-align: baseline
  +inline-link-button

.drawer-settings-link
  color: $link
  &:hover
    color: $link-hover

// Both sign-out options stacked as full-width tap targets under the greeting
// (still inside the bordered .drawer-auth section).
.drawer-logout
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $tiny
  margin-top: $small

  .action-link
    min-height: 44px

.drawer-nav
  margin-bottom: $medium
  padding-bottom: $medium
  border-bottom: 1px solid $border

.drawer-nav-list
  list-style: none

  li
    // >= 44px tap target via padding (not font-size bloat)
    display: flex
    align-items: center
    min-height: 44px

.muted
  color: $text-muted

// LeftSidebar's own scoped styles handle .blocks/list-item presentation —
// this wrapper is just a grouping hook for the drawer layout.
.drawer-context
  min-height: 0
</style>
