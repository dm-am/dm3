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
//   - star: red, only when a pendency in this room awaits the current user.
//     Rooms carry no tooltip, the star's aria-label is the whole affordance.
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { RoomType, RoomAccessType, type Room } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { SvgIcon } from "@/shared/ui/Icon";
import SidebarCounter from "./SidebarCounter.vue";

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

// Whether this viewer may open the room is the server's answer, given by the
// same filter that guards the room page itself, so the row and the page
// behind it cannot disagree. Reads that return a room at all return one the
// viewer may open, and those omit the flag.
const canView = computed(() => props.room.canView !== false);

const unread = computed(() => props.room.unreadPostsCount ?? 0);

// Only unfulfilled expectations count: a pendency stays in the payload after
// the post that answered it has landed.
const myPendencies = computed(() =>
  (props.room.pendencies ?? []).filter(
    (p) => !p.fulfilledUtc && p.waitingFor?.username === user.value?.username,
  ),
);
const awaitsMe = computed(() => myPendencies.value.length > 0);
const starLabel = computed(
  () =>
    `Вашего хода ждут: ${myPendencies.value
      .map((p) => p.characterName)
      .join(", ")}`,
);

const STAR = "★";
</script>

<template>
  <li class="link">
    <span v-if="prefix" class="muted" aria-hidden="true">{{ prefix }}</span>
    <SvgIcon
      v-if="isPrivate"
      name="locked"
      class="lock"
      :class="{ granted: canView }"
    />
    <router-link v-if="canView" class="title" :to="to">{{
      room.title
    }}</router-link>
    <span v-else class="title no-access">{{ room.title }}</span>
    <SidebarCounter :value="unread" />
    <span v-if="awaitsMe" class="star" :aria-label="starLabel">{{ STAR }}</span>
  </li>
</template>

<style scoped lang="sass">
.link
  display: block

.muted
  color: $text-muted

.lock
  color: $text-muted
  vertical-align: -0.1em
  margin-right: 2px

  &.granted
    color: $accent-green

// A room the viewer may not open is not a link and must not look like one.
.no-access
  color: $text-muted

.star
  color: $accent-red
  margin-left: 4px
  cursor: default
</style>
