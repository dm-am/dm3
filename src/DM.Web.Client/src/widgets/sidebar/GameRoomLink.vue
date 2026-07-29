<script setup lang="ts">
// Sidebar row for a single game room, used by both the active and the
// archived room lists in GamePanel. Layout:
//
//   - {lock?} {title} ({unread>0}) {star?}
//
// Affordances (all computed strictly from the CURRENT viewer's own data —
// the row must never reveal which OTHER members have access):
//   - lock: shown only for Private rooms. Grey by default, green when the
//     current user has a matching room.accesses entry (character owner or
//     explicit reader).
//   - unread: "(N)" muted counter, only when unreadPostsCount > 0.
//   - star: red, only when a pendency in this room awaits the current user;
//     its tooltip names the character(s) whose turn it is.
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { RoomType, RoomAccessType, type Room } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { SvgIcon } from "@/shared/ui/Icon";
import { Tooltip } from "@/shared/ui/Tooltip";

const props = withDefaults(
  defineProps<{
    room: Room;
    /** Game public id used to build the room route params. */
    gamePublicId: string;
    prefix?: string;
  }>(),
  { prefix: "- " },
);

const { user } = storeToRefs(useAuthStore());

const to = computed(() => ({
  name: props.room.type === RoomType.Chat ? "game-chat-room" : "game-room",
  params: { id: props.gamePublicId, num: props.room.roomNumber },
}));

const isPrivate = computed(() => props.room.access === RoomAccessType.Private);

// Green lock only when THIS viewer has an explicit access grant; otherwise
// grey. Never inspects other members' grants beyond the current user.
const hasAccess = computed(() => {
  const me = user.value?.username;
  if (!me) return false;
  return (
    props.room.accesses?.some(
      (a) => a.user?.username === me || a.character?.author?.username === me,
    ) ?? false
  );
});

const unread = computed(() => props.room.unreadPostsCount ?? 0);

const myPendings = computed(() =>
  (props.room.pendings ?? []).filter(
    (p) => p.awaitingUser?.username === user.value?.username,
  ),
);
const awaitsMe = computed(() => myPendings.value.length > 0);
const starTooltip = computed(
  () =>
    `Вашего хода ждут: ${myPendings.value
      .map((p) => p.characterName)
      .join(", ")}`,
);

const STAR = "★";
</script>

<template>
  <li class="link">
    <span class="muted" aria-hidden="true">{{ prefix }}</span>
    <SvgIcon
      v-if="isPrivate"
      name="locked"
      class="lock"
      :class="{ granted: hasAccess }"
    />
    <router-link class="title" :to="to">{{ room.title }}</router-link>
    <span v-if="unread > 0" class="unread">&nbsp;({{ unread }})</span>
    <Tooltip v-if="awaitsMe" :text="starTooltip">
      <span class="star" aria-label="Ожидается ваш ход">{{ STAR }}</span>
    </Tooltip>
  </li>
</template>

<style scoped lang="sass">
.link
  display: block

.muted
  color: $text-muted
  user-select: none

.lock
  color: $text-muted
  vertical-align: -0.1em
  margin-right: 2px

  &.granted
    color: $accent-green

.unread
  color: $text-muted

.star
  color: $accent-red
  margin-left: 4px
  cursor: default
</style>
