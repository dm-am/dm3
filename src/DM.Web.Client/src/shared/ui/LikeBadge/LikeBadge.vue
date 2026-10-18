<script setup lang="ts">
/**
 * The like of the site: the heart of icons.ts with the count in a plate over
 * its bottom-right corner, the plate cut the way the award plates are (bold
 * digits in a filled pill, a ring in the colour of the surface under it,
 * rounded by $minor). Hollow until the viewer likes it, filled after.
 *
 * Heart and plate are the colour of the line they stand in, in every state:
 * no red, and nothing changes under the pointer. A count of zero is the bare
 * heart with no plate.
 *
 * Interactive, it is a toggle button (aria-pressed) that emits `toggle`; the
 * caller sends the request and hands the new likes back. Otherwise (the
 * author's own content, a guest) it is the same drawing and not a control,
 * and it takes the keyboard only while there are likers for its tooltip to
 * list. The tooltip names them (getLikesTooltip) in both variants.
 *
 * The plate's ring is $bg-element, the card a comment, a topic and a
 * publication stand on; it has to match the surface or it shows as a frame. A
 * caller on another surface sets --like-badge-surface to that surface's token.
 * It is the last part of a caption line, after every action.
 */
import { computed } from "vue";
import type { User } from "@/shared/api/models/common/user";
import { getLikesTooltip } from "@/shared/lib/utils/chat";
import { SvgIcon } from "@/shared/ui/Icon";
import { Tooltip } from "@/shared/ui/Tooltip";

const props = withDefaults(
  defineProps<{
    /** Who liked it: the count, and the names in the tooltip. */
    likes: readonly Pick<User, "username">[];
    /** The viewer is among them: the heart is filled. */
    liked?: boolean;
    /** The viewer may press it. */
    interactive?: boolean;
  }>(),
  { liked: false, interactive: false },
);

const emit = defineEmits<{ toggle: [] }>();

const count = computed(() => props.likes.length);

// getLikesTooltip declares User[] and reads nothing but the usernames.
const likersText = computed(() => getLikesTooltip(props.likes as User[]));

// The button is named by its action and says the count; the static drawing
// has no action to name, so it says the count alone, zero included.
const label = computed(() =>
  props.interactive && count.value === 0
    ? "Нравится"
    : `Нравится: ${count.value}`,
);
</script>

<template>
  <Tooltip
    :text="likersText"
    :disabled="count === 0"
    :focusable="!interactive && count > 0"
    show-on-tap
  >
    <button
      v-if="interactive"
      type="button"
      class="like-badge"
      :aria-label="label"
      :aria-pressed="liked"
      @click="emit('toggle')"
    >
      <SvgIcon
        :name="liked ? 'heartFilled' : 'heartEmpty'"
        class="like-badge__heart"
      />
      <span v-if="count > 0" class="like-badge__count"
        ><span class="like-badge__digits">{{ count }}</span></span
      >
    </button>
    <span
      v-else
      class="like-badge"
      :class="{ 'like-badge--listed': count > 0 }"
      role="img"
      :aria-label="label"
    >
      <SvgIcon
        :name="liked ? 'heartFilled' : 'heartEmpty'"
        class="like-badge__heart"
      />
      <span v-if="count > 0" class="like-badge__count"
        ><span class="like-badge__digits">{{ count }}</span></span
      >
    </span>
  </Tooltip>
</template>

<style scoped lang="sass">
// The geometry was approved on the 12px caption line: a 15px heart, a 13px
// plate on 9px digits sliding 6px under the heart and hanging 3px below it.
// Written in em of that line, so a line of another size scales it whole; the
// plate's own lengths are in its own em, the digits' 9px.
.like-badge
  display: inline-flex
  align-items: flex-end
  vertical-align: calc(-2em / 12)
  padding: 0
  margin: 0
  border: none
  background: none
  font: inherit
  line-height: 1
  color: inherit

button.like-badge
  cursor: pointer

  // The ring of inline text (Reset.sass): the badge is a word of its line,
  // not a block control.
  &:focus-visible
    outline: 1px solid $link
    outline-offset: 2px

// Static, with likers to list: the tooltip is the one thing it answers with.
.like-badge--listed
  cursor: help

.like-badge__heart
  width: 1.25em
  height: 1.25em

.like-badge__count
  position: relative
  top: calc(3em / 9)
  margin-left: calc(-6em / 9)
  box-sizing: border-box
  height: calc(13em / 9)
  min-width: calc(13em / 9)
  padding: 0 calc(3em / 9)
  display: inline-flex
  align-items: center
  justify-content: center
  font-size: 0.75em
  font-weight: 700
  letter-spacing: 0.3px
  line-height: 1
  font-variant-numeric: tabular-nums
  // Filled with the line's colour: the plate is currentColor, and the digits
  // take the ink of a solid fill.
  background-color: currentColor
  border: 2px solid var(--like-badge-surface, #{$bg-element})
  border-radius: $minor

.like-badge__digits
  color: $text-on-fill
</style>
