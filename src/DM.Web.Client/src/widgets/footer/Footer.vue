<template>
  <footer class="footer">
    <!-- Static content - render once (currentYear computed once at component creation) -->
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
          :to="{ name: 'profile', params: { username: 'Azur' } }"
          @click="rickroll"
          >Azur</router-link
        >
      </div>
    </div>

    <!-- The other address of the site used to lead this column; it now has its
         own sidebar block (widgets/sidebar/SiteAddresses.vue), where it is met
         before anything stops answering rather than at the bottom of a page
         nobody scrolls to in a hurry. -->
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

// Static value - year doesn't change during session
const currentYear = new Date().getFullYear();

// Easter egg: rickroll disguised as profile link
// Only on plain left-click; Ctrl/Shift/Meta+click opens real profile in new tab
function rickroll(event: MouseEvent) {
  if (event.ctrlKey || event.shiftKey || event.metaKey || event.button !== 0) {
    return; // Let browser handle modified clicks normally
  }
  event.preventDefault();
  window.location.href = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
}
</script>

<style scoped lang="sass">
// Shared column look for .credits/.legal — only the padding side differs,
// which each caller sets itself (padding-left vs padding-right)
%footer-column
  position: relative
  width: $sidebar-width
  flex-shrink: 0
  padding-top: $medium + $minor
  box-sizing: border-box
  font-size: $secondary-font-size
  line-height: 1.3
  // Darkened one step further than the global --text-muted token: this
  // text sits over the decoration strip's semi-transparent overlay, which
  // still eats a bit of contrast even after the sitewide AA fix
  color: color-mix(in srgb, $text-muted 60%, $text)
  display: flex
  flex-direction: column
  justify-content: center
  align-self: stretch

.footer
  display: flex
  align-items: stretch
  justify-content: space-between
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
    background: url('@/assets/images/decorations/footer-decoration.png') left bottom repeat-x
    background-size: auto $footer-height
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

// One step between every pair of rows in the column: the lines read as one
// list, and a list reads as one only while its rows are spaced alike.
.legal > div:not(:last-child)
  margin-bottom: $small

@media (max-width: $bp-shell)
  .footer
    flex-direction: column
    align-items: stretch
    min-height: auto
    // Decoration strip is tuned for a wide single row; relax the fixed
    // height so stacked columns aren't clipped by it
    overflow: visible
    &:before
      height: $footer-height

  .credits,
  .legal
    width: auto
    padding: $medium $big
    align-self: auto

  .legal
    padding-top: 0
</style>
