<template>
  <footer class="footer">
    <!-- Static content - render once (currentYear computed once at component creation) -->
    <div class="credits" v-once>
      <div class="copyright-line">
        © DM.AM, 2007 &ndash; {{ currentYear }}. <span class="age">18+</span>
      </div>
      <div>
        Основатель сайта –
        <router-link :to="{ name: 'profile', params: { username: 'rakot' } }"
          >rakot</router-link
        >
      </div>
      <div>
        Администрирование –
        <router-link :to="{ name: 'profile', params: { username: 'Evengard' } }"
          >Evengard</router-link
        >,
        <router-link
          :to="{ name: 'profile', params: { username: 'SolohinLex' } }"
          >SolohinLex</router-link
        >
      </div>
      <div>
        Помощь в разработке –
        <router-link :to="{ name: 'profile', params: { username: 'Rayzen' } }"
          >Rayzen</router-link
        >
      </div>
      <div>
        Дизайн логотипа –
        <router-link
          :to="{ name: 'profile', params: { username: 'Azur' } }"
          @click="rickroll"
          >Azur</router-link
        >
      </div>
    </div>

    <div class="center-col" v-once></div>

    <div class="legal" v-once>
      <div class="legal-first">
        <router-link :to="{ name: 'privacy-policy' }"
          >Политика конфиденциальности</router-link
        >
      </div>
      <div>
        <router-link :to="{ name: 'user-agreement' }"
          >Пользовательское соглашение</router-link
        >
      </div>
    </div>
  </footer>
</template>

<script setup lang="ts">
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
@import "src/assets/styles/Layout"
@import "src/assets/styles/Themes"

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
    background: url('@/assets/images/decorations/footer-decoration.png') left bottom repeat-x
    background-size: auto $footer-height
    filter: $filter-invert

.credits
  position: relative
  width: $sidebar-width
  flex-shrink: 0
  padding-left: $big
  padding-top: $medium + $minor
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

.center-col
  flex-grow: 1
  position: relative

.legal
  position: relative
  width: $sidebar-width
  flex-shrink: 0
  padding-right: $big
  padding-top: $medium + $minor
  box-sizing: border-box
  font-size: $secondary-font-size
  line-height: 1.3
  color: $text-muted
  display: flex
  flex-direction: column
  justify-content: center
  align-self: stretch

.legal-first
  margin-bottom: $small
</style>
