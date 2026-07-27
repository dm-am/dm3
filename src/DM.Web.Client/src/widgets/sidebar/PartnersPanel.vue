<template>
  <SidebarBlock token="Partners">
    <template #title>Партнеры</template>
    <div v-once class="partners">
      <Tooltip
        v-for="partner in partners"
        :key="partner.name"
        :text="partner.name"
      >
        <a
          :href="partner.url"
          target="_blank"
          rel="noopener noreferrer"
          class="partner-link"
        >
          <img :src="partner.logo" :alt="partner.name" class="partner-logo" />
        </a>
      </Tooltip>
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import loreLogo from "@/assets/images/partners/lore.png";
import dndsuLogo from "@/assets/images/partners/dndsu.png";

interface Partner {
  name: string;
  url: string;
  logo: string;
}

const partners: Partner[] = [
  {
    name: "LORE",
    url: "https://www.youtube.com/channel/UCEDRLNTs-Fp3AZfxveT8d3w",
    logo: loreLogo,
  },
  {
    name: "DnD.su",
    url: "https://dnd.su/",
    logo: dndsuLogo,
  },
];
</script>

<style scoped lang="sass">
.partners
  display: flex
  flex-direction: row
  gap: $medium
  align-items: center
  flex-wrap: wrap

.partner-link
  display: block
  transition: filter 0.15s ease

  &:hover
    filter: brightness(1.1)

.partner-logo
  width: 100px
  height: 100px
  border-radius: 50%
  object-fit: cover
  box-shadow: 0 0 12px var(--partner-glow, transparent)
  margin: 6px
  filter: brightness(var(--partner-brightness, 1))
</style>
