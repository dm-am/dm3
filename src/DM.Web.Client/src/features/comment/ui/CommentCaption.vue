<script setup lang="ts">
// The actions come through the slot: the line takes each host node for one part.
import { computed } from "vue";
import type { Comment } from "@/shared/api/models/common/comment";
import { RemovedAccountLink, UserRoleBadge } from "@/entities/user";
import { CaptionLine, CaptionPart } from "@/shared/ui/CaptionLine";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { GUEST_NAME, commentAuthor } from "../model/commentAuthor";

const props = defineProps<{ comment: Comment; guest: boolean }>();

const { online: isAuthorOnline, roleBadge } = commentAuthor(
  () => props.comment,
  () => props.guest,
);

const formattedDate = computed(() =>
  props.comment.createdUtc ? formatDateFull(props.comment.createdUtc) : "",
);

const formattedEditDate = computed(() =>
  props.comment.modifiedUtc ? formatDateFull(props.comment.modifiedUtc) : "",
);
</script>

<template>
  <div class="comment-caption">
    <CaptionLine>
      <CaptionPart
        :joint="comment.createdUtc ? ',' : undefined"
        class="author-info"
        >Автор:
        <router-link
          v-if="comment.author"
          :to="{
            name: 'profile',
            params: { username: comment.author.username },
          }"
          class="author-link"
          >{{ comment.author.username }}</router-link
        ><template v-else-if="guest">{{ GUEST_NAME }}</template
        ><RemovedAccountLink v-else /><UserRoleBadge
          v-if="roleBadge"
          :badge="roleBadge"
          variant="caption"
        /><template v-if="comment.author">
          [<span :class="isAuthorOnline ? 'online' : 'offline'">{{
            isAuthorOnline ? "online" : "offline"
          }}</span
          >]</template
        ></CaptionPart
      >
      <span v-if="comment.createdUtc">{{ formattedDate }}</span>
      <span v-if="comment.modifiedUtc"
        >Отредактировано {{ formattedEditDate }}</span
      >
      <slot />
    </CaptionLine>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// The caption's size on the block too, so the line is the caption's height.
.comment-caption
  flex: 1 1 0%
  min-width: 0
  font-size: $tertiary-font-size

.author-link
  +link-tone($link)

.online
  color: $accent-green

.offline
  color: $text-muted
</style>
