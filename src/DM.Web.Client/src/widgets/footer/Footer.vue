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
import { siteTime } from "@/shared/lib/utils/datetime";

// Static value - year doesn't change during session
const currentYear = siteTime().year();

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

  // The paper strip covers the footer from its torn edge down to the bottom,
  // whatever height the footer takes, and the edge is the one of a desktop
  // at every width. Two layers of one picture: on top the picture at the
  // footer's desktop height, torn edge and all; under it the same picture as
  // tall as the footer, of which only the flat paper below the first layer
  // shows. One layer at the footer's height drew the edge larger than on a
  // desktop in a footer taller than its picture (a larger text of the
  // browser); one layer of 150 left the last lines on the bare page under a
  // hard edge.
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

// One step between every pair of rows in the column: the lines read as one
// list, and a list reads as one only while its rows are spaced alike.
.legal > div:not(:last-child)
  margin-bottom: $small

// Without the sidebars the columns have nothing to line up with, so each is as
// wide as its text: the credits on the page's left gutter, the legal links
// against its right one, and never closer to each other than one gutter.
@media (max-width: $bp-shell)
  .footer
    column-gap: $big

  .credits,
  .legal
    width: auto
    flex-shrink: 1
</style>
