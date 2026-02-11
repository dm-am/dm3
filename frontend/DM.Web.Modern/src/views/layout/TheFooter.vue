<template>
  <div class="footer">
    <div class="credits">
      <div class="copyright-line">
        © DM.AM, 2007 &ndash; {{ currentYear }}. <span class="age">18+</span>
      </div>
      <div>
        Основатель сайта –
        <router-link :to="{ name: 'profile', params: { login: 'rakot' } }"
          >rakot</router-link
        >
      </div>
      <div>
        Администрирование и разработка –
        <router-link :to="{ name: 'profile', params: { login: 'Evengard' } }"
          >Evengard</router-link
        >,
        <router-link :to="{ name: 'profile', params: { login: 'SolohinLex' } }"
          >SolohinLex</router-link
        >
      </div>
      <div>
        Дизайн логотипа –
        <router-link :to="{ name: 'azur-profile' }">Azur</router-link>
      </div>
    </div>

    <!-- Пустая центральная колонка -->
    <div class="center-spacer"></div>

    <div class="social">
      <a class="social-icon" href="https://vk.com/club376658" target="_blank" title="Группа ВКонтакте">
        <svg viewBox="-3 -3 30 30"><path d="M15.684 0H8.316C1.592 0 0 1.592 0 8.316v7.368C0 22.408 1.592 24 8.316 24h7.368C22.408 24 24 22.408 24 15.684V8.316C24 1.592 22.391 0 15.684 0zm3.692 17.123h-1.744c-.66 0-.864-.525-2.05-1.727-1.033-1-1.49-1.135-1.744-1.135-.356 0-.458.102-.458.593v1.575c0 .424-.135.678-1.253.678-1.846 0-3.896-1.118-5.335-3.202C4.624 10.857 4 8.418 4 7.928c0-.254.102-.491.593-.491h1.744c.44 0 .61.203.78.678.847 2.489 2.27 4.674 2.853 4.674.22 0 .322-.102.322-.66V9.623c-.068-1.186-.695-1.287-.695-1.71 0-.203.17-.407.44-.407h2.744c.373 0 .508.203.508.643v3.473c0 .372.17.508.271.508.22 0 .407-.136.813-.542 1.253-1.406 2.143-3.574 2.143-3.574.119-.254.322-.491.763-.491h1.744c.525 0 .644.27.525.643-.22 1.017-2.354 4.031-2.354 4.031-.186.305-.254.44 0 .78.186.254.796.779 1.203 1.253.745.847 1.32 1.558 1.473 2.05.17.49-.085.744-.576.744z"/></svg>
      </a>
      <a class="social-icon" href="https://discord.gg/ez7FdeYgQv" target="_blank" title="Discord сервер">
        <svg viewBox="0 0 24 24"><path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028 14.09 14.09 0 0 0 1.226-1.994.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/></svg>
      </a>
      <a class="social-icon" href="https://www.youtube.com/@DungFM" target="_blank" title="YouTube канал">
        <svg viewBox="0 0 24 24"><path d="M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/></svg>
      </a>
    </div>

    <!-- Выезжающая плашка с переключателем региона -->
    <div
      v-if="isHydrated && canSwitch"
      class="region-panel"
      :class="{ 'is-open': regionPanelOpen }"
    >
      <div class="region-panel-tab" @click="toggleRegionPanel"></div>
      <div class="region-panel-content">
        <button
          type="button"
          class="region-switcher"
          :class="{ 'is-loading': isTransferring }"
          :title="switchTooltip"
          :aria-label="switchTooltip"
          :disabled="!canSwitch || isTransferring"
          @click="switchRegion"
        >{{ currentRegion?.id === 'main' ? '🌐︎' : '🇷🇺' }}</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useRegion } from "@/composables/useRegion";

const currentYear = ref<number>(new Date().getFullYear());

const {
  currentRegion,
  canSwitch,
  switchTooltip,
  switchRegion,
  isHydrated,
  isTransferring,
} = useRegion();

const regionPanelOpen = ref(false);
const toggleRegionPanel = () => {
  regionPanelOpen.value = !regionPanelOpen.value;
};
</script>

