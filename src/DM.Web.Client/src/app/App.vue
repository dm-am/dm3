<template>
  <div id="app">
    <div class="main" ref="scroll">
      <div class="content-container">
        <div class="content-wrapper">
          <Header />
          <div class="content-body">
            <div class="sidebar-left">
              <router-view name="left" />
            </div>
            <div class="content">
              <router-view name="page" />
            </div>
            <div class="sidebar-right">
              <router-view name="right" />
            </div>
          </div>
        </div>
        <Footer />
      </div>
    </div>
    <modals-container />
    <ToastContainer />
    <ScrollNav />
  </div>
</template>

<script setup lang="ts">
import { useUiStore } from "@/shared/stores/ui";
import { useUserStore } from "@/entities/user";
import { useMessagingStore } from "@/entities/message";
import { onMounted, watch } from "vue";
import { ModalsContainer } from "vue-final-modal";
import { Header } from "@/widgets/header";
import { Footer } from "@/widgets/footer";
import { ToastContainer } from "@/shared/ui/Toast";
import { ScrollNav } from "@/shared/ui/ScrollNav";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import { EventType } from "@/shared/api/models/notifications/signalr";
import type { SignalRNotification } from "@/shared/api/models/notifications/signalr";

const uiStore = useUiStore();
const userStore = useUserStore();
const messagingStore = useMessagingStore();
const {
  connect: connectSignalR,
  disconnect: disconnectSignalR,
  onNotification,
} = useGlobalSignalR();

// Map Theme to CSS theme class (now 1:1 mapping)
const themeToClass = (theme: string) => theme;

watch(
  () => uiStore.theme,
  (value, oldValue) => {
    const html = document.querySelector("html")!;
    if (oldValue) {
      html.classList.remove(`theme_${themeToClass(oldValue)}`);
    }
    html.classList.add(`theme_${themeToClass(value)}`);
  },
  { immediate: true },
);

// Handle SignalR notifications
function handleNotification(notification: SignalRNotification) {
  switch (notification.eventType) {
    case EventType.NewMessage:
    case EventType.NewGlobalChatMessage:
      // Refresh unread count when new message arrives
      messagingStore.fetchUnreadCount();
      break;
    // Add more event handlers as needed
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
  // User уже инициализирован из localStorage в store
  // Параллельно обновляем данные с сервера
  userStore.fetchUser();
  messagingStore.fetchUnreadCount(true); // immediate on app start

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
  min-width: $min-width
  &:before
    content: ''
    position: absolute
    left: 0
    right: 0
    top: 0
    height: $header-height
    background: url('@/assets/images/header_bg.gif') left top repeat-x
    background-size: auto $header-height
    filter: $filter-invert

.content-wrapper
  position: relative
  flex: 1 0 auto
  min-width: $min-width

.content-body
  display: flex
  padding-bottom: $big

.sidebar-left
  width: $sidebar-width
  flex-shrink: 0
  padding-left: $big
  box-sizing: border-box

.content
  flex-grow: 1
  padding: 0 $big
  box-sizing: border-box

.sidebar-right
  width: $sidebar-width
  flex-shrink: 0
  padding-right: $big
  box-sizing: border-box
  @media (max-width: $min-width)
    display: none
</style>
