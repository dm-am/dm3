<script setup lang="ts">
/**
 * The events line at the top of the global chat's frame: one focal event (the
 * live one, else the nearest scheduled) with its state word, title and time,
 * and the rest behind "+N запланировано". The focal event is derived, so a
 * realtime start or end has no index to desync. The line wraps like text and
 * cuts nothing; it says "Нет запланированных эвентов" only once the list has
 * answered. The focal event's action lives in the card, since its label is
 * decided by the participants list the card brings. The upcoming list and the
 * card float over the feed, mutually exclusive, inert while closed, and answer
 * "Развернуть все". The line is one inline run with real spaces, so it copies
 * as it reads (onelineCopy.spec.ts).
 */
import {
  computed,
  nextTick,
  onBeforeUnmount,
  onMounted,
  onUnmounted,
  ref,
  watch,
} from "vue";
import {
  DATE_TIME_FORMAT,
  DAY_MONTH_FORMAT,
  DAY_MONTH_TIME_FORMAT,
  formatSiteTime,
  siteTime,
} from "@/shared/lib/utils/datetime";
import { storeToRefs } from "pinia";
import { useGlobalChatEventsStore } from "@/entities/global-chat";
import { ChatEventActions } from "@/features/chat-event-actions";
import {
  notifyExpandableChanged,
  refreshExpandableStates,
  registerExpandable,
} from "@/shared/lib/composables/useExpandableRegistry";
import { ContentText } from "@/shared/ui/Content";
import { ChatStrip } from "@/widgets/chat-feed";

const store = useGlobalChatEventsStore();
const { liveEvent, upcomingEvents, eventDetails, eventsLoad } =
  storeToRefs(store);

const rootRef = ref<HTMLElement | null>(null);
/** "описание", where the focus goes once a refused read comes back with events. */
const describeRef = ref<HTMLElement | null>(null);

// The focal event: live if running, else the nearest upcoming.
const primaryEvent = computed(
  () => liveEvent.value ?? upcomingEvents.value[0] ?? null,
);

const isPrimaryLive = computed(
  () =>
    !!liveEvent.value &&
    !!primaryEvent.value &&
    primaryEvent.value.id === liveEvent.value.id,
);

// The state word is mandatory: weight alone is a difference no reader can name.
const stateWord = computed(() => (isPrimaryLive.value ? "Идет" : "Скоро"));

// Everything not on the focal line, behind the count.
const restEvents = computed(() =>
  liveEvent.value ? upcomingEvents.value : upcomingEvents.value.slice(1),
);
const restCount = computed(() => restEvents.value.length);

// " запланировано" follows it in the template.
const countLabel = computed(() => `+${restCount.value}`);

// ", ближайший DD.MM" only while a live event holds the line; a no-break space
// keeps the day with its word.
const nearestText = computed(() => {
  if (!isPrimaryLive.value) return "";
  const first = upcomingEvents.value[0];
  return first
    ? `, ближайший\u00a0${formatSiteTime(first.startsUtc, DAY_MONTH_FORMAT)}`
    : "";
});

// In place of an event: a bar before the answer and during a retry, the reason
// of a refused read, or that there is none, once the list said so.
const retrying = ref(false);

const emptyLine = computed<"pending" | "failed" | "none">(() => {
  if (retrying.value || eventsLoad.value === "pending") return "pending";
  return eventsLoad.value === "failed" ? "failed" : "none";
});

const retryRef = ref<HTMLElement | null>(null);

/** Reads the list again; the focus goes to what stands in the button's place once it is back. */
async function retry(e: MouseEvent) {
  const focused = document.activeElement === e.currentTarget;
  retrying.value = true;
  try {
    await store.fetchEvents();
  } finally {
    retrying.value = false;
  }
  if (!focused) return;
  await nextTick();
  (retryRef.value ?? describeRef.value)?.focus();
}