<style scoped lang="sass">
@import "src/assets/styles/Layout"
@import "src/assets/styles/Themes"

$icon-size: 40px

.footer
  display: flex
  align-items: stretch
  position: relative
  box-sizing: border-box
  min-height: $footer-height
  flex-shrink: 0
  overflow: hidden

  &:before
    content: ''
    position: absolute
    top: 0
    left: 0
    right: 0
    bottom: 0
    background: url('@/assets/images/footer_bg.gif') left bottom repeat-x
    background-size: auto $footer-height
    filter: $filter-invert

.credits
  position: relative
  width: $sidebar-width
  flex-shrink: 0
  padding-left: $big
  padding-top: 12px
  box-sizing: border-box
  font-size: $secondary-font-size
  line-height: 1.3
  color: $text-muted
  display: flex
  flex-direction: column
  justify-content: center
  align-self: stretch

.copyright-line
  margin-bottom: $minor

.age
  display: inline-flex
  align-items: center
  justify-content: center
  width: 16px
  height: 16px
  margin-left: $minor
  border-radius: 50%
  font-size: 8px
  vertical-align: baseline
  position: relative
  top: -2px
  border: 1px solid $text-muted
  color: $text-muted

.center-spacer
  flex-grow: 1
  position: relative
  padding-left: $big
  padding-right: $big
  padding-top: 12px
  box-sizing: border-box

.social
  position: relative
  width: $sidebar-width
  flex-shrink: 0
  padding-right: $big
  padding-top: 12px
  box-sizing: border-box
  display: flex
  align-items: center
  align-self: stretch
  justify-content: flex-start
  gap: $medium

.social-icon
  display: block
  width: $icon-size
  height: $icon-size
  color: $text-muted
  transition: opacity $animation-time ease

  &:hover
    opacity: 0.7

  svg
    width: 100%
    height: 100%
    fill: currentColor

// Выезжающая плашка снизу (фиксирована к низу экрана)
.region-panel
  position: absolute
  bottom: 0
  left: 50%
  transform: translateX(-50%) translateY(100%) translateY(-16px)
  z-index: 1000
  display: flex
  flex-direction: column
  align-items: center
  transition: transform 0.25s cubic-bezier(0.4, 0, 0.2, 1)

  // Закрыто + наведение на язычок — выдвигается чуть больше
  &:not(.is-open):has(.region-panel-tab:hover)
    transform: translateX(-50%) translateY(100%) translateY(-22px)

  // Наведение на язычок — стрелочка меняет цвет (всегда)
  &:has(.region-panel-tab:hover) .region-panel-tab::before
    border-color: $text-muted

  &.is-open
    transform: translateX(-50%) translateY(0)

    .region-panel-tab::before
      transform: rotate(45deg)
      margin-bottom: 2px

.region-panel-tab
  width: 24px
  height: 16px
  background: $bg-element
  border: 1px solid $border
  border-bottom: none
  border-radius: 8px 8px 0 0
  cursor: pointer
  display: flex
  align-items: center
  justify-content: center
  order: -1

  // Указатель вверх (закрыто), вниз (открыто)
  &::before
    content: ''
    width: 6px
    height: 6px
    border-right: 1.5px solid $border
    border-bottom: 1.5px solid $border
    transform: rotate(-135deg)
    margin-bottom: -3px
    transition: border-color 0.2s ease, transform 0.25s ease, margin-bottom 0.25s ease

.region-panel-content
  padding: $tiny $tiny
  background: $bg-element
  border: 1px solid $border
  border-bottom: none
  border-radius: $border-radius $border-radius 0 0

.region-switcher
  display: flex
  align-items: center
  justify-content: center
  background: none
  border: none
  cursor: pointer
  font-size: 1.4em
  width: 1.4em
  height: 1.4em
  overflow: hidden
  opacity: 0.7
  transition: opacity $animation-time ease
  color: $text-muted

  &:hover:not(:disabled)
    opacity: 1

  &:disabled
    cursor: not-allowed
    opacity: 0.4

  &.is-loading
    animation: pulse 1s infinite

@keyframes pulse
  0%, 100%
    opacity: 0.7
  50%
    opacity: 0.3
</style>
