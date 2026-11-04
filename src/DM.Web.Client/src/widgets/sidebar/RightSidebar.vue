<template>
  <nav aria-label="Опросы, игры, блоги и контакты">
    <ul class="blocks">
      <SiteAddresses />
      <ActivePolls />
      <ContactLinks />
      <PopularGames />
      <PopularBlogs />
      <GameTagCloud />
    </ul>
    <ul class="blocks">
      <SupportPanel />
      <PartnersPanel />
      <SocialsPanel />
    </ul>
  </nav>
</template>

<script setup lang="ts">
import { defineAsyncComponent } from "vue";

// Above the fold, loaded with the page: a block landing later would push the column down.
import SiteAddresses from "./SiteAddresses.vue";
import ActivePolls from "./ActivePolls.vue";
import PopularGames from "./PopularGames.vue";
import ContactLinks from "./ContactLinks.vue";

// Below the fold: loaded after the page.
const PopularBlogs = defineAsyncComponent(() => import("./PopularBlogs.vue"));
const GameTagCloud = defineAsyncComponent(() => import("./GameTagCloud.vue"));
const SupportPanel = defineAsyncComponent(() => import("./SupportPanel.vue"));
const PartnersPanel = defineAsyncComponent(() => import("./PartnersPanel.vue"));
const SocialsPanel = defineAsyncComponent(() => import("./SocialsPanel.vue"));
</script>

<style scoped lang="sass">
.blocks
  list-style: none
</style>
