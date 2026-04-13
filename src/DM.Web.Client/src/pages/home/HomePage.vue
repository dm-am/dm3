<template>
  <block-title v-once>Наши пользователи о нас</block-title>
  <RandomTestimonials />
  <p class="reviews-links" v-once>
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
  <LatestRatedPost />
  <div class="separator" v-once>
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    - - - - - - - - - - - - - -
  </div>
  <p class="discovery">
    Хотите увидеть, как еще играют на площадке? Загляните в Пульс —
    там вы найдете <router-link to="/pulse"><strong>последние оцененные</strong></router-link> и <router-link :to="bestPostsLink"><strong>лучшие посты</strong></router-link> этой недели.
  </p>
</template>

<script setup lang="ts">
import { computed } from "vue";
import RandomTestimonials from "./RandomTestimonials.vue";
import BestWeeklyPost from "./BestWeeklyPost.vue";
import LatestRatedPost from "./LatestRatedPost.vue";
import RecentNews from "./RecentNews.vue";
import { getWeekStartUtc } from "@/entities/game";
import dayjs from "dayjs";

const bestPostsLink = computed(() => {
  const weekStart = dayjs(getWeekStartUtc()).format("YYYY-MM-DD");
  return `/pulse?sort=rating&createdFrom=${weekStart}`;
});
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.reviews-links
  margin: 12px 0 $medium
  color: $text
  line-height: 1.6

.separator
  margin: $small 0
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: 100%
  width: 0
  min-width: 100%
  user-select: none

.discovery
  margin: 0
  color: $text
  line-height: 1.6
</style>
