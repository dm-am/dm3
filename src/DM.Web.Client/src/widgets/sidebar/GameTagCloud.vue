<template>
  <SidebarBlock token="GameTags" title="Теги игр">
    <SidebarSkeleton v-if="gamesStore.tagsLoading && !tags.length" :lines="4" />
    <li v-else-if="gamesStore.tagsError && !tags.length">
      <SidebarLoadFailure @retry="gamesStore.fetchTags(true)" />
    </li>
    <li v-else-if="!tags.length">
      <SecondaryText>Тегов пока нет</SecondaryText>
    </li>
    <li v-else class="tag-cloud">
      <Tooltip v-for="tag in sortedTags" :key="tag.id">
        <template #content>
          <TooltipContent :html="tag.description" :text="tag.title" />
        </template>
        <router-link
          :to="{ name: 'games', query: { requiredTags: String(tag.id) } }"
          class="tag"
          :style="getTagStyle(tag)"
        >
          {{ tag.title }}
        </router-link>
      </Tooltip>
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import { computed, onMounted } from "vue";
import { Tooltip, TooltipContent } from "@/shared/ui/Tooltip";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SidebarLoadFailure from "./SidebarLoadFailure.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useGamesStore } from "@/entities/game";
import type { Tag } from "@/entities/game";

const gamesStore = useGamesStore();

const tags = computed(() => gamesStore.tags ?? []);

const sortedTags = computed(() =>
  [...tags.value].sort((a, b) => a.title.localeCompare(b.title, "ru")),
);

const maxGamesCount = computed(() =>
  Math.max(...tags.value.map((t) => t.gamesCount), 1),
);

const MIN_FONT_SIZE = 12;
const MAX_FONT_SIZE = 18;
const BOLD_THRESHOLD = 15; // 15px and above are bold

function getTagStyle(tag: Tag): { fontSize: string; fontWeight: string } {
  const normalizedValue = tag.gamesCount / maxGamesCount.value;

  const fontSize = Math.round(
    MIN_FONT_SIZE + normalizedValue * (MAX_FONT_SIZE - MIN_FONT_SIZE),
  );

  return {
    fontSize: `${fontSize}px`,
    fontWeight: fontSize >= BOLD_THRESHOLD ? "bold" : "normal",
  };
}

onMounted(() => {
  gamesStore.fetchTags();
});
</script>

<style scoped lang="sass">
.tag-cloud
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $tiny $small
  line-height: 1.4
</style>
