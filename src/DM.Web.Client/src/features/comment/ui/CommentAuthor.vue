<script setup lang="ts">
import { computed } from "vue";
import type { Comment } from "@/shared/api/models/common/comment";
import { RemovedAccountLink, UserRoleBadge } from "@/entities/user";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { GUEST_NAME, commentAuthor } from "../model/commentAuthor";

const props = defineProps<{ comment: Comment; guest: boolean }>();

const {
  online: isAuthorOnline,
  roleBadge,
  to: authorTo,
  alt: authorAlt,
} = commentAuthor(
  () => props.comment,
  () => props.guest,
);

const formattedDate = computed(() =>
  props.comment.createdUtc ? formatDateFull(props.comment.createdUtc) : "",
);
</script>

<template>
  <div class="author-inner">
    <!-- Every part at every width: the line breaks only at the .author-gap spaces. -->
    <span class="author-line"
      ><router-link
        v-if="comment.author"
        :to="{
          name: 'profile',
          params: { username: comment.author.username },
        }"
        class="author-name"
        >{{ comment.author.username }}</router-link
      ><span v-else-if="guest" class="author-name">{{ GUEST_NAME }}</span
      ><RemovedAccountLink v-else class="author-name" /><template
        v-if="roleBadge"
        ><span class="author-gap">{{ " " }}</span
        ><span class="author-role"
          ><UserRoleBadge
            :badge="roleBadge"
            variant="column" /></span></template
      ><template v-if="comment.author"
        ><span class="author-gap">{{ " " }}</span
        ><span class="author-presence"
          >[<span :class="isAuthorOnline ? 'online' : 'offline'">{{
            isAuthorOnline ? "online" : "offline"
          }}</span
          >]</span
        ></template
      ></span
    >
    <router-link v-if="authorTo" :to="authorTo" class="avatar-link">
      <AvatarImg
        :picture="comment.author?.picture"
        :alt="authorAlt"
        :size="128"
        img-class="avatar"
      />
    </router-link>
    <span v-else class="avatar-link">
      <AvatarImg :alt="authorAlt" :size="128" img-class="avatar" />
    </span>
    <span v-if="comment.createdUtc" class="comment-date">{{
      formattedDate
    }}</span>
    <slot />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// 11px from the top and 5px from the sides, 2px more on the right: 128 inside.
.author-inner
  margin: 11px 5px
  padding-right: $tiny

.author-line
  display: block
  padding-bottom: $minor

.avatar-link
  display: block

.avatar
  display: block
  box-sizing: border-box
  width: 100%
  height: auto
  margin: $tiny 0
  border: 1px solid $border

// Of a fixed width, ending inside the column, short of the spacer.
.comment-date
  display: block
  color: $text
  white-space: nowrap

.author-name
  font-weight: 700
  font-size: $font-size
  color: $text

a.author-name
  +link-tone($text)

// Selectable bracketed text, never a status dot.
.online
  color: $accent-green

.offline
  color: $text-muted

// Each part goes to the next line whole; nowrap would also stop the break of an over-long login.
.author-name,
.author-role,
.author-presence
  display: inline-block
  max-width: 100%

// The line's own space and its one break point; 0.2676em is the space advance of PT Sans.
.author-gap
  word-spacing: calc(#{$tiny} - 0.2676em)

.author-presence > .online,
.author-presence > .offline
  padding: 0 $tiny
</style>
