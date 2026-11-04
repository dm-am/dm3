<script setup lang="ts">
/**
 * The like of a line: the word "Нравится", with the count in brackets once
 * somebody likes it, "Нравится (3)". For a reader who may press it, an action
 * of the line like "Редактировать", in the line's colour with a grey link's
 * answer to the pointer; the reader's own mark is the pressed state, bold as a
 * selected tab or segment. For the author and a guest the plain words, and
 * nothing at all while there is nobody to count. The tooltip lists who likes
 * it, and there is none while nobody does.
 */
import { computed } from "vue";
import type { User } from "@/shared/api/models/common/user";
import { getLikesTooltip } from "@/shared/lib/utils/chat";
import { Tooltip } from "@/shared/ui/Tooltip";

const props = withDefaults(
  defineProps<{
    /** Who likes it: the count, and the names in the tooltip. */
    likes: readonly Pick<User, "username">[];
    /** The viewer is among them: the words are bold. */
    liked?: boolean;
    /** The viewer may press it. */
    interactive?: boolean;
  }>(),
  { liked: false, interactive: false },
);

const emit = defineEmits<{ toggle: [] }>();

const count = computed(() => props.likes.length);

const likersText = computed(() => getLikesTooltip(props.likes));

const caption = computed(() =>
  count.value > 0 ? `Нравится (${count.value})` : "Нравится",
);
</script>

<template>
  <Tooltip
    v-if="interactive || count > 0"
    :text="likersText"
    :disabled="!likersText"
    :focusable="!interactive && !!likersText"
  >
    <!-- The words name the button, aria-pressed says the reader's mark. -->
    <button
      v-if="interactive"
      type="button"
      class="like-badge"
      :class="{ 'like-badge--pressed': liked }"
      :aria-pressed="liked"
      @click="emit('toggle')"
    >
      {{ caption }}
    </button>
    <span
      v-else
      class="like-badge like-badge--listed"
      :class="{ 'like-badge--pressed': liked }"
      >{{ caption }}</span
    >
  </Tooltip>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// The words of the line it stands in: its font, size and colour.
.like-badge
  font: inherit
  color: inherit
  white-space: nowrap

// An action of the caption line, as CaptionAction: the button reset, the grey
// link's hover and the inline ring.
button.like-badge
  margin: 0
  +inline-link-button(inherit)

// Static, with likers to list: the tooltip is the one thing it answers with.
.like-badge--listed
  cursor: help

// The reader's own mark. Two classes, to outweigh the button's `font: inherit`.
.like-badge.like-badge--pressed
  font-weight: 700
</style>
