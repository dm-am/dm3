<template>
  <div id="app">
    <div class="main" ref="scroll">
      <div class="content-container">
        <div class="content-wrapper">
          <the-header />
          <div class="content-body">
            <div class="content-menu">
              <router-view name="menu" />
            </div>
            <div class="content">
              <router-view name="page" />
            </div>
            <div class="content-sidebar">
              <router-view name="sidebar" />
            </div>
          </div>
        </div>
        <the-footer />
      </div>
    </div>
    <modals-container />
  </div>
</template>

<script setup lang="ts">
import { useUiStore, useUserStore, useMessagingStore } from "@/stores";
import { onMounted, watch } from "vue";
import { ModalsContainer } from "vue-final-modal";
import TheHeader from "@/views/layout/TheHeader.vue";
import TheFooter from "@/views/layout/TheFooter.vue";
import { useGlobalSignalR } from "@/composables/useSignalR";
import { EventType } from "@/api/models/notifications/signalr";
import type { SignalRNotification } from "@/api/models/notifications/signalr";

const uiStore = useUiStore();
const userStore = useUserStore();
const messagingStore = useMessagingStore();
const { connect: connectSignalR, disconnect: disconnectSignalR, onNotification } = useGlobalSignalR();

// Map ColorSchema to CSS theme class (now 1:1 mapping)
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
    case EventType.NewChatMessage:
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
  messagingStore.fetchUnreadCount();

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
@import "src/assets/styles/Layout"
@import "src/assets/styles/Themes"

.main
  height: 100%
  min-height: 100%
  overflow-y: scroll
  background-color: $bg-page

.content-container
  position: relative
  min-height: 100%
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
  min-height: 100%
  min-width: $min-width
  padding-bottom: $footer-height

.content-body
  display: flex
  padding-bottom: $footer-height + $big

.content-menu
  width: $sidebar-width
  flex-shrink: 0
  padding-left: $big
  box-sizing: border-box

.content
  flex-grow: 1
  padding: 0 $big
  box-sizing: border-box

.content-sidebar
  width: $sidebar-width
  flex-shrink: 0
  padding-right: $big
  box-sizing: border-box
  @media (max-width: $min-width)
    display: none
</style>
