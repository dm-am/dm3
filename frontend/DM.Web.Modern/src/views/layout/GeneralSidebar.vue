<template>
  <div>
    <active-polls />
    <popular-games />
    <popular-blogs />
    <game-tag-cloud />
    <feedback-links />
    <project-support />
    <site-partners />
  </div>
</template>

<script setup lang="ts">
import ActivePolls from "@/views/layout/sidebar/ActivePolls.vue";
import FeedbackLinks from "@/views/layout/sidebar/FeedbackLinks.vue";
import PopularGames from "@/views/layout/sidebar/PopularGames.vue";
import PopularBlogs from "@/views/layout/sidebar/PopularBlogs.vue";
import GameTagCloud from "@/views/layout/sidebar/GameTagCloud.vue";
import ProjectSupport from "@/views/layout/sidebar/ProjectSupport.vue";
import SitePartners from "@/views/layout/sidebar/SitePartners.vue";
</script>
