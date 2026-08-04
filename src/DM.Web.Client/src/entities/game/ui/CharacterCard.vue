<script setup lang="ts">
// Character card. Renders the character.attributes list schema-driven — the
// legacy fixed fields (race / class / appearance / temper / story / skills /
// inventory / alignment) are gone from the DTO. BbCode attributes carry
// server-rendered HTML in `valueBbText` and are bound through ContentText;
// the raw `value` string is NEVER piped to v-html or interpolated as markup
// (docs/architecture/BBCODE_RENDERING.md).
//
// The card draws no action of its own: what a viewer may do with a character
// depends on their role in the game, and that rule belongs to the feature that
// owns it rather than to the entity. Whatever the consumer puts into the
// `controls` slot is rendered under the character's name.
import { computed, ref } from "vue";
import type { Character, CharacterAttribute } from "../model/types";
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

// The details block is named so the toggle can point at it. Two cards of the
// same character can be on one page (roster and a game post), and a duplicate
// id would send both toggles to the first block.
const detailsId = `character-details-${Math.random().toString(36).slice(2, 9)}`;

// Status badge for non-active characters (Retired is refined by the flags).
// The caption itself is entity-level: the character's own page prints the same
// one, and two copies of it would be two vocabularies for one lifecycle.
const statusLabel = computed<string | null>(() =>
  characterStatusLabel(props.character),
);

// An attribute is worth rendering when it carries either server-rendered
// BbCode HTML or a non-empty plain value.
function hasValue(attr: CharacterAttribute): boolean {
  return !!attr.valueBbText || !!(attr.value && attr.value.trim().length);
}

// The roster endpoint answers characters without their attributes: the sheet is
// read one character at a time. A card there shows the name and the portrait and
// no details, rather than throwing and taking the whole page with it.
const visibleAttributes = computed(() =>
  (props.character.attributes ?? []).filter(hasValue),
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
    <!-- Character header.
         The whole strip stays a mouse target for the disclosure, and it cannot
         also be the keyboard one: it carries a link to the player and the
         consumer's controls slot, and both disappear from assistive technology
         once they sit inside a role="button". The keyboard path is the chevron
         at its end, a real button with aria-expanded — which is the part the
         rule below cannot see from one element. -->
    <!-- eslint-disable-next-line vuejs-accessibility/click-events-have-key-events -->
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
        <!-- The header toggles the card, so a click on a control has to stop
             before it gets there. -->
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

    <!-- Character details (expanded) — schema-driven attributes -->
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
@import "@/assets/styles/Inputs"

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

// Typography only: a slot that renders nothing must not add a box to the card,
// so the spacing above the control lives on the control itself.
.character-controls
  font-size: $secondary-font-size

// The disclosure the keyboard can reach. Drawn exactly as the bare glyph it
// replaced — no fill, no border, the same colour and the same place in the row
// — so a reader with a mouse sees no change; what is new is that the control
// can be tabbed to and answers Enter and Space, which a <header> holding a link
// and a slot could never do without hiding both.
.expand-toggle
  color: $text-muted
  align-self: center
  // The glyph is drawn at 1em, and a <button> does not inherit the page's font
  // size — the user agent gives it one of its own. Without this the chevron
  // would come out three pixels smaller than the icon it replaced.
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
