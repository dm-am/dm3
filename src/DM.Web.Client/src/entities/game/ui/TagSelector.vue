<script setup lang="ts">
// Tag picker over the game tag catalog, grouped the way the catalog groups it.
// The catalog itself is moderation's (`/moderation/tags`): a master attaches
// the tags it offers and invents none, so this control only ever toggles what
// GET /games/tags returned. Both write paths of a game use it — the creation
// form and the settings page — which is why it lives with the entity rather
// than inside either feature.
import { ref, computed, onMounted } from "vue";
import { pluralize } from "@/shared/lib/utils/pluralize";
import type { Tag } from "../model/types";
import { gameApi } from "../api";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { Tooltip, TooltipContent } from "@/shared/ui/Tooltip";

const model = defineModel<number[]>({ default: () => [] });

const tags = ref<Tag[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);

// Fixed group order
// A group as this control works with it: the tags plus the number of them one
// game may carry. The number is the catalog's, served with every tag, so
// moderation can move it without the client being rebuilt.
type TagGroupView = {
  name: string;
  tags: Tag[];
  limit: number | null;
};

// Groups in the catalog's own order, not in a list kept here.
//
// A hard-coded order was a second copy of the catalog, and it had drifted: it
// said "Новичкам" where the group is called "Новички", and did not mention
// "Деликатный контент" at all. Since anything outside the list was dropped
// rather than appended, two groups of eight never reached the screen - one of
// them the group a master marks sensitive content with, so the warning could
// not be given at all. Reading the order the catalog serves means a group
// moderation adds shows up by itself instead of vanishing quietly.
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

/**
 * A tag the master cannot add right now. Only ever true above a limit of one:
 * a group that takes a single tag is a switch, so its tags stay clickable and
 * a click moves the choice.
 */
function isBlocked(group: TagGroupView, tagId: number): boolean {
  if (group.limit === null || group.limit === 1 || isSelected(tagId)) {
    return false;
  }
  return selectedCount(group) >= group.limit;
}

/**
 * What the dimmed tag says when the pointer stops on it. Worded from the limit
 * and not from how many are ticked: a game imported over its group's limit
 * carries more than the limit, and a hint counting them would contradict itself.
 */
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

  // One tag per group is a switch and not a refusal: picking a second one
  // drops the first, the way a radio group behaves. Anything above one is a
  // ceiling, and the tags over it are dimmed rather than silently ignored.
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

async function loadTags() {
  loading.value = true;
  error.value = null;

  const { data, error: apiError } = await gameApi.getTags();

  if (apiError) {
    // UI_STANDARDS: error copy is Russian, and the server's raw title is not put
    // in front of the reader. A tag list carries no per-field errors worth
    // relaying, so the title could only ever be a generic server sentence this
    // page already words better.
    error.value = "Не удалось загрузить теги";
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
              <TooltipContent
                :text="
                  isBlocked(group, tag.id)
                    ? blockedHint(group)
                    : tag.description || tag.title
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
  // Relative accent-strength base (not a solid color) so the chip reads
  // on any background — one shade deeper than the surface it sits on
  background-color: $control-bg-hover-overlay
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
    color: $text-on-fill

  // The group is full: the tag stays readable and stops answering. Dimming is
  // the whole signal, so it must not also look hoverable.
  &.blocked
    color: $text-muted
    cursor: not-allowed

    &:hover
      background-image: none
      border-color: $border

.selected-count
  margin-top: $small
</style>
