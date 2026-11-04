<script setup lang="ts">
// Both write paths of a game use it, so it lives with the entity.
import { computed, onMounted } from "vue";
import { pluralize } from "@/shared/lib/utils/pluralize";
import type { Tag } from "../model/gameTypes";
import { useGamesStore } from "../model/store";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import TagSelectorSkeleton from "@/shared/ui/Skeleton/TagSelectorSkeleton.vue";
import { Tooltip, TooltipContent } from "@/shared/ui/Tooltip";

const model = defineModel<number[]>({ default: () => [] });

withDefaults(
  defineProps<{
    /** The rows each group of the catalogue wraps into at the host's width, for the skeleton */
    loadingRows?: number[];
  }>(),
  // The seeded catalogue in the game form, 600 wide.
  { loadingRows: () => [5, 3, 1, 1, 1, 1, 1, 1] },
);

// The catalogue the tag cloud and the filter read: a form opened again finds it cached.
const gamesStore = useGamesStore();
const tags = computed<Tag[]>(() => gamesStore.tags ?? []);
// No answer yet: a refusal empties the list as well, and has its own block.
const waiting = computed(
  () => gamesStore.tags === null && !gamesStore.tagsError,
);

type TagGroupView = {
  name: string;
  tags: Tag[];
  limit: number | null;
};

// In the catalog's own order, so a group moderation adds shows up by itself.
const orderedGroups = computed<TagGroupView[]>(() => {
  const groups = new Map<string, Tag[]>();
  for (const tag of tags.value) {
    const group = tag.groupTitle || "Другое";
    const existing = groups.get(group);
    if (existing) existing.push(tag);
    else groups.set(group, [tag]);
  }

  return [...groups.entries()]
    .map(([name, groupTags]) => ({
      name,
      tags: [...groupTags].sort((a, b) => a.sortOrder - b.sortOrder),
      limit: groupTags[0].groupMaxTagsPerGame ?? null,
      order: groupTags[0].groupSortOrder,
    }))
    .sort((a, b) => a.order - b.order || a.name.localeCompare(b.name, "ru"))
    .map(({ name, tags: groupTags, limit }) => ({
      name,
      tags: groupTags,
      limit,
    }));
});

function isSelected(tagId: number): boolean {
  return model.value.includes(tagId);
}

function getGroupDescription(groupTitle: string): string | undefined {
  const tag = tags.value.find((t) => t.groupTitle === groupTitle);
  return tag?.groupDescription;
}

function selectedCount(group: TagGroupView): number {
  return group.tags.filter((t) => isSelected(t.id)).length;
}

// Never for a limit of one: such a group is a switch, and a click moves the choice.
function isBlocked(group: TagGroupView, tagId: number): boolean {
  if (group.limit === null || group.limit === 1 || isSelected(tagId)) {
    return false;
  }
  return selectedCount(group) >= group.limit;
}

// From the limit, not the ticked count: an imported game can carry more than the limit.
function blockedHint(group: TagGroupView): string {
  const limit = group.limit ?? 0;
  const word = pluralize(limit, "тега", "тегов", "тегов");
  return `Из группы "${group.name}" можно выбрать не больше ${limit} ${word}. Снимите один, чтобы выбрать другой`;
}

function toggleTag(group: TagGroupView, tagId: number) {
  if (isSelected(tagId)) {
    model.value = model.value.filter((id) => id !== tagId);
    return;
  }

  if (group.limit === 1) {
    const others = new Set(group.tags.map((t) => t.id));
    model.value = [...model.value.filter((id) => !others.has(id)), tagId];
    return;
  }

  if (isBlocked(group, tagId)) {
    return;
  }

  model.value = [...model.value, tagId];
}

onMounted(() => gamesStore.fetchTags());
</script>

<template>
  <div class="tag-selector">
    <TagSelectorSkeleton v-if="waiting" :groups="loadingRows" />

    <ErrorState
      v-else-if="gamesStore.tagsError && !tags.length"
      message="Не удалось загрузить теги"
      :retry="() => gamesStore.fetchTags(true)"
    />

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
              <TooltipContent
                :html="isBlocked(group, tag.id) ? null : tag.description"
                :text="
                  isBlocked(group, tag.id) ? blockedHint(group) : tag.title
                "
              />
            </template>
            <button
              type="button"
              class="tag-button"
              :class="{
                selected: isSelected(tag.id),
                blocked: isBlocked(group, tag.id),
              }"
              :aria-pressed="isSelected(tag.id)"
              :disabled="isBlocked(group, tag.id)"
              @click="toggleTag(group, tag.id)"
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
@use "@/assets/styles/Buttons" as *
@use "@/assets/styles/FilterBubbles" as bubbles

.tag-selector
  min-height: $grid-step * 10

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

// The bubble of an applied filter, as the same tags stand under the filter bar of /games.
.tag-button
  +bubbles.bubble-box
  color: $text
  cursor: pointer

  &:hover:not(:disabled):not(.selected)
    background-image: linear-gradient($hover-overlay, $hover-overlay)

  // The press wash keeps a chosen tag apart from a hovered one.
  &.selected
    background-color: $bg-element-accent
    background-image: linear-gradient($active-overlay, $active-overlay)

  +disabled-hatch

.selected-count
  margin-top: $small
</style>
