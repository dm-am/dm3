<script setup lang="ts">
// Character card. Renders the character.attributes list schema-driven — the
// legacy fixed fields (race / class / appearance / temper / story / skills /
// inventory / alignment) are gone from the DTO. BbCode attributes carry
// server-rendered HTML in `valueBbText` and are bound through ContentText;
// the raw `value` string is NEVER piped to v-html or interpolated as markup
// (docs/architecture/BBCODE_RENDERING.md).
import { computed, ref } from "vue";
import type { Character, CharacterAttribute } from "../model/types";
import { UserLink } from "@/entities/user/@x/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { AvatarImg, ContentText } from "@/shared/ui";
import { SvgIcon } from "@/shared/ui/Icon";

const props = defineProps<{
  character: Character;
}>();

const isExpanded = ref(false);

// Status badge for non-active characters (Retired is refined by the flags).
const statusLabel = computed<string | null>(() => {
  const c = props.character;
  switch (c.status) {
    case "UnderReview":
      return "На рассмотрении";
    case "Retired":
      if (c.isDead) return "Персонаж мертв";
      if (c.isPlayerLeft) return "Покинул игру";
      if (c.isPlayerExiled) return "Выведен из игры";
      return "Вне игры";
    default:
      return null;
  }
});

// An attribute is worth rendering when it carries either server-rendered
// BbCode HTML or a non-empty plain value.
function hasValue(attr: CharacterAttribute): boolean {
  return !!attr.valueBbText || !!(attr.value && attr.value.trim().length);
}

const visibleAttributes = computed(() =>
  props.character.attributes.filter(hasValue),
);

const hasDetails = computed(() => visibleAttributes.value.length > 0);

// Plain (non-BbCode) value with its optional list modifier appended.
function plainValue(attr: CharacterAttribute): string {
  let text = attr.value ?? "";
  if (attr.modifier) text = `${text} (${attr.modifier})`;
  return text;
}

function toggleExpand() {
  isExpanded.value = !isExpanded.value;
}
</script>

<template>
  <article class="character-card" :class="{ expanded: isExpanded }">
    <!-- Character header -->
    <header class="character-header" @click="toggleExpand">
      <!-- Character: no avatar = no image (differs from the User default silhouette). -->
      <AvatarImg
        :picture="character.picture"
        :alt="character.name"
        :size="60"
        img-class="character-picture"
        no-default
      />
      <div class="character-info">
        <h4 class="character-name">{{ character.name }}</h4>
        <secondary-text v-if="statusLabel" class="character-status">
          {{ statusLabel }}
        </secondary-text>
        <secondary-text v-if="character.author" class="character-author">
          Игрок: <user-link :user="character.author" />
        </secondary-text>
        <secondary-text v-else-if="character.isNpc" class="character-author">
          НПС
        </secondary-text>
      </div>
      <SvgIcon
        v-if="hasDetails"
        :name="isExpanded ? 'chevronUp' : 'chevronDown'"
        class="expand-icon"
      />
    </header>

    <!-- Character details (expanded) — schema-driven attributes -->
    <div v-if="isExpanded && hasDetails" class="character-details">
      <div v-if="character.totalPostsCount" class="detail-section">
        <span class="detail-label">Постов:</span>
        <span class="detail-value">{{ character.totalPostsCount }}</span>
      </div>

      <div
        v-for="attr in visibleAttributes"
        :key="attr.id"
        class="detail-section"
      >
        <span class="detail-label">{{ attr.title }}:</span>
        <!-- BbCode attribute: server-rendered HTML via ContentText. -->
        <content-text
          v-if="attr.valueBbText"
          :html="attr.valueBbText"
          class="detail-content"
        />
        <!-- Plain attribute: interpolated as text, never as markup. -->
        <span v-else class="detail-value">{{ plainValue(attr) }}</span>
      </div>
    </div>
  </article>
</template>

<style scoped lang="sass">
.character-card
  background-color: $bg-element
  border-radius: $border-radius
  overflow: hidden

.character-header
  display: flex
  align-items: flex-start
  gap: $small
  padding: $small
  cursor: pointer

  &:hover
    background-color: $bg-element-accent

.character-picture
  width: $grid-step * 15
  height: $grid-step * 15
  object-fit: cover
  border-radius: $border-radius

.character-info
  flex: 1
  min-width: 0

.character-name
  font-weight: bold
  margin: 0
  white-space: nowrap
  overflow: hidden
  text-overflow: ellipsis

.character-status
  font-size: $secondary-font-size
  margin-top: $tiny
  color: $text-muted

.character-author
  font-size: $secondary-font-size
  margin-top: $tiny

.expand-icon
  color: $text-muted
  align-self: center

.character-details
  padding: $small
  border-top: 1px solid $border
  background-color: $bg-element-accent

.detail-section
  & + &
    margin-top: $small
    padding-top: $small
    border-top: 1px solid $border

.detail-label
  display: block
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $tiny

.detail-value
  font-weight: bold

.detail-content
  font-size: $secondary-font-size
</style>
