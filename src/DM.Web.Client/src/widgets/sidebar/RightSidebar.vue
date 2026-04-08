<template>
  <div>
    <ActivePolls />
    <PopularGames />
    <PopularBlogs />
    <GameTagCloud />
    <ContactForms />
    <SupportUs />
    <Partners />
    <Socials />
  </div>
</template>

<script setup lang="ts">
import { defineAsyncComponent } from "vue";

// Critical above-fold components - load synchronously
import ActivePolls from "./ActivePolls.vue";
import PopularGames from "./PopularGames.vue";

// Below-fold components - lazy load to reduce initial bundle
const PopularBlogs = defineAsyncComponent(() => import("./PopularBlogs.vue"));
const GameTagCloud = defineAsyncComponent(() => import("./GameTagCloud.vue"));
const ContactForms = defineAsyncComponent(() => import("./ContactForms.vue"));
const SupportUs = defineAsyncComponent(() => import("./SupportUs.vue"));
const Partners = defineAsyncComponent(() => import("./Partners.vue"));
const Socials = defineAsyncComponent(() => import("./Socials.vue"));
</script>
