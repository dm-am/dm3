<script setup lang="ts">
import type { LinkedProfile } from "@/api/models/moderation";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import dayjs from "dayjs";

defineProps<{
  profiles: LinkedProfile[];
}>();

function formatDate(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY");
}
</script>

<template>
  <div class="mod-section">
    <h4 class="mod-section_title">
      Связанные профили ({{ profiles.length }})
    </h4>

    <div v-if="profiles.length" class="mod-linked-list">
      <div
        v-for="p in profiles"
        :key="p.userId"
        class="mod-linked-item"
      >
        <router-link
          :to="{ name: 'profile', params: { login: p.login } }"
          class="mod-linked-login"
        >
          {{ p.login }}
        </router-link>
        <secondary-text>
          {{ p.sharedIpCount }} общих IP · последний
          {{ formatDate(p.lastSharedLoginUtc) }}
        </secondary-text>
      </div>
    </div>

    <secondary-text v-else>Нет связанных профилей</secondary-text>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.mod-section
  margin-bottom: $medium

.mod-section_title
  margin: 0 0 $small 0
  color: $heading

.mod-linked-list
  display: flex
  flex-direction: column
  gap: $small

.mod-linked-item
  display: flex
  align-items: baseline
  gap: $small

.mod-linked-login
  color: $link
  font-weight: bold
  text-decoration: none
  &:hover
    color: $link-hover
    text-decoration: underline
</style>
