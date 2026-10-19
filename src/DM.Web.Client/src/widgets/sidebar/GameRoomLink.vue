<script setup lang="ts">
// A room row of GamePanel, as the old site writes rooms: lock, title, unread or writers, star.
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

// The server's answer, from the filter that guards the room page; absent means yes.
const canView = computed(() => props.room.canView !== false);

const unread = computed(() => props.room.unreadPostsCount ?? 0);

const writers = computed(() =>
  canView.value ? "" : (props.room.characterNames ?? []).join(", "),
);

// A pendency stays in the payload after the post that answered it: only open ones count.
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

const STAR = "*";
</script>

<template>
  <li class="link">
    <span v-if="prefix" class="muted" aria-hidden="true">{{ prefix }}</span>
    <SvgIcon
      name="roomLocked"
      class="lock"
      :class="{ granted: canView, public: !isPrivate }"
      :aria-hidden="!isPrivate"
    />
    <router-link v-if="canView" class="title" :to="to">{{
      room.title
    }}</router-link>
    <span v-else class="title no-access">{{ room.title }}</span>
    <span v-if="writers" class="writers">{{ ` - ${writers}` }}</span>
    <SidebarCounter v-if="canView" :value="unread" />
    <span v-if="awaitsMe" class="star" :aria-label="starLabel">{{ STAR }}</span>
  </li>
</template>

<style scoped lang="sass">
.link
  display: block

// Drawn for every room and hidden on the open ones, so the titles start at one x.
.lock
  color: $border
  vertical-align: -0.1em
  margin-right: $grid-step

  &.granted
    color: $accent-green

  &.public
    visibility: hidden

// A room the viewer may not open is plain text, as the names after it.
.no-access,
.writers
  color: $text

.star
  color: $accent-red
  margin-left: $minor
  cursor: default
</style>
