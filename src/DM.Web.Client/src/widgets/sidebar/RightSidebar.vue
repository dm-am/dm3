<template>
  <nav aria-label="Опросы, игры, блоги и контакты">
    <ul class="blocks">
      <ActivePolls />
      <PopularGames />
      <PopularBlogs />
      <GameTagCloud />
      <ContactLinks />
      <SupportPanel />
      <PartnersPanel />
      <SocialsPanel />
    </ul>
  </nav>
</template>

<script setup lang="ts">
import { defineAsyncComponent } from "vue";

// Critical above-fold components - load synchronously
import ActivePolls from "./ActivePolls.vue";
import PopularGames from "./PopularGames.vue";

// Below-fold components - lazy load to reduce initial bundle
const PopularBlogs = defineAsyncComponent(() => import("./PopularBlogs.vue"));
const GameTagCloud = defineAsyncComponent(() => import("./GameTagCloud.vue"));
const ContactLinks = defineAsyncComponent(() => import("./ContactLinks.vue"));
const SupportPanel = defineAsyncComponent(() => import("./SupportPanel.vue"));
const PartnersPanel = defineAsyncComponent(() => import("./PartnersPanel.vue"));
const SocialsPanel = defineAsyncComponent(() => import("./SocialsPanel.vue"));
</script>

<style scoped lang="sass">
.blocks
  list-style: none
</style>
