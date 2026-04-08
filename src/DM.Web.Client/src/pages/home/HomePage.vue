<template>
  <block-title>Наши пользователи о нас</block-title>
  <RandomTestimonials />
  <p class="reviews-links">
    Со всеми отзывами можно ознакомиться <router-link to="/testimonials"><strong>на отдельной странице</strong></router-link>.
    Будем рады, если поделитесь и своим — <router-link to="/forum/general/1"><strong>в топике на форуме</strong></router-link>.
  </p>
  <div class="separator" v-once>
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - -
  </div>
  <RecentNews />
  <div class="separator" v-once>
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - -
  </div>
  <BestWeeklyPost />
  <div class="separator" v-once>
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - -
  </div>
  <LatestFeaturedPost />
</template>

<script setup lang="ts">
import RandomTestimonials from "./RandomTestimonials.vue";
import BestWeeklyPost from "./BestWeeklyPost.vue";
import LatestFeaturedPost from "./LatestFeaturedPost.vue";
import RecentNews from "./RecentNews.vue";
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.reviews-links
  margin: 12px 0 $medium
  color: $text
  line-height: 1.6

  a
    color: $link
    &:hover
      color: $link-hover

.separator
  margin: $small 0
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: 100%
  width: 0
  min-width: 100%
  user-select: none
</style>