/** Parse a .NET TimeSpan string ("[d.]hh:mm:ss[.fffffff]") into minutes. */
function parseDurationMinutes(duration: string): number | null {
  const match = /^(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})/.exec(duration);
  if (!match) return null;
  const [, days, hours, minutes] = match;
  return Number(days ?? 0) * 24 * 60 + Number(hours) * 60 + Number(minutes);
}

// "до HH:mm" of a live event in the site's clock, a date when it runs past
// midnight; empty when the end is unknown or already past.
function liveEndText(ev: { startsUtc: string; id: string }): string {
  const duration = eventDetails.value[ev.id]?.duration;
  if (!duration) return "";
  const minutes = parseDurationMinutes(duration);
  if (!minutes) return "";
  const end = siteTime(ev.startsUtc).add(minutes, "minute");
  const now = siteTime();
  if (end.isBefore(now)) return "";
  return end.format("YYYY-MM-DD") === now.format("YYYY-MM-DD")
    ? `до ${end.format("HH:mm")}`
    : `до ${end.format(DATE_TIME_FORMAT)}`;
}

// The muted tail after the title, led by a comma only when something follows.
const primaryMeta = computed(() => {
  const ev = primaryEvent.value;
  if (!ev) return "";
  const time = isPrimaryLive.value
    ? liveEndText(ev)
    : formatSiteTime(ev.startsUtc, DAY_MONTH_TIME_FORMAT);
  const parts = [time, ev.isOpen ? "" : "закрытый"].filter(Boolean);
  return parts.length ? `, ${parts.join(", ")}` : "";
});

// The two floating layers share one ref: they stand in the same place over the feed.
type RevealId = "description" | "upcoming";

const openReveal = ref<RevealId | null>(null);
const descriptionOpen = computed(() => openReveal.value === "description");
const upcomingOpen = computed(() => openReveal.value === "upcoming");
let lastTrigger: HTMLElement | null = null;

/** Closes whatever is up; `manual` tells the registry a person did it. */
function closeAll(manual = false) {
  if (!openReveal.value) return;
  openReveal.value = null;
  if (manual) notifyExpandableChanged();
  else refreshExpandableStates();
}

function toggle(which: RevealId, e: MouseEvent) {
  const willOpen = openReveal.value !== which;
  openReveal.value = willOpen ? which : null;
  lastTrigger = willOpen ? (e.currentTarget as HTMLElement) : null;
  notifyExpandableChanged();
}

// "Развернуть все": both layers are content, so they register, as one handle
// since they are mutually exclusive; only while there is an event to reveal.
let unregisterReveal: (() => void) | null = null;

function syncRevealRegistration() {
  const hasSubject = !!primaryEvent.value;
  if (hasSubject && !unregisterReveal) {
    unregisterReveal = registerExpandable({
      id: Symbol("chat-events-panel"),
      isExpanded: () => openReveal.value !== null,
      // Pure state: notifyExpandableChanged() belongs to a manual click only.
      expand: () => {
        if (!openReveal.value) openReveal.value = "description";
      },
      collapse: () => {
        openReveal.value = null;
      },
    });
  } else if (!hasSubject && unregisterReveal) {
    unregisterReveal();
    unregisterReveal = null;
  }
}

// Synchronously, so the ScrollNav button appears with the line.
syncRevealRegistration();
onBeforeUnmount(() => unregisterReveal?.());

const overlayDetails = computed(() =>
  primaryEvent.value
    ? (eventDetails.value[primaryEvent.value.id] ?? null)
    : null,
);

// The organizer link is appended in the template.
const overlayMetaText = computed(() => {
  const ev = primaryEvent.value;
  if (!ev) return "";
  const time = isPrimaryLive.value
    ? liveEndText(ev) || "идет"
    : formatSiteTime(ev.startsUtc, DATE_TIME_FORMAT);
  const parts = [
    time,
    `участников: ${overlayDetails.value?.participants?.length ?? ev.participantCount}`,
  ];
  if (!ev.isOpen) parts.push("закрытый");
  return parts.join(", ");
});

const overlayOrganizer = computed(
  () => overlayDetails.value?.createdBy?.username ?? null,
);

