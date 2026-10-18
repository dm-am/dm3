<script setup lang="ts">
/**
 * ChatEventActions — the one thing the viewer is entitled to do to a global
 * chat event: sign up for it, give the place back, or, as its organizer, take
 * it live and close it.
 *
 * WHERE IT SITS, and why not on the strip. The events strip above the feed is a
 * summary: one line, the same line for everybody, and on a narrow screen its
 * title is already the last thing left standing. Two facts rule an action item
 * out of it. First, the label depends on the viewer, so a strip that carries it
 * stops being one line everyone can read together. Second, the label cannot
 * even be chosen from what the strip is drawn with: the list endpoint sends a
 * summary with no participants in it, so "Участвовать" and "Отменить участие"
 * are indistinguishable until the details of the event arrive — and those are
 * fetched when the card opens. Putting the action where the data lands is also
 * putting it where its result is legible: the card is the only place that
 * prints the organizer and the count of participants the action changes.
 *
 * The control is an inline link-button, the site's idiom for an action written
 * inside a sentence, and not a filled button: the card is a text surface.
 *
 * States, all of them written here rather than described elsewhere:
 *   - signed out: nothing at all, an action nobody may take is not shown
 *   - details not read yet: nothing, the answer is not known yet
 *   - organizer: "Начать" while scheduled, "Закончить" while live
 *   - participant: "Отменить участие", which the API refuses to an organizer
 *   - anybody else, open event: "Участвовать"
 *   - anybody else, closed event: one quiet line saying why there is no control
 *   - request in flight: the control stays put and goes disabled
 *   - refusal: the sentence the server sent, in a line under the control
 *
 * SUCCESS IS SILENT, and the states above are the reason. All four endpoints
 * answer with the whole event, the store drops that answer straight into the
 * details cache, and every line of the list is read back out of it — so the
 * press rewrites the very word it was made on. "Участвовать" becomes "Отменить
 * участие" and back; "Начать" becomes "Закончить"; the end of an event takes
 * the control away with it. The card around the control moves in the same
 * breath, its "участников: N" being the length of the same participants list,
 * and so does the strip above the feed, which the store re-reads. A plate in
 * the corner would be a second, quieter copy of all of that — the rule is
 * UI_STANDARDS, "Всплывающие сообщения".
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import { useGlobalChatStore } from "@/entities/global-chat";
import { useAuthStore } from "@/shared/stores";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const props = defineProps<{
  /** The event the surrounding card describes. */
  eventId: string;
}>();

const store = useGlobalChatStore();
const { eventDetails } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

const pending = ref(false);

/** Only the details carry the participants, and only they decide the label. */
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

/** What this viewer may do, mirroring GlobalChatEventIntentionResolver. */
const action = computed<ActionKind | null>(() => {
  const ev = event.value;
  if (!ev || !viewer.value) return null;
  if (membership.value?.isOrganizer) {
    if (ev.status === "Scheduled") return "start";
    return ev.status === "Live" ? "end" : null;
  }
  if (membership.value) return "leave";
  if (!ev.isOpen) return null;
  return ev.status === "Scheduled" || ev.status === "Live" ? "join" : null;
});

const LABELS: Record<ActionKind, string> = {
  join: "Участвовать",
  // Not "Выйти": that word is the sign-out of the whole site, in the header
  // and on the account page.
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

/**
 * Why there is no control, when the reason is worth a line. A closed event
 * takes its participants from the organizer, and the card's own ", закрытый"
 * says what it is without saying what it means for the reader.
 */
const note = computed(() => {
  const ev = event.value;
  if (!ev || !viewer.value || action.value) return "";
  const upcoming = ev.status === "Scheduled" || ev.status === "Live";
  return !ev.isOpen && upcoming && !membership.value
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

// The refusal of the last press, under the control that was pressed.
const result = useActionResult();

async function run() {
  const kind = action.value;
  if (!kind || pending.value) return;
  pending.value = true;
  result.clear();
  try {
    const { error } = await RUN[kind](props.eventId);
    // Only the refusal is spoken. The server names its own, "Уже идет другой
    // эвент" among them; the fallback is for a request that never reached it.
    // Success needs no sentence: the store has already rewritten the control
    // and the card around it (see the header).
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
@use "@/assets/styles/Inputs" as *

.event-actions
  margin-top: $tiny
  line-height: 1.4

// An action written inside a text surface: link blue, underlined on intent.
// Disabled it recedes to the muted colour of the card's own meta, so the row
// keeps its width and its place while the request is out.
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

// One step under the control, the step the row keeps from the card above it.
.event-refusal
  --action-result-margin: #{$tiny} 0 0
</style>
