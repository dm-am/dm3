<script setup lang="ts">
// On the card, not the strip: the label depends on the viewer, and only the details carry participants.
// Success is silent: the store rewrites the control from the answer.
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import { useGlobalChatEventsStore } from "@/entities/global-chat";
import { useAuthStore } from "@/shared/stores";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const props = defineProps<{
  eventId: string;
}>();

const store = useGlobalChatEventsStore();
const { eventDetails } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

const pending = ref(false);

const event = computed(() => eventDetails.value[props.eventId] ?? null);

const viewer = computed(() => user.value?.username ?? null);

const membership = computed(() => {
  const username = viewer.value;
  if (!username) return null;
  return (
    event.value?.participants?.find((p) => p.user.username === username) ?? null
  );
});

type ActionKind = "join" | "leave" | "start" | "end";

const isUpcoming = (status: string) =>
  status === "Scheduled" || status === "Live";

const action = computed<ActionKind | null>(() => {
  const ev = event.value;
  if (!ev || !viewer.value) return null;
  if (membership.value?.isOrganizer) {
    if (ev.status === "Scheduled") return "start";
    return ev.status === "Live" ? "end" : null;
  }
  if (membership.value) return "leave";
  if (!ev.isOpen) return null;
  return isUpcoming(ev.status) ? "join" : null;
});

const LABELS: Record<ActionKind, string> = {
  join: "Участвовать",
  // Not "Выйти": that word is the sign-out of the whole site.
  leave: "Отменить участие",
  start: "Начать",
  end: "Закончить",
};

const FAILED: Record<ActionKind, string> = {
  join: "Не удалось записаться на эвент",
  leave: "Не удалось отменить участие",
  start: "Не удалось начать эвент",
  end: "Не удалось закончить эвент",
};

const label = computed(() => (action.value ? LABELS[action.value] : ""));

const note = computed(() => {
  const ev = event.value;
  if (!ev || !viewer.value || action.value) return "";
  return !ev.isOpen && isUpcoming(ev.status) && !membership.value
    ? "Участников закрытого эвента приглашает организатор"
    : "";
});

type Outcome = Promise<{ error: GeneralError | null }>;

const RUN: Record<ActionKind, (id: string) => Outcome> = {
  join: (id) => store.joinEvent(id),
  leave: (id) => store.leaveEvent(id),
  start: (id) => store.startEvent(id),
  end: (id) => store.endEvent(id),
};

const result = useActionResult();

async function run() {
  const kind = action.value;
  if (!kind || pending.value) return;
  pending.value = true;
  result.clear();
  try {
    const { error } = await RUN[kind](props.eventId);
    // The fallback is for a request that never reached the server.
    if (error) result.refuse(error, FAILED[kind]);
  } finally {
    pending.value = false;
  }
}
</script>

<template>
  <div v-if="label || note" class="event-actions">
    <button
      v-if="label"
      type="button"
      class="event-action"
      :disabled="pending"
      :aria-busy="pending"
      @click="run"
    >
      {{ label }}
    </button>
    <span v-else class="event-note">{{ note }}</span>
    <ActionResult :result="result" class="event-refusal" />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.event-actions
  margin-top: $tiny
  line-height: 1.4

// Disabled, it keeps its width and place while the request is out.
.event-action
  +inline-link-button
  &
    white-space: nowrap
  &:disabled
    color: $text-muted
    cursor: default
    text-decoration: none
  &:focus:not(:focus-visible)
    outline: none

.event-note
  color: $text-muted

.event-refusal
  --action-result-margin: #{$tiny} 0 0
</style>
