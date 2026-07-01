<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import type { Tag } from "@/entities/game";
import { gameApi } from "@/entities/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { Tooltip, RichText } from "@/shared/ui/Tooltip";

const model = defineModel<number[]>({ default: () => [] });

const tags = ref<Tag[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);

// Fixed group order
const TAG_GROUP_ORDER = [
  "Система",
  "Жанр",
  "Формат игры",
  "Формат постов",
  "Темп",
  "Ограничения",
  "Новичкам",
];

// Ordered groups with tags
const orderedGroups = computed(() => {
  const groups: Record<string, Tag[]> = {};
  for (const tag of tags.value) {
    const group = tag.groupTitle || "Другое";
    if (!groups[group]) {
      groups[group] = [];
    }
    groups[group].push(tag);
  }
  // Return array of [groupName, tags] in fixed order
  return TAG_GROUP_ORDER.filter((g) => groups[g] && groups[g].length > 0).map(
    (g) => ({ name: g, tags: groups[g] }),
  );
});

function isSelected(tagId: number): boolean {
  return model.value.includes(tagId);
}

function getGroupDescription(groupTitle: string): string | undefined {
  const tag = tags.value.find((t) => t.groupTitle === groupTitle);
  return tag?.groupDescription;
}

function toggleTag(tagId: number) {
  if (isSelected(tagId)) {
    model.value = model.value.filter((id) => id !== tagId);
  } else {
    model.value = [...model.value, tagId];
  }
}

async function loadTags() {
  loading.value = true;
  error.value = null;

  const { data, error: apiError } = await gameApi.getTags();

  if (apiError) {
    error.value = apiError.title || "Failed to load tags";
  } else if (data) {
    tags.value = data.resources;
  }

  loading.value = false;
}

onMounted(loadTags);
</script>

<template>
  <div class="tag-selector">
    <div v-if="error" class="selector-error">
      {{ error }}
    </div>

    <div v-else-if="tags.length === 0" class="selector-empty">
      <secondary-text>Нет доступных тегов</secondary-text>
    </div>

    <div v-else class="tags-groups">
      <div
        v-for="group in orderedGroups"
        :key="group.name"
        class="tag-category"
      >
        <Tooltip :text="getGroupDescription(group.name)">
          <h4 class="category-title">
            {{ group.name }}
          </h4>
        </Tooltip>
        <div class="category-tags">
          <Tooltip v-for="tag in group.tags" :key="tag.id">
            <template #content>
              <RichText :text="tag.description || tag.title" />
            </template>
            <button
              type="button"
              class="tag-button"
              :class="{ selected: isSelected(tag.id) }"
              @click="toggleTag(tag.id)"
            >
              {{ tag.title }}
            </button>
          </Tooltip>
        </div>
      </div>
    </div>

    <div v-if="model.length > 0" class="selected-count">
      <secondary-text>Выбрано тегов: {{ model.length }}</secondary-text>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.tag-selector
  min-height: $grid-step * 10

.selector-error
  color: $accent-red

.tags-groups
  display: flex
  flex-direction: column
  gap: $medium

.tag-category
  .category-title
    margin-bottom: $small
    font-size: $secondary-font-size
    color: $text-muted
    text-transform: uppercase
    cursor: help

.category-tags
  display: flex
  flex-wrap: wrap
  gap: $small

.tag-button
  padding: $tiny $small
  background-color: $bg-element-accent
  border: 1px solid $border
  border-radius: $border-radius
  color: $text
  font-size: $secondary-font-size
  cursor: pointer

  &:hover
    background-image: linear-gradient($hover-overlay, $hover-overlay)
    border-color: $border-focus

  &.selected
    background-color: $link
    border-color: $link
    color: white

.selected-count
  margin-top: $small
</style>
