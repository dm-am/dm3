<template>
  <ul class="blocks">
    <ContactLinks />
    <SupportPanel />
    <PartnersPanel />
    <SocialsPanel />
  </ul>
</template>

<script setup lang="ts">
/**
 * The blocks that end the right column: the address of the support, the
 * fundraising, the partners and the socials, in this order, after the blocks
 * of RightSidebar at every width.
 */
import { defineAsyncComponent } from "vue";

// Below the fold wherever they stand: loaded after the page.
const ContactLinks = defineAsyncComponent(() => import("./ContactLinks.vue"));
const SupportPanel = defineAsyncComponent(() => import("./SupportPanel.vue"));
const PartnersPanel = defineAsyncComponent(() => import("./PartnersPanel.vue"));
const SocialsPanel = defineAsyncComponent(() => import("./SocialsPanel.vue"));
</script>

<style scoped lang="sass">
.blocks
  list-style: none
</style>
