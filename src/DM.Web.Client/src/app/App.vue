<template>
  <div id="app">
    <!-- Skip-link: accessibility helper for keyboard/screen-reader users.
         Visually hidden until it receives keyboard focus (first Tab on any
         page); Enter moves focus straight to <main id="main">, skipping the
         header (logo, menu, statistics). Mouse users never see it.
         Styles live in Reset.sass (.skip-link). -->
    <a class="skip-link" href="#main" :inert="columnOut"
      >К основному содержимому</a
    >
    <div class="main" ref="scroll">
      <div class="content-container">
        <div class="content-wrapper">
          <Header :inert="columnOut" />
          <div class="content-body">
            <!-- A narrow screen keeps both columns in one column that slides
                 in over the page from the button of the header (MobileDrawer),
                 a view for each: the left, led by the menu of the header, and
                 the right, led by its statistics, the two things of the header
                 that have no room beside the logo. Tabs in the bar of the
                 column switch the two, the tabs of the site. The same columns,
                 not copies: they load with the page, and what they hold stays
                 as it was between openings and between the views, how far
                 each view is scrolled included.

                 The right view is the right column of the site at every
                 address, a page that has none on a desktop included (the
                 messenger, the room of posts): there the page stands in its
                 place, and here the column is the way to what it holds.

                 A page a letter leads to has none of the columns' blocks, at
                 any width: its column holds the menu and the statistics
                 alone, as its header does on a desktop. -->
            <MobileDrawer
              v-if="narrow"
              ref="drawer"
              v-model:open="drawerOpen"
              label="Меню"
            >
              <template #bar>
                <Tabs
                  ref="drawerTabs"
                  v-model="drawerView"
                  :tabs="DRAWER_VIEWS"
                  variant="headings"
                  aria-label="Колонки сайта"
                  id-prefix="shell-drawer"
                  class="drawer-tabs"
                />
              </template>
              <div
                v-show="drawerView === 'left'"
                :id="drawerTabs?.panelId('left')"
                role="tabpanel"
                :aria-labelledby="drawerTabs?.tabId('left')"
                class="sidebar-left drawer-view"
              >
                <MainMenu class="drawer-lead" />
                <router-view v-if="!route.meta.fromLetter" name="left" />
              </div>
              <div
                v-show="drawerView === 'right'"
                :id="drawerTabs?.panelId('right')"
                role="tabpanel"
                :aria-labelledby="drawerTabs?.tabId('right')"
                class="sidebar-right drawer-view"
              >
                <SiteStatistics class="drawer-lead" />
                <RightSidebar v-if="!route.meta.fromLetter" />
              </div>
            </MobileDrawer>
            <!-- A page a letter leads to stands alone: both columns are
                 dropped, not emptied. Left in place they keep their 330px on a
                 1024px window and leave the card 285px of the 352px it asks
                 for, which is where the heading of a guest card starts
                 wrapping. -->
            <aside
              v-else-if="!narrow && !route.meta.fromLetter"
              class="sidebar-left"
            >
              <router-view name="left" />
            </aside>
            <main id="main" class="content" tabindex="-1" :inert="columnOut">
              <router-view name="page" />
            </main>
            <!-- Only a route that mounts a right column has one: the messenger
                 lays its list in its place, and the room of posts is read
                 across its width (router.ts). An empty column there would
                 keep 330 of the window from the page. -->
            <aside
              v-if="!narrow && hasRightColumn && !route.meta.fromLetter"
              class="sidebar-right"
            >
              <router-view name="right" />
            </aside>
          </div>
        </div>
        <Footer :inert="columnOut" />
      </div>
    </div>
    <modals-container />
    <ToastContainer />
    <ScrollNav />
    <!-- A picture of a post opened whole on a click (bbcodeInteractive.ts). -->
    <ImageViewer />
  </div>
</template>