// The details of the focal event, read on the first open and when it changes.
watch([primaryEvent, descriptionOpen], ([ev, open]) => {
  if (open && ev) void store.fetchEventDetails(ev.id);
});

// A layer whose subject went away closes; the registry gets the line's new shape.
watch(primaryEvent, (ev) => {
  if (!ev) closeAll();
  syncRevealRegistration();
});
watch(restCount, (n) => {
  if (n === 0 && upcomingOpen.value) closeAll();
});

// What was open when the press began: a click outside closes that, not what the
// click itself opened, as "Развернуть все" outside the line does.
let openAtPress: RevealId | null = null;

function notePress() {
  openAtPress = openReveal.value;
}

// Escape and a click outside close whatever is open; Escape returns the focus.
function onDocClick(e: MouseEvent) {
  if (openAtPress && rootRef.value && !rootRef.value.contains(e.target as Node))
    closeAll(true);
}

function onKeydown(e: KeyboardEvent) {
  if (e.key !== "Escape") return;
  const trigger = lastTrigger;
  const wasOpen = openReveal.value !== null;
  closeAll(true);
  if (wasOpen && trigger) {
    trigger.focus();
    lastTrigger = null;
  }
}

onMounted(() => {
  document.addEventListener("click", notePress, true);
  document.addEventListener("click", onDocClick);
  document.addEventListener("keydown", onKeydown);
});
onUnmounted(() => {
  document.removeEventListener("click", notePress, true);
  document.removeEventListener("click", onDocClick);
  document.removeEventListener("keydown", onKeydown);
});
</script>

<template>
  <div ref="rootRef" class="chat-events-panel">
    <ChatStrip>
      <!-- The focal event, the description item, the count. The separators are
           literal " | " text nodes and every label a literal interpolation, so the
           line copies without stray spaces; it wraps as text at every width. -->
      <span class="row-main">
        <template v-if="primaryEvent">
          <span class="row-text" aria-live="polite"
            ><span class="row-status">{{ stateWord }}:{{ " " }}</span
            ><span class="row-title" :class="{ upcoming: !isPrimaryLive }">{{
              primaryEvent.title
            }}</span
            ><span class="row-meta">{{ primaryMeta }}</span></span
          ><span class="row-sep" aria-hidden="true">{{ " | " }}</span
          ><button
            ref="describeRef"
            type="button"
            class="strip-item"
            :class="{ act: descriptionOpen }"
            :aria-expanded="descriptionOpen"
            aria-controls="chat-event-overlay"
            @click="toggle('description', $event)"
          >
            {{ "описание" }}</button
          ><span v-if="restCount > 0" class="row-more"
            ><span class="row-sep" aria-hidden="true">{{ " | " }}</span
            ><button
              type="button"
              class="strip-item"
              :class="{ act: upcomingOpen }"
              :aria-expanded="upcomingOpen"
              aria-controls="chat-event-list"
              @click="toggle('upcoming', $event)"
            >
              {{ countLabel
              }}<span class="rest-word">{{ " запланировано" }}</span></button
            ><span v-if="nearestText" class="rest-near">{{
              nearestText
            }}</span></span
          >
        </template>
        <span v-else-if="emptyLine === 'failed'" class="row-failed"
          >{{ "Эвенты не загрузились"
          }}<span class="row-sep" aria-hidden="true">{{ " | " }}</span
          ><button
            ref="retryRef"
            type="button"
            class="strip-item"
            @click="retry"
          >
            {{ "повторить" }}
          </button></span
        >
        <span v-else-if="emptyLine === 'none'" class="row-none"
          >Нет запланированных эвентов</span
        >
        <span v-else class="row-placeholder" aria-hidden="true" />
      </span>
    </ChatStrip>

    <!-- Unrolls from the line, over the feed. -->
    <div
      v-if="restCount > 0"
      id="chat-event-list"
      class="event-reveal rest-list"
      :class="{ open: upcomingOpen }"
      :inert="!upcomingOpen"
    >
      <div class="reveal-clip">
        <div class="reveal-inner">
          <!-- An event a line, the title whole. -->
          <div v-for="ev in restEvents" :key="ev.id" class="rest-item">
            <span class="rest-title">{{ ev.title }}</span
            ><span class="rest-meta"
              >, {{ formatSiteTime(ev.startsUtc, DAY_MONTH_TIME_FORMAT)
              }}<template v-if="!ev.isOpen">, закрытый</template></span
            >
          </div>
        </div>
      </div>
    </div>

    <div
      v-if="primaryEvent"
      id="chat-event-overlay"
      class="event-reveal event-overlay"
      :class="{ open: descriptionOpen }"
      :inert="!descriptionOpen"
    >
      <div class="reveal-clip">
        <div class="reveal-inner">
          <div class="overlay-head">
            <span class="overlay-title">{{ primaryEvent.title }}</span
            ><span class="overlay-meta"
              >, {{ overlayMetaText
              }}<template v-if="overlayOrganizer"
                >, организатор:
                <router-link
                  class="overlay-organizer"
                  :to="{
                    name: 'profile',
                    params: { username: overlayOrganizer },
                  }"
                  >{{ overlayOrganizer }}</router-link
                ></template
              ></span
            >
          </div>
          <!-- The one action this viewer may take, decided by the participants list the
               card brings; above the description, so a long one never pushes it out. -->
          <ChatEventActions :event-id="primaryEvent.id" />
          <content-text
            v-if="overlayDetails?.description"
            class="overlay-description"
            :html="overlayDetails.description"
          />
          <secondary-text v-else-if="!overlayDetails" class="overlay-loading"
            >Описание загружается...</secondary-text
          >
          <secondary-text v-else class="overlay-loading"
            >Без описания</secondary-text
          >
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *
@use "@/assets/styles/Skeleton" as *
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Animations" as *

