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

const uiStore = useUiStore();
const userStore = useUserStore();
const messagingStore = useMessagingStore();

// Map ColorSchema to CSS theme class
const themeToClass = (theme: string) => {
  return theme === "Dark" ? "Night" : "Modern";
};

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

onMounted(async () => {
  await userStore.fetchUser();
  messagingStore.fetchUnreadCount();
});
</script>

<style scoped lang="sass">
@import "src/assets/styles/Layout"
@import "src/assets/styles/Themes"

.main
  height: 100%
  min-height: 100%
  overflow-y: scroll
  +theme(background-color, $background)

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