<script setup lang="ts">
import { useUiStore, type SidePanel } from "@/shared/stores/ui";
import { useAuthStore, fetchUser, restoreSession } from "@/entities/user";
import { useMessagingStore } from "@/entities/message";
import { useNotificationStore } from "@/entities/notification";
import { setScrollContainer } from "@/shared/lib/scroll";
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { ModalsContainer } from "vue-final-modal";
import { Header, MainMenu, SiteStatistics } from "@/widgets/header";
import { RightSidebar } from "@/widgets/sidebar";
import { useTimeZoneCheck } from "@/features/account-time-zone";
import { Footer } from "@/widgets/footer";
import { ToastContainer } from "@/shared/ui/Toast";
import { ImageViewer } from "@/shared/ui/ImageViewer";
import { ScrollNav } from "@/shared/ui/ScrollNav";
import { MobileDrawer } from "@/shared/ui/Drawer";
import { Tabs, type TabItem } from "@/shared/ui/Tabs";
import { useShellMode } from "@/shared/lib/composables/useShellMode";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import { NotificationType } from "@/shared/api/models/notifications";
import type { SignalRNotification } from "@/shared/api/models/notifications";

const uiStore = useUiStore();
const userStore = useAuthStore();
const messagingStore = useMessagingStore();
const notificationStore = useNotificationStore();
const route = useRoute();

// The account keeps a time zone and the device is in one. The page shows a
// signed-in reader's time in the first, and when the two disagree, this offers
// to move the first to the second.
useTimeZoneCheck();

const { narrow } = useShellMode();

// Whether the page has a right column on a desktop. The router says it: a
// route without one is a record without the right outlet (router.ts).
const hasRightColumn = computed(() =>
  route.matched.some((record) => record.components?.right !== undefined),
);

// The two views of the sliding column, named by what they hold: the menu of
// the site with the left column, what is known of the site with the right
// one.
const DRAWER_VIEWS: readonly TabItem<SidePanel>[] = [
  { value: "left", label: "Меню" },
  { value: "right", label: "О сайте" },
];

// The column is out while the store names a view of it, and opens on the
// left one, where the button of the header stands.
const drawerOpen = computed({
  get: () => uiStore.openedPanel !== null,
  set: (open: boolean) => {
    if (open) uiStore.openPanel("left");
    else uiStore.closePanel();
  },
});

// The view shown. It stays as it was while the column slides back, so the
// column does not change under the reader on its way out.
const drawerView = ref<SidePanel>("left");
watch(
  () => uiStore.openedPanel,
  (side) => {
    if (side) drawerView.value = side;
  },
  { immediate: true },
);
watch(drawerView, (side) => {
  if (uiStore.openedPanel) uiStore.openPanel(side);
});

const drawer = ref<InstanceType<typeof MobileDrawer> | null>(null);

// Each view keeps its own scroll. The two scroll in the one element of the
// column, so a view would otherwise open as deep as the other was left, with
// what leads it above the top of the screen. The scroll of the view going is
// read before the switch reaches the page, and the view coming is put back
// where it was left, at its top the first time.
const viewScroll: Partial<Record<SidePanel, number>> = {};
watch(drawerView, async (side, was) => {
  const content = drawer.value?.content;
  if (!content) return;
  viewScroll[was] = content.scrollTop;
  await nextTick();
  content.scrollTop = viewScroll[side] ?? 0;
});

const drawerTabs = ref<{
  tabId(value: SidePanel): string;
  panelId(value: SidePanel): string;
} | null>(null);

// The column is out over the page: the rest of the shell takes no focus and no
// press until it goes back.
const columnOut = computed(() => narrow.value && uiStore.openedPanel !== null);

// The column goes back on every change of address: the browser's "back" leaves
// the page with the column out, and a column left out would hold the new page
// still under it. A press on a link inside the column closes it on its own
// (MobileDrawer), the address changed or not.
watch(
  () => route.fullPath,
  () => uiStore.closePanel(),
);

// A screen widened past the shell breakpoint puts the columns back on the page.
watch(
  () => uiStore.openedPanel !== null && !narrow.value,
  (stray) => {
    if (stray) uiStore.closePanel();
  },
  { immediate: true },
);

