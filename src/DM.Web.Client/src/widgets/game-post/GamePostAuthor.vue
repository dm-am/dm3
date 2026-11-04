<script setup lang="ts">
/** What stands in the author's column of a post; the last line is the host's slot. */
import { computed } from "vue";
import type { Post } from "@/entities/game";
import { RemovedAccountLink, UserLink } from "@/entities/user";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { formatDateFull } from "@/shared/lib/utils/datetime";

const props = defineProps<{ post: Post }>();

const canLinkCharacter = computed(
  () => !!props.post.character?.id && !!props.post.room?.game?.id,
);

const formattedDate = computed(() => formatDateFull(props.post.createdUtc));
</script>

<template>
  <div class="meta-inner">
    <template v-if="post.character">
      <!-- The character's own page, as the game's roster links it -->
      <router-link
        v-if="canLinkCharacter"
        class="character-name"
        :to="{
          name: 'game-character',
          params: {
            id: post.room?.game?.publicId || post.room?.game?.id,
            characterId: post.character.id,
          },
        }"
        >{{ post.character.name }}</router-link
      >
      <span v-else class="character-name">{{ post.character.name }}</span>
    </template>
    <span v-else-if="post.authorGameRole" class="game-role">{{
      post.authorGameRole
    }}</span>
    <UserLink v-if="post.author" :user="post.author" hide-badge />
    <!-- No author: an account removed since -->
    <RemovedAccountLink v-else />

    <!-- No default picture for a character; 128 is the drawn width -->
    <AvatarImg
      v-if="post.character"
      :picture="post.character.picture"
      :alt="post.character.name || ''"
      :size="128"
      img-class="avatar"
      no-default
    />

    <span class="post-date">{{ formattedDate }}</span>

    <slot />
  </div>
</template>

<style scoped lang="sass">
// DM2's div.p inside td.firstrow, leaving 128 for the content.
.meta-inner
  margin: 11px 5px
  padding-right: $tiny

  // DM2 set the parts as blocks.
  > *
    display: block

// The character name and the role in the alt heading colour; the role bold.
.character-name,
.game-role
  padding-bottom: $minor
  color: $heading-alt

a.character-name
  &:hover
    color: $link-hover

.game-role
  font-weight: bold

.avatar
  display: block
  box-sizing: border-box
  width: 100%
  max-height: 500px
  height: auto
  margin: $tiny 0
  border: 1px solid $border

// One line as on DM2; a few pixels wider than 128, it runs into the column's spare 7px.
.post-date
  color: $text
  white-space: nowrap
</style>
