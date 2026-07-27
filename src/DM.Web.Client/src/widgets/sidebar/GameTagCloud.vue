<template>
  <SidebarBlock token="GameTags">
    <template #title>Теги игр</template>
    <SidebarSkeleton v-if="gamesStore.tagsLoading && !tags.length" :lines="4" />
    <SecondaryText v-else-if="gamesStore.tagsError && !tags.length">
      Не удалось загрузить.
      <button
        type="button"
        class="retry-link"
        @click="gamesStore.fetchTags(true)"
      >
        Повторить
      </button>
    </SecondaryText>
    <SecondaryText v-else-if="!tags.length">Тегов пока нет</SecondaryText>
    <div v-else class="tag-cloud">
      <Tooltip v-for="tag in sortedTags" :key="tag.id">
        <template #content>
          <TooltipContent :text="tag.description || tag.title" />
        </template>
        <router-link
          :to="{ name: 'games', query: { requiredTags: String(tag.id) } }"
          class="tag"
          :style="getTagStyle(tag)"
        >
          {{ tag.title }}
        </router-link>
      </Tooltip>
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import { computed, onMounted } from "vue";
import { Tooltip, TooltipContent } from "@/shared/ui/Tooltip";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useGamesStore } from "@/entities/game/model/store";
import type { Tag } from "@/entities/game";

const gamesStore = useGamesStore();

// Use tags from store (cached with 5-minute TTL)
const tags = computed(() => gamesStore.tags ?? []);

// Sort tags alphabetically for better UX
const sortedTags = computed(() =>
  [...tags.value].sort((a, b) => a.title.localeCompare(b.title, "ru")),
);

// Calculate min/max for normalization
const maxGamesCount = computed(() =>
  Math.max(...tags.value.map((t) => t.gamesCount), 1),
);
const minGamesCount = computed(() =>
  Math.min(...tags.value.map((t) => t.gamesCount), 0),
);

// Font size range: 12px to 18px (7 levels)
const MIN_FONT_SIZE = 12;
const MAX_FONT_SIZE = 18;
const BOLD_THRESHOLD = 15; // 15px and above are bold

function getTagStyle(tag: Tag): { fontSize: string; fontWeight: string } {
  const range = maxGamesCount.value - minGamesCount.value;
  const normalizedValue =
    range > 0 ? (tag.gamesCount - minGamesCount.value) / range : 0;

  // Map to font size (12-18px)
  const fontSize = Math.round(
    MIN_FONT_SIZE + normalizedValue * (MAX_FONT_SIZE - MIN_FONT_SIZE),
  );

  return {
    fontSize: `${fontSize}px`,
    fontWeight: fontSize >= BOLD_THRESHOLD ? "bold" : "normal",
  };
}

onMounted(() => {
  // Fetch tags via store (uses cache with stale-while-revalidate)
  gamesStore.fetchTags();
});
</script>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

.tag-cloud
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $tiny $small
  line-height: 1.4

.tag
  transition: opacity 0.15s ease

  &:hover
    opacity: 0.8

.retry-link
  +inline-link-button
</style>
