<template>
  <SidebarBlock token="Socials" title="Наши соцсети">
    <li v-once class="socials">
      <Tooltip
        v-for="social in socialLinks"
        :key="social.icon"
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
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { icons } from "@/shared/lib/utils/icons";
import {
  VK_GROUP_URL,
  DISCORD_INVITE_URL,
  YOUTUBE_CHANNEL_URL,
} from "@/shared/config/contacts";

const socialLinks = [
  {
    icon: "vk" as const,
    url: VK_GROUP_URL,
    label: "Группа ВКонтакте",
  },
  {
    icon: "discord" as const,
    url: DISCORD_INVITE_URL,
    label: "Discord сервер",
  },
  {
    icon: "youtube" as const,
    url: YOUTUBE_CHANNEL_URL,
    label: "YouTube канал",
  },
];
</script>

<style scoped lang="sass">
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