// Template ref for the scrollable content container (".main")
const scroll = ref<HTMLElement | null>(null);
const {
  connect: connectSignalR,
  disconnect: disconnectSignalR,
  onNotification,
  isConnected: isSignalRConnected,
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
    case NotificationType.NewGlobalChatMessage:
    case NotificationType.GlobalChatEventStarted:
    case NotificationType.GlobalChatEventEnded:
      // The broadcast set, mirrored from Broadcast in
      // src/DM.Web.API/Realtime/RealtimeNotificationProcessor.cs: these three
      // reach every open connection, guests included, and store no
      // notification for anybody. The global chat page reads them through its
      // own subscription; the bell has nothing to re-read, and falling through
      // to the default branch turned one busy chat into a request per message
      // from every tab on the site.
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

// Every push addressed to this tab while the socket was down is gone: the hub
// keeps no backlog. The global chat page catches up on its own, the two badges
// have no polling fallback at all, so they are re-read here on every transition
// into the connected state. Both stores debounce, so the first connect of the
// page costs one request and no more.
watch(isSignalRConnected, (connected) => {
  if (!connected) return;
  messagingStore.fetchUnreadCount();
  notificationStore.fetchUnreadCount();
});

// A backgrounded tab misses pushes it can never get back: the browser is free
// to close the socket, and the hub keeps no backlog for a reconnect either. So
// both badges are re-read when the tab returns to the screen (the idiom
// GlobalChatPage and SiteStatistics already use for what they show). Both
// stores debounce, so a run of tab switches costs one request.
function refreshBadgesOnReturn() {
  if (document.hidden) return;
  messagingStore.fetchUnreadCount();
  notificationStore.fetchUnreadCount();
}

// Subscribed once, for the life of the application, and never behind the result
// of a connect attempt. Handlers live in a Set keyed by function identity, so
// registering is idempotent; the socket, on the other hand, can come up on an
// attempt later than the first, and the automatic reconnect of the client only
// revives a connection that already started. Tying the handler to the outcome of
// the first connect left the two badges frozen for the whole SPA session whenever
// that attempt failed. The global chat page subscribes the same way.
onNotification(handleNotification);

// Connect/disconnect SignalR based on authentication state
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

  // Who the viewer is: the one stored in the browser is refreshed, and with
  // nobody stored the server is asked once, since the session cookie can
  // outlive the stored copy. The route guard may have asked already; it is
  // the same request either way.
  void restoreSession();
  messagingStore.fetchUnreadCount(true); // immediate on app start
  notificationStore.fetchUnreadCount(true); // immediate on app start

  document.addEventListener("visibilitychange", refreshBadgesOnReturn);

  // Connect to SignalR if already authenticated
  if (userStore.isAuthenticated) {
    await connectSignalR();
  }
});

onUnmounted(() => {
  document.removeEventListener("visibilitychange", refreshBadgesOnReturn);
});
</script>

<style scoped lang="sass">
// Variables from Layout and Themes are injected globally via vite.config.ts additionalData

.main
  height: 100%
  min-height: 100%
  overflow-y: scroll
  background-color: $bg-page

// The narrowest the page is laid out: the width a phone lays it out at
// (index.html), whose middle column is as wide as the middle column of a
// desktop of 1366 (642, with no scrollbar), the shell's gutters on either
// side. A window narrower than that scrolls the page sideways, as a phone
// shows the whole of it smaller: no width has a layout of its own below this
// one. Kept in step with index.html by src/phoneLayout.spec.ts.
$narrowest-layout: 706px

.content-container
  position: relative
  display: flex
  flex-direction: column
  min-height: 100vh
  // No more than the narrowest layout: a min-width of $bp-shell bit between
  // 1001 and 1014 with a classic scrollbar, where the query counts the
  // scrollbar and the page does not, and the page went 14 sideways.
  min-width: $narrowest-layout

.content-wrapper
  position: relative
  flex: 1 0 auto

.content-body
  display: flex
  padding-bottom: $big
  @media (max-width: $bp-shell)
    // Both side columns are out of the flow below the shell breakpoint
    // (MobileDrawer, fixed at their edges), and the page is the one column
    // left.
    flex-direction: column

aside.sidebar-left
  width: $sidebar-width
  flex-shrink: 0
  padding-left: $big
  box-sizing: border-box

.content
  flex-grow: 1
  // A flex item will not shrink below its content without this, and the
  // middle column of a 1024 window was pushed to 1213 by a page wider than
  // its 349: the right column went off the edge of the window. The column is
  // as wide as the shell leaves it; a page wider than that is the page's own
  // to fit.
  min-width: 0
  padding: 0 $big
  box-sizing: border-box

aside.sidebar-right
  width: $sidebar-width
  flex-shrink: 0
  padding-right: $big
  box-sizing: border-box

// The tabs of the two views stand in the bar of the sliding column as its
// heading, with no margin of their own under them: the bar centres them.
.drawer-tabs.tabs
  margin: 0

// What leads each view, the menu of the header or its statistics, stands
// apart from the column under it as the header stands apart from the page.
.drawer-lead
  margin-bottom: $big
</style>
