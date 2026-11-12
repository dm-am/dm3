<template>
  <div id="app">
    <!-- The first Tab jumps past the header to <main> (.skip-link in Reset.sass). -->
    <a class="skip-link" href="#main">К основному содержимому</a>
    <div class="main" ref="scroll">
      <div class="content-container">
        <div class="content-wrapper" :class="{ 'with-columns': hasColumns }">
          <Header />
          <div class="content-body">
            <!-- A page a letter leads to drops both columns: it is one action,
                 and its guest card stands alone (layout.md, "Колонки шелла"). -->
            <aside v-if="hasColumns" class="sidebar-left">
              <router-view name="left" />
            </aside>
            <main id="main" class="content" tabindex="-1">
              <router-view name="page" />
            </main>
            <!-- Only a route with a right outlet has the column (router.ts). -->
            <aside v-if="hasColumns && hasRightColumn" class="sidebar-right">
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
    <!-- A picture of a post opened whole on a click (bbcodeImageViewer.ts). -->
    <ImageViewer />
  </div>
</template>

<script setup lang="ts">
import { useUiStore } from "@/shared/stores/ui";
import { useAuthStore, fetchUser, restoreSession } from "@/entities/user";
import { useMessagingStore } from "@/entities/message";
import { useNotificationStore } from "@/entities/notification";
import { setScrollContainer } from "@/shared/lib/scroll";
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { ModalsContainer } from "vue-final-modal";
import { Header } from "@/widgets/header";
import { useTimeZoneCheck } from "@/features/account-time-zone";
import { Footer } from "@/widgets/footer";
import { ToastContainer } from "@/shared/ui/Toast";
import { ImageViewer } from "@/shared/ui/ImageViewer";
import { ScrollNav } from "@/shared/ui/ScrollNav";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import { NotificationType } from "@/shared/api/models/notifications";
import type { SignalRNotification } from "@/shared/api/models/notifications";
import type { Theme } from "@/shared/api/models/personal";

const uiStore = useUiStore();
const userStore = useAuthStore();
const messagingStore = useMessagingStore();
const notificationStore = useNotificationStore();
const route = useRoute();

// Offers to move the account's time zone to the device's when the two differ.
useTimeZoneCheck();

// A page a letter leads to has neither column.
const hasColumns = computed(() => !route.meta.fromLetter);

// A route without the right outlet has no right column (router.ts).
const hasRightColumn = computed(() =>
  route.matched.some((record) => record.components?.right !== undefined),
);

const scroll = ref<HTMLElement | null>(null);
const {
  connect: connectSignalR,
  disconnect: disconnectSignalR,
  onNotification,
  isConnected: isSignalRConnected,
} = useGlobalSignalR();

// Mirrors --bg-page of ThemeVariables.css: the meta changes with the class
// swap, before any style recalculation.
const THEME_COLORS = {
  Light: "#ffffff",
  Dark: "#222222",
} satisfies Record<Theme, string>;

watch(
  () => uiStore.theme,
  (value, oldValue) => {
    const html = document.documentElement;
    // Suppress transitions during theme swap to prevent color fading
    html.classList.add("no-transitions");
    if (oldValue) {
      html.classList.remove(`theme_${oldValue}`);
    }
    html.classList.add(`theme_${value}`);
    // Force reflow — browser computes styles with transitions disabled
    void html.offsetHeight;
    html.classList.remove("no-transitions");

    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.setAttribute("content", THEME_COLORS[value]);
  },
  { immediate: true },
);

function handleNotification(notification: SignalRNotification) {
  switch (notification.eventType) {
    case NotificationType.NewMessage:
      // Nothing is stored behind this event, so the bell is left alone.
      messagingStore.fetchUnreadCount();
      break;
    case NotificationType.UserAvatarChanged:
      // Other users' avatars refresh on the next render: their URLs are immutable.
      handleAvatarChanged(notification.payload);
      break;
    case NotificationType.NewGlobalChatMessage:
    case NotificationType.GlobalChatEventStarted:
    case NotificationType.GlobalChatEventEnded:
      // Broadcast to every connection with no stored notification
      // (RealtimeNotificationProcessor.cs): the bell has nothing to re-read.
      break;
    default:
      // Any other event may be a stored notification; the store debounces bursts.
      notificationStore.fetchUnreadCount();
      break;
  }
}

function handleAvatarChanged(payload: Record<string, unknown>) {
  const { userId } = payload;
  if (!userId) return;
  const currentUserId = userStore.user?.id;
  if (currentUserId === userId) {
    // The whole user, so settings and visibility update too.
    fetchUser();
  }
}

// The hub keeps no backlog and the badges do not poll: re-read on reconnect.
watch(isSignalRConnected, (connected) => {
  if (!connected) return;
  messagingStore.fetchUnreadCount();
  notificationStore.fetchUnreadCount();
});

// The browser may close the socket of a background tab: re-read on return.
function refreshBadgesOnReturn() {
  if (document.hidden) return;
  messagingStore.fetchUnreadCount();
  notificationStore.fetchUnreadCount();
}

// Not tied to the first connect: the socket may come up on a later attempt.
onNotification(handleNotification);

watch(
  () => userStore.isAuthenticated,
  async (isAuthenticated) => {
    if (isAuthenticated) {
      await connectSignalR();
    } else {
      await disconnectSignalR();
    }
  },
);

onMounted(async () => {
  // Register the scrollable container so paging/router can scroll to top
  setScrollContainer(scroll.value);

  // The session cookie can outlive the stored user, so the server is asked.
  void restoreSession();
  messagingStore.fetchUnreadCount(true); // immediate on app start
  notificationStore.fetchUnreadCount(true); // immediate on app start

  document.addEventListener("visibilitychange", refreshBadgesOnReturn);

  if (userStore.isAuthenticated) {
    await connectSignalR();
  }
});

onUnmounted(() => {
  document.removeEventListener("visibilitychange", refreshBadgesOnReturn);
});
</script>

<style scoped lang="sass">
// Variables and Themes are injected globally via vite.config.ts additionalData

.main
  height: 100%
  overflow-y: scroll
  background-color: $bg-page

.content-container
  position: relative
  display: flex
  flex-direction: column
  min-height: 100vh

.content-wrapper
  position: relative
  flex: 1 0 auto

  // A page with columns is taller than the window once loaded: the footer waits
  // below the edge from the first frame, or the first block pushes it off.
  &.with-columns
    min-height: 100vh

.content-body
  display: flex
  padding-bottom: $big

aside.sidebar-left
  width: $sidebar-width
  flex-shrink: 0
  padding-left: $big

.content
  flex-grow: 1
  // Without it a page wider than the column pushes the right column off the window.
  min-width: 0
  padding: 0 $big

aside.sidebar-right
  width: $sidebar-width
  flex-shrink: 0
  padding-right: $big

aside.sidebar-left, .content, aside.sidebar-right
  box-sizing: border-box
</style>
