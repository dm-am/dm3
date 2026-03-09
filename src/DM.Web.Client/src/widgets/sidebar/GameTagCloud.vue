<template>
  <menu-block token="GameTags">
    <template #title>Теги игр</template>
    <secondary-text v-if="!tags.length">Нет игр</secondary-text>
    <div v-else class="tag-cloud">
      <router-link
        v-for="tag in sortedTags"
        :key="tag.id"
        :to="{ name: 'games-active', query: { tagId: tag.id } }"
        class="tag"
        :style="getTagStyle(tag)"
      >
        {{ tag.title }}
      </router-link>
    </div>
  </menu-block>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import MenuBlock from "@/widgets/menu/MenuBlock.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { gameApi } from "@/entities/game";
import type { Tag } from "@/entities/game";

const tags = ref<Tag[]>([]);

// Sort tags alphabetically for better UX
const sortedTags = computed(() =>
  [...tags.value].sort((a, b) => a.title.localeCompare(b.title, "ru"))
);

// Calculate min/max for normalization
const maxGamesCount = computed(() =>
  Math.max(...tags.value.map((t) => t.gamesCount), 1)
);
const minGamesCount = computed(() =>
  Math.min(...tags.value.map((t) => t.gamesCount), 0)
);

// Font size range: 12px to 18px (7 levels)
const MIN_FONT_SIZE = 12;
const MAX_FONT_SIZE = 18;
const BOLD_THRESHOLD = 15; // 15px and above are bold

function getTagStyle(tag: Tag): { fontSize: string; fontWeight: string } {
  const range = maxGamesCount.value - minGamesCount.value;
  const normalizedValue = range > 0
    ? (tag.gamesCount - minGamesCount.value) / range
    : 0;

  // Map to font size (12-18px)
  const fontSize = Math.round(
    MIN_FONT_SIZE + normalizedValue * (MAX_FONT_SIZE - MIN_FONT_SIZE)
  );

  return {
    fontSize: `${fontSize}px`,
    fontWeight: fontSize >= BOLD_THRESHOLD ? "bold" : "normal",
  };
}

onMounted(async () => {
  const { data } = await gameApi.getTags();
  // Only show tags with at least one active game
  tags.value = (data?.resources ?? []).filter((t) => t.gamesCount > 0);
});
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.tag-cloud
  display: flex
  flex-wrap: wrap
  gap: $tiny
  line-height: 1.4

.tag
  display: inline-block
  transition: opacity 0.15s ease

  &:hover
    opacity: 0.8
</style>
