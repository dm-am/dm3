<template>
  <SidebarBlock token="Socials">
    <template #title>Наши соцсети</template>
    <div v-once class="socials">
      <a
        v-for="social in socialLinks"
        :key="social.name"
        class="social-icon"
        :href="social.url"
        target="_blank"
        :title="social.label"
      >
        <svg
          :viewBox="icons[social.icon].viewBox"
          v-html="icons[social.icon].path"
        />
      </a>
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import { icons } from "@/shared/lib/utils/icons";

const socialLinks = [
  {
    name: "vk",
    icon: "vk" as const,
    url: "https://vk.com/club376658",
    label: "Группа ВКонтакте",
  },
  {
    name: "discord",
    icon: "discord" as const,
    url: "https://discord.gg/ez7FdeYgQv",
    label: "Discord сервер",
  },
  {
    name: "youtube",
    icon: "youtube" as const,
    url: "https://www.youtube.com/@DungFM",
    label: "YouTube канал",
  },
];
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

$icon-size: 48px

.socials
  display: flex
  flex-direction: row
  gap: $medium
  align-items: center

.social-icon
  display: block
  width: $icon-size
  height: $icon-size
  color: $link-nav
  transition: color $animation-time ease

  &:hover
    color: $link-nav-hover

  svg
    width: 100%
    height: 100%
    fill: currentColor
</style>
