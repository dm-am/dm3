<template>
  <div>
    <subscribed-games />
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
import ActivePolls from "./ActivePolls.vue";
import FeedbackLinks from "./FeedbackLinks.vue";
import PopularGames from "./PopularGames.vue";
import PopularBlogs from "./PopularBlogs.vue";
import SubscribedGames from "./SubscribedGames.vue";
import GameTagCloud from "./GameTagCloud.vue";
import ProjectSupport from "./ProjectSupport.vue";
import SitePartners from "./SitePartners.vue";
</script>
