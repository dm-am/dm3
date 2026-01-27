<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import type { Tag } from "@/api/models/gaming";
import gamingApi from "@/api/requests/gamingApi";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import TheLoader from "@/components/TheLoader.vue";

const model = defineModel<string[]>({ default: () => [] });

const tags = ref<Tag[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);

// Group tags by category
const groupedTags = computed(() => {
  const groups: Record<string, Tag[]> = {};
  for (const tag of tags.value) {
    const category = tag.category || "Другое";
    if (!groups[category]) {
      groups[category] = [];
    }
    groups[category].push(tag);
  }
  return groups;
});

function isSelected(tagId: string): boolean {
  return model.value.includes(tagId);
}

function toggleTag(tagId: string) {
  if (isSelected(tagId)) {
    model.value = model.value.filter((id) => id !== tagId);
  } else {
    model.value = [...model.value, tagId];
  }
}

async function loadTags() {
  loading.value = true;
  error.value = null;

  const { data, error: apiError } = await gamingApi.getTags();

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
    <the-loader v-if="loading" />

    <div v-else-if="error" class="selector-error">
      {{ error }}
    </div>

    <div v-else-if="tags.length === 0" class="selector-empty">
      <secondary-text>Нет доступных тегов</secondary-text>
    </div>

    <div v-else class="tags-groups">
      <div
        v-for="(categoryTags, category) in groupedTags"
        :key="category"
        class="tag-category"
      >
        <h4 class="category-title">{{ category }}</h4>
        <div class="category-tags">
          <button
            v-for="tag in categoryTags"
            :key="tag.id"
            type="button"
            class="tag-button"
            :class="{ selected: isSelected(tag.id) }"
            @click="toggleTag(tag.id)"
          >
            {{ tag.title }}
          </button>
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
  transition: all 0.2s

  &:hover
    background-color: $button-bg-hover
    border-color: $button-border-hover

  &.selected
    background-color: $accent-blue
    border-color: $accent-blue
    color: white

.selected-count
  margin-top: $small
</style>
