<script setup lang="ts">
// No action of its own: what a viewer may do depends on the role, which the feature owns.
import { computed, ref } from "vue";
import type { Character, CharacterAttribute } from "../model/characterTypes";
import { characterStatusLabel } from "../model/characterStatus";
import { UserLink } from "@/entities/user/@x/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { ContentText } from "@/shared/ui/Content";
import { SvgIcon } from "@/shared/ui/Icon";

const props = defineProps<{
  character: Character;
}>();

const isExpanded = ref(false);

// Two cards of one character can share a page, so the id must be unique.
const detailsId = `character-details-${Math.random().toString(36).slice(2, 9)}`;

const statusLabel = computed<string | null>(() =>
  characterStatusLabel(props.character),
);

function hasValue(attr: CharacterAttribute): boolean {
  return !!attr.valueBbText || !!(attr.value && attr.value.trim().length);
}

// The roster answers characters without their attributes.
const visibleAttributes = computed(() =>
  (props.character.attributes ?? []).filter(hasValue),
);

const hasDetails = computed(() => visibleAttributes.value.length > 0);

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
    <!-- The strip holds a link and a slot, which a role="button" would hide; the keyboard uses the chevron. -->
    <!-- eslint-disable-next-line vuejs-accessibility/click-events-have-key-events -->
    <header class="character-header" @click="toggleExpand">
      <!-- Without an avatar no image, unlike a user's silhouette. -->
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
          Неигровой персонаж
        </secondary-text>
        <div v-if="$slots.controls" class="character-controls" @click.stop>
          <slot name="controls" />
        </div>
      </div>
      <button
        v-if="hasDetails"
        type="button"
        class="expand-toggle"
        :aria-expanded="isExpanded"
        :aria-controls="detailsId"
        :aria-label="isExpanded ? 'Свернуть' : 'Показать полностью'"
        @click.stop="toggleExpand"
      >
        <SvgIcon
          :name="isExpanded ? 'chevronUp' : 'chevronDown'"
          class="expand-icon"
        />
      </button>
    </header>

    <div
      v-if="isExpanded && hasDetails"
      :id="detailsId"
      class="character-details"
    >
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
        <!-- The raw value is text; only valueBbText, rendered by the server, goes through ContentText (BBCODE_RENDERING.md). -->
        <content-text
          v-if="attr.valueBbText"
          :html="attr.valueBbText"
          class="detail-content"
        />
        <span v-else class="detail-value">{{ plainValue(attr) }}</span>
      </div>
    </div>
  </article>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *

.character-card
  background-color: $bg-element
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

.character-info
  flex: 1
  min-width: 0

.character-name
  font-weight: bold
  margin: 0

.character-status
  font-size: $secondary-font-size
  margin-top: $tiny
  color: $text-muted

.character-author
  font-size: $secondary-font-size
  margin-top: $tiny

// A slot that renders nothing must add no box, so the spacing lives on the control.
.character-controls
  font-size: $secondary-font-size

.expand-toggle
  color: $text-muted
  align-self: center
  // A button takes the user agent's font size, and the glyph is drawn at 1em.
  font-size: inherit
  +icon-button(1em)

.expand-icon
  color: inherit

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
