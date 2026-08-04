<template>
  <!-- Visually hidden page heading: the landing has no visible h1 by
       design, but screen readers and SEO need the document outline to
       start at h1 before the h2 block titles below. -->
  <h1 class="visually-hidden">Dungeon Master — текстовые ролевые игры</h1>
  <BlockTitle v-once>Наши пользователи о нас</BlockTitle>
  <RandomTestimonials />
  <p class="reviews-links" v-once>
    Со всеми отзывами можно ознакомиться
    <router-link to="/testimonials"
      ><strong>на отдельной странице</strong></router-link
    >. Будем рады, если поделитесь и своим —
    <router-link :to="TESTIMONIALS_FORUM_TOPIC"
      ><strong>в топике на форуме</strong></router-link
    >.
  </p>
  <DashSeparator />
  <RecentNews />
  <DashSeparator />
  <BestWeeklyPost />
  <DashSeparator />
  <LatestRatedPost />
  <DashSeparator />
  <p class="discovery">
    Хотите увидеть, как еще играют на площадке? Загляните в Пульс, там вы
    найдете
    <router-link to="/pulse"><strong>последние оцененные</strong></router-link>
    и
    <router-link :to="bestPostsLink"><strong>лучшие посты</strong></router-link>
    этой недели.
  </p>
</template>

<script setup lang="ts">
import { computed } from "vue";
import RandomTestimonials from "./RandomTestimonials.vue";
import BestWeeklyPost from "./BestWeeklyPost.vue";
import LatestRatedPost from "./LatestRatedPost.vue";
import RecentNews from "./RecentNews.vue";
import { getWeekStartUtc } from "@/shared/lib/utils/datetime";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { TESTIMONIALS_FORUM_TOPIC } from "@/shared/config/wellKnownRoutes";
import dayjs from "dayjs";

const bestPostsLink = computed(() => {
  const weekStart = dayjs(getWeekStartUtc()).format("YYYY-MM-DD");
  return `/pulse?sort=rating&createdFrom=${weekStart}`;
});
</script>

<style scoped lang="sass">
.reviews-links
  margin: $small 0 $medium
  color: $text
  line-height: 1.6
  a
    color: $link
    &:hover
      color: $link-hover

.discovery
  margin: 0
  color: $text
  line-height: 1.6
</style>
