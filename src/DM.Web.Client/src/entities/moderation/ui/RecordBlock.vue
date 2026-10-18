<script setup lang="ts">
/**
 * The rectangle of a moderation record: what the record is about, and one
 * caption line under it that says where, whose and when.
 *
 * The frame is the one every piece of content on the site stands in, the
 * dashed square of a comment and of a post (CommentItem.vue, GamePost.vue): one
 * padding around, the dashed $border, the ground of $bg-element and no
 * rounding. The caption is DM2's caption of content (l.dm.am/Admin), "Название
 * места, Автор: имя, дата", in the caption size and grey, its links in the link
 * colour, a small step under the text. What the record cost is said by the
 * decisions under it, not here.
 *
 * The owner asked for this frame by name (28.09): "Посмотри как у нас
 * пунктирно-квадратные хрени у постов, комментов и прочего". It stood on the
 * darker $bg-element-accent before, which on the site marks a deleted comment.
 *
 * A warning draws the offending text in it (WarningCard), an entry of the ban
 * log the sentence of who banned whom and why (BanLogCard), a ticket the text
 * the reporter wrote (TicketThread); the text is optional, and a rectangle
 * without it is the caption alone.
 *
 * A record that has a name of its own heads the rectangle with it, the way a
 * topic does (TopicCard.vue, .topic-title): bold, a dashed rule under it. A
 * ticket is the one so far, named by its reason. Under the heading stand its
 * details, one to a line and each a short "Слово: имя", so none of them wraps
 * or runs off the side however narrow the record is: who wrote it, about whom,
 * who took it.
 *
 * The caption wraps between its parts at every width, as the captions of
 * topics and comments do: on a phone it is the caption of the full version,
 * with nothing put away for want of room (owner, 12 October 2026: no "Еще",
 * no "…", no name cut).
 */
import { computed } from "vue";
import { getActivePinia } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import { CaptionLine } from "@/shared/ui/CaptionLine";

// "Перенос слов" of the settings panel, as over the text of a post and of a
// comment. The switch lives in the store the app installs; a record drawn
// where none is installed, a card on its own, keeps the switch's default, on.
const pinia = getActivePinia();
const ui = pinia ? useUiStore(pinia) : null;
const typesetting = computed(() => ({
  hyphenated: ui ? ui.hyphenateWords : true,
}));
</script>

<template>
  <div class="record-block">
    <h2 v-if="$slots.title" class="record-block__title">
      <slot name="title" />
    </h2>
    <!-- Every direct child of the details slot is a line of its own. -->
    <div v-if="$slots.details" class="record-block__details">
      <slot name="details" />
    </div>
    <div v-if="$slots.default" class="record-block__text" :class="typesetting">
      <slot />
    </div>
    <!-- Every direct child of the caption slot is a part of the line. -->
    <div v-if="$slots.caption" class="record-block__caption">
      <CaptionLine><slot name="caption" /></CaptionLine>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *

// The comment's frame (CommentItem.vue, .comment): the same padding, border and
// ground, so a record reads as a piece of content of the site.
.record-block
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

// The heading of a topic (TopicCard.vue, .topic-title): the text's size in
// bold, a small step and the dashed rule of the frame under it.
.record-block__title
  margin: 0 0 $small
  padding-bottom: $small
  border-bottom: 1px dashed $border
  font-size: $font-size
  font-weight: bold
  overflow-wrap: anywhere

// The details: grey lines of the secondary size, a small step over the text.
// A link in them keeps the colour of a link.
.record-block__details
  margin-bottom: $small
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4

  > :deep(*)
    display: block

// The text of a comment (CommentItem.vue, .comment-text): its colour, and the
// block DM2 set every text in (+body-text), 16px on a step of 20, flush on both
// sides at every width, its words broken while "Перенос слов" is on.
.record-block__text
  color: $text
  +body-text
  overflow-wrap: anywhere

  & + .record-block__caption
    margin-top: $small

// The caption's size on the block as well, so the line it stands on is the
// caption's height and not the text's.
.record-block__caption
  font-size: $tertiary-font-size
</style>
