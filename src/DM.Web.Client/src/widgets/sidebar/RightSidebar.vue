<template>
  <nav aria-label="Опросы, игры, блоги и контакты">
    <ul class="blocks">
      <SiteAddresses />
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

// Critical above-fold components - load synchronously.
// SiteAddresses leads the column, so deferring it would land it a frame later
// and push everything under it down after the page has been read. It is a
// handful of static markup and costs the bundle nothing worth saving.
import SiteAddresses from "./SiteAddresses.vue";
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
