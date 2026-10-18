<template>
  <nav :aria-label="label">
    <ul class="blocks">
      <SiteAddresses />
      <ActivePolls />
      <PopularGames />
      <PopularBlogs />
      <GameTagCloud />
    </ul>
    <RightSidebarTail />
    <!-- On a narrow screen the column slides in over the page, and the
         documents of the footer, a page away there, close it. -->
    <ul v-if="narrow" class="blocks">
      <LegalLinks />
    </ul>
  </nav>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent } from "vue";
import { useShellMode } from "@/shared/lib/composables/useShellMode";
import RightSidebarTail from "./RightSidebarTail.vue";

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
const LegalLinks = defineAsyncComponent(() => import("./LegalLinks.vue"));

const { narrow } = useShellMode();

const label = computed(() =>
  narrow.value
    ? "Опросы, игры, блоги, контакты и документы"
    : "Опросы, игры, блоги и контакты",
);
</script>

<style scoped lang="sass">
.blocks
  list-style: none
</style>