// The surface is ChatStrip's; this box anchors the layers below it.
.chat-events-panel
  position: relative
  flex-shrink: 0


.row-status
  color: $text
  font-weight: 700

.row-title
  color: $text
  font-weight: 700
  &.upcoming
    font-weight: 400

.row-meta
  color: $text-muted

.row-sep
  color: $text-muted

.rest-near
  color: $text-muted

// The items of the line are grey link-buttons: $link-hover under the pointer,
// $link and bold while the layer they opened is up.
.strip-item
  +muted-link-button
  &
    font-size: $secondary-font-size
    white-space: nowrap
  &.act
    color: $link
    font-weight: 700
  &:focus:not(:focus-visible)
    outline: none

.row-placeholder
  display: inline-block
  width: 180px
  max-width: 100%
  height: 1em
  vertical-align: middle
  +skeleton-shimmer

// The layers float over the feed (absolute), so opening one shifts nothing;
// their height is indefinite, so they reveal by opacity and a small drop on
// the $expand tokens rather than by grid rows.
.event-reveal
  position: absolute
  top: 100%
  left: 0
  right: 0
  z-index: $z-dropdown
  font-size: $secondary-font-size
  opacity: 0
  transform: translateY(-$small)
  pointer-events: none
  transition: opacity $expand-duration $expand-easing, transform $expand-duration $expand-easing
  &.open
    opacity: 1
    transform: translateY(0)
    pointer-events: auto

// The layer surface; .reveal-clip clips nothing, so the shadow is never cut.
.reveal-inner
  padding: $small $medium
  background-color: $bg-element
  border: 1px solid $border
  box-shadow: $shadow-small
  max-height: 320px
  overflow-y: auto

// Quiet text, a block line each, so a selection copies one event per line.
.rest-item
  line-height: 1.4

.rest-title
  color: $text

.rest-meta
  color: $text-muted

// Inline flow, so the head copies as one line.
.overlay-head
  line-height: 1.4

.overlay-title
  font-weight: 700
  color: $text

.overlay-meta
  color: $text-muted

.overlay-organizer
  +muted-link

// The line height is the global .bbcode-content's.
.overlay-description
  margin-top: $small

.overlay-loading
  display: block
  margin-top: $small
</style>
