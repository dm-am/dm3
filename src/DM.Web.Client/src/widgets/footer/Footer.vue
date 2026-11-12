<template>
  <footer class="footer">
    <div class="credits" v-once>
      <div class="copyright-line">
        © Dungeon Master, {{ SITE_FOUNDED_YEAR }}–{{ currentYear }}.
        <span class="age">18+</span>
      </div>
      <div>
        Основатель сайта —
        <router-link :to="{ name: 'profile', params: { username: 'rakot' } }"
          >rakot</router-link
        >
      </div>
      <div>
        Администрирование —
        <router-link :to="{ name: 'profile', params: { username: 'Evengard' } }"
          >Evengard</router-link
        >,
        <router-link
          :to="{ name: 'profile', params: { username: 'SolohinLex' } }"
          >SolohinLex</router-link
        >
      </div>
      <div>
        Помощь в разработке —
        <router-link :to="{ name: 'profile', params: { username: 'Rayzen' } }"
          >Rayzen</router-link
        >
      </div>
      <div>
        Дизайн логотипа —
        <router-link
          v-slot="{ href, navigate, isActive, isExactActive }"
          :to="{ name: 'profile', params: { username: 'Azur' } }"
          custom
        >
          <a
            :href="href"
            :class="{
              'router-link-active': isActive,
              'router-link-exact-active': isExactActive,
            }"
            :aria-current="isExactActive ? 'page' : undefined"
            @click="rickroll($event, navigate)"
            >Azur</a
          >
        </router-link>
      </div>
    </div>

    <nav class="legal" aria-label="Правовая информация" v-once>
      <div>
        <router-link :to="{ name: 'user-agreement' }"
          >Пользовательское соглашение</router-link
        >
      </div>
      <div>
        <router-link :to="{ name: 'privacy-policy' }"
          >Политика конфиденциальности</router-link
        >
      </div>
    </nav>
  </footer>
</template>

<script setup lang="ts">
import { SITE_FOUNDED_YEAR } from "@/shared/config/site";
import { siteTime } from "@/shared/lib/utils/datetime";

const currentYear = siteTime().year();

// A ctrl, shift or meta click, or a button other than the left one, is handed
// to the router link and opens the real profile; any other click, Alt among
// them, goes to the video. One handler
// decides, so a plain click never starts the router's navigation to the profile
// (its route chunk and requests) on the way out to the video.
function rickroll(event: MouseEvent, navigate: (event: MouseEvent) => unknown) {
  if (event.ctrlKey || event.shiftKey || event.metaKey || event.button !== 0) {
    navigate(event);
    return;
  }
  event.preventDefault();
  window.location.href = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
}
</script>

<style scoped lang="sass">
// The column look of .credits and .legal; each sets its own padding side.
%footer-column
  position: relative
  width: $sidebar-width
  flex-shrink: 0
  padding-top: $medium + $minor
  box-sizing: border-box
  font-size: $secondary-font-size
  line-height: 1.3
  // One step darker than $text-muted: the strip's overlay eats some contrast.
  color: color-mix(in srgb, $text-muted 60%, $text)
  display: flex
  flex-direction: column
  justify-content: center
  align-self: stretch

$footer-strip: url('@/assets/images/decorations/footer-decoration.png')

.footer
  display: flex
  align-items: stretch
  justify-content: space-between
  position: relative
  box-sizing: border-box
  min-height: $footer-height
  flex-shrink: 0
  overflow: hidden

  // Two layers of one picture: the torn edge at the desktop height, the paper as tall as the footer.
  &:before
    content: ''
    position: absolute
    top: 0
    left: 0
    right: 0
    bottom: 0
    background-image: $footer-strip, $footer-strip
    background-position: left top
    background-repeat: repeat-x
    background-size: auto $footer-height, auto 100%
    filter: $filter-invert

.credits
  @extend %footer-column
  padding-left: $big

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
  border: 1px solid currentColor
  color: inherit

.legal
  @extend %footer-column
  padding-right: $big

// One step between every pair of rows, so the lines read as one list.
.legal > div:not(:last-child)
  margin-bottom: $small
</style>
