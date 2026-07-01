<template>
  <SidebarBlock token="Socials">
    <template #title>Наши соцсети</template>
    <div v-once class="socials">
      <Tooltip
        v-for="social in socialLinks"
        :key="social.name"
        :text="social.label"
      >
        <a
          class="social-icon"
          :href="social.url"
          target="_blank"
          rel="noopener noreferrer"
          :aria-label="social.label"
        >
          <svg
            :viewBox="icons[social.icon].viewBox"
            v-html="icons[social.icon].path"
            aria-hidden="true"
          />
        </a>
      </Tooltip>
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { icons } from "@/shared/lib/utils/icons";
import { DISCORD_INVITE_URL } from "@/shared/config/contacts";

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
    url: DISCORD_INVITE_URL,
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

  &:hover
    color: $link-nav-hover

  svg
    width: 100%
    height: 100%
    fill: currentColor
</style>
