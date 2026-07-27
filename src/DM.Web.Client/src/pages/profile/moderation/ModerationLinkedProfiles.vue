<script setup lang="ts">
import type { LinkedProfile } from "@/shared/api/models/moderation";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDate } from "@/shared/lib/utils/datetime";

defineProps<{
  profiles: LinkedProfile[];
}>();
</script>

<template>
  <div class="mod-section">
    <h4 class="mod-section_title">Связанные профили ({{ profiles.length }})</h4>

    <div v-if="profiles.length" class="mod-linked-list">
      <div v-for="p in profiles" :key="p.userId" class="mod-linked-item">
        <router-link
          :to="{ name: 'profile', params: { username: p.username } }"
          class="mod-linked-username"
        >
          {{ p.username }}
        </router-link>
        <secondary-text>
          {{ p.sharedIpsCount }} общих IP, последний
          {{ formatDate(p.lastSharedLoginUtc) }}
        </secondary-text>
      </div>
    </div>

    <secondary-text v-else>Нет связанных профилей</secondary-text>
  </div>
</template>

<style scoped lang="sass">
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

.mod-linked-username
  color: $link
  font-weight: bold
  text-decoration: none
  &:hover
    color: $link-hover
    text-decoration: underline
</style>
