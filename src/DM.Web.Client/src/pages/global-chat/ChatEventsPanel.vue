<script setup lang="ts">
/**
 * ChatEventsPanel — one thin events strip pinned at the top of the chat frame,
 * layered over the scrolling feed.
 *
 * The line always leads with ONE focal event — the live one if something is
 * running, otherwise the nearest scheduled one — shown in full: a bold "Идет:"
 * label (live only), the title, and the muted time ("до HH:mm" live /
 * "DD.MM в HH:mm" scheduled, + ", закрытый" for invite-only events). Every
 * other upcoming event is demoted to one quiet count link "+N запланировано"
 * ("+N ..., ближайший DD.MM" while a live event holds the line); clicking it
 * unrolls the full upcoming list. There is no arrow browsing — the focal event
 * is derived (live ?? nearest), so there is no index to desync when an event
 * starts or ends in realtime.
 *
 * The strip carries no controls of its own beyond the chevron that discloses
 * the focal event's description: search and the archive date live on the
 * search row above the chat frame, in the site's filter-bar idiom.
 *
 * Two floating layers hang below the strip — the upcoming list and the
 * description overlay — mutually exclusive, each kept mounted and toggled by an
 * ".open" class so they animate open/closed with the site's one reveal idiom
 * ($expand-duration/$expand-easing), and marked `inert` while closed so their
 * content is out of the tab order and the a11y tree. They float over the feed
 * (absolute, below the strip), so the message list never shifts.
 *
 * Copy-friendly: the focal composite is one inline-flow run with real spaces,
 * so selecting the line copies exactly "Идет: Title, до 22:48" as one string.
 */
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import dayjs from "dayjs";
import { storeToRefs } from "pinia";
import { useGlobalChatStore } from "@/entities/global-chat";
import { SvgIcon } from "@/shared/ui/Icon";
import { ContentText } from "@/shared/ui";

const store = useGlobalChatStore();
const { liveEvent, upcomingEvents, eventDetails } = storeToRefs(store);

const rootRef = ref<HTMLElement | null>(null);

// ─────────────────────────────────────────────────────────────
// Focal event = the live one if running, else the nearest upcoming. Derived,
// so a realtime start/end simply re-picks it — no index state to desync.
// ─────────────────────────────────────────────────────────────
const primaryEvent = computed(
  () => liveEvent.value ?? upcomingEvents.value[0] ?? null,
);

const isPrimaryLive = computed(
  () =>
    !!liveEvent.value &&
    !!primaryEvent.value &&
    primaryEvent.value.id === liveEvent.value.id,
);

// Everything not on the focal line, collapsed behind the count: all upcoming
// while a live event leads, otherwise the upcoming after the shown nearest one.
const restEvents = computed(() =>
  liveEvent.value ? upcomingEvents.value : upcomingEvents.value.slice(1),
);
const restCount = computed(() => restEvents.value.length);

// "..., ближайший DD.MM" is shown only while a live event holds the line — when
// an upcoming event is the focal one, it already IS the nearest.
const nearestText = computed(() => {
  if (!isPrimaryLive.value) return "";
  const first = upcomingEvents.value[0];
  return first ? `, ближайший ${dayjs(first.startsUtc).format("DD.MM")}` : "";
});

/** Parse a .NET TimeSpan string ("[d.]hh:mm:ss[.fffffff]") into minutes. */
function parseDurationMinutes(duration: string): number | null {
  const match = /^(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})/.exec(duration);
  if (!match) return null;
  const [, days, hours, minutes] = match;
  return Number(days ?? 0) * 24 * 60 + Number(hours) * 60 + Number(minutes);
}

// "до HH:mm" for a live event (end = scheduled start + duration; date appended
// when it runs past midnight). Empty when the duration is not yet cached OR the
// computed end is already in the past (manual start can run late, so a stale
// "до 22:48" at 23:10 would mislead — drop it and let bare "Идет:" stand).
function liveEndText(ev: { startsUtc: string; id: string }): string {
  const duration = eventDetails.value[ev.id]?.duration;
  if (!duration) return "";
  const minutes = parseDurationMinutes(duration);
  if (!minutes) return "";
  const end = dayjs(ev.startsUtc).add(minutes, "minute");
  if (end.isBefore(dayjs())) return "";
  return end.isSame(dayjs(), "day")
    ? `до ${end.format("HH:mm")}`
    : `до ${end.format("DD.MM.YYYY [в] HH:mm")}`;
}

// Muted tail after the title: time (may be empty for a live event whose end is
// unknown/passed) plus ", закрытый" for invite-only events. Leads with a comma
// only when something follows, so "Идет: Title" stays clean when both are empty.
const primaryMeta = computed(() => {
  const ev = primaryEvent.value;
  if (!ev) return "";
  const time = isPrimaryLive.value
    ? liveEndText(ev)
    : dayjs(ev.startsUtc).format("DD.MM [в] HH:mm");
  const parts = [time, ev.isOpen ? "" : "закрытый"].filter(Boolean);
  return parts.length ? `, ${parts.join(", ")}` : "";
});

// ─────────────────────────────────────────────────────────────
// Floating layers — mutually exclusive, animated, focus-returning
// ─────────────────────────────────────────────────────────────
const overlayOpen = ref(false);
const listOpen = ref(false);
let lastTrigger: HTMLElement | null = null;

function closeAll() {
  overlayOpen.value = false;
  listOpen.value = false;
}

function toggle(which: "overlay" | "list", e: MouseEvent) {
  const flag = which === "overlay" ? overlayOpen : listOpen;
  const willOpen = !flag.value;
  closeAll();
  flag.value = willOpen;
  lastTrigger = willOpen ? (e.currentTarget as HTMLElement) : null;
}

// Overlay details for the focal event.
const overlayDetails = computed(() =>
  primaryEvent.value
    ? (eventDetails.value[primaryEvent.value.id] ?? null)
    : null,
);

// "время, участников: N[, закрытый]" — organizer link is appended in the
// template (needs router-link), the description block follows below.
const overlayMetaText = computed(() => {
  const ev = primaryEvent.value;
  if (!ev) return "";
  const time = isPrimaryLive.value
    ? liveEndText(ev) || "идет"
    : dayjs(ev.startsUtc).format("DD.MM.YYYY [в] HH:mm");
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

// Lazily fill the details cache for the focal event — covers first open and the
// focal event changing (a live one starting/ending) while the overlay stays up.
watch([primaryEvent, overlayOpen], ([ev, open]) => {
  if (open && ev) void store.fetchEventDetails(ev.id);
  else if (open && !ev) overlayOpen.value = false;
});

// Close the list / overlay if their subject disappears out from under them.
watch(restCount, (n) => {
  if (n === 0) listOpen.value = false;
});
watch(primaryEvent, (ev) => {
  if (!ev) overlayOpen.value = false;
});

// ─────────────────────────────────────────────────────────────
// Dismissal: Escape and click outside close whatever is open; Escape returns
// focus to the control that opened it (click-outside lands where the user
// clicked, so it keeps its own focus).
// ─────────────────────────────────────────────────────────────
function onDocClick(e: MouseEvent) {
  if (rootRef.value && !rootRef.value.contains(e.target as Node)) closeAll();
}

function onKeydown(e: KeyboardEvent) {
  if (e.key !== "Escape") return;
  const wasOpen = overlayOpen.value || listOpen.value;
  closeAll();
  if (wasOpen && lastTrigger) {
    lastTrigger.focus();
    lastTrigger = null;
  }
}

onMounted(() => {
  document.addEventListener("click", onDocClick);
  document.addEventListener("keydown", onKeydown);
});
onUnmounted(() => {
  document.removeEventListener("click", onDocClick);
  document.removeEventListener("keydown", onKeydown);
});
</script>

<template>
  <div ref="rootRef" class="chat-events-panel">
    <div class="panel-row">
      <!-- Focal event composite (or the empty placeholder), then the quiet
           count of everything else. -->
      <div class="row-main">
        <template v-if="primaryEvent">
          <span class="row-text" aria-live="polite"
            ><span v-if="isPrimaryLive" class="row-status">Идет:{{ " " }}</span
            ><span class="row-title" :class="{ upcoming: !isPrimaryLive }">{{
              primaryEvent.title
            }}</span
            ><span class="row-meta">{{ primaryMeta }}</span></span
          ><button
            type="button"
            class="disc-toggle"
            :class="{ open: overlayOpen }"
            :aria-expanded="overlayOpen"
            aria-controls="chat-event-overlay"
            aria-label="Описание"
            title="Описание"
            @click="toggle('overlay', $event)"
          >
            <SvgIcon name="chevronDown" /></button
          ><span v-if="restCount > 0" class="row-more"
            ><span class="row-sep" aria-hidden="true">|{{ " " }}</span
            ><button
              type="button"
              class="rest-toggle"
              :class="{ act: listOpen }"
              :aria-expanded="listOpen"
              aria-controls="chat-event-list"
              @click="toggle('list', $event)"
            >
              +{{ restCount }} запланировано</button
            ><span v-if="nearestText" class="rest-near">{{
              nearestText
            }}</span></span
          >
        </template>
        <span v-else class="row-none">Нет запланированных эвентов</span>
      </div>
    </div>

    <!-- Upcoming list: unrolls from the strip, floats over the feed. -->
    <div
      v-if="restCount > 0"
      id="chat-event-list"
      class="event-reveal rest-list"
      :class="{ open: listOpen }"
      :inert="!listOpen"
    >
      <div class="reveal-clip">
        <div class="reveal-inner">
          <div v-for="ev in restEvents" :key="ev.id" class="rest-item">
            <span class="rest-title">{{ ev.title }}</span
            ><span class="rest-meta"
              >, {{ dayjs(ev.startsUtc).format("DD.MM [в] HH:mm")
              }}<template v-if="!ev.isOpen">, закрытый</template></span
            >
          </div>
        </div>
      </div>
    </div>

    <!-- Description overlay for the focal event. -->
    <div
      v-if="primaryEvent"
      id="chat-event-overlay"
      class="event-reveal event-overlay"
      :class="{ open: overlayOpen }"
      :inert="!overlayOpen"
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
@import "@/assets/styles/Inputs"
@import "@/assets/styles/ZIndex"
@import "@/assets/styles/Animations"

// One thin surface pinned to the top of the chat frame. The dashed bottom
// border hands over to the feed below. No rounding — this is an informational
// strip, not a control (rounding is reserved for inputs/buttons/dropdowns).
.chat-events-panel
  position: relative
  flex-shrink: 0
  background-color: $bg-element
  border-bottom: 1px dashed $border
  font-size: $secondary-font-size

// Self-sizing flex row: the focal text (grows, ellipsis) on the left, the
// control cluster (fixed) on the right. No absolute aside, no magic reserve.
.panel-row
  display: flex
  align-items: center
  justify-content: space-between
  gap: $medium
  padding: $small $medium
  line-height: 1.4
  white-space: nowrap

.row-main
  display: flex
  align-items: center
  gap: $minor
  flex: 1 1 auto
  min-width: 0

// The focal composite: one selectable inline run (status + title + meta),
// ellipsis-clipped so the title truncates before the count is ever lost.
.row-text
  flex: 0 1 auto
  min-width: 0
  overflow: hidden
  text-overflow: ellipsis
  white-space: nowrap

.row-status
  color: $text
  font-weight: 600

.row-title
  color: $text
  font-weight: 600
  &.upcoming
    font-weight: 500

.row-meta
  color: $text-muted

// Description disclosure — a quiet chevron on the event line that reveals the
// focal event's own description; it rotates as the overlay opens. An icon
// control (not a text link), so it darkens on hover rather than turning blue.
.disc-toggle
  flex: 0 0 auto
  display: inline-flex
  align-items: center
  justify-content: center
  width: 20px
  height: 20px
  padding: 0
  border: none
  background: none
  color: $text-muted
  cursor: pointer
  line-height: 0
  svg
    width: 14px
    height: 14px
    transition: transform $expand-duration $expand-easing
  &.open
    color: $text
    svg
      transform: rotate(180deg)
  &:hover
    color: $text
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px

// The quiet count of everything else — never truncated.
.row-more
  flex: 0 0 auto
  color: $text-muted

.row-sep
  color: $text-muted

.rest-near
  color: $text-muted

// Count of the remaining events. Calm at rest ($text-muted, blends into the
// info line), link-blue + underline on hover, and $link/600 while its list is
// open — so blue appears on intent, not scattered across the strip.
.rest-toggle
  +inline-link-button
  &
    font-size: $secondary-font-size
    white-space: nowrap
    color: $text-muted
  &:hover:not(:disabled)
    color: $link
  &.act
    color: $link
    font-weight: 600
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px

.row-none
  color: $text-muted

// ─────────────────────────────────────────────────────────────
// Floating reveals — the upcoming list and the description overlay float over
// the feed (absolute below the strip), so opening one never shifts the message
// list. Because they are absolutely positioned (indefinite height), the
// grid-rows reveal cannot resolve a track height here — so they use the site's
// floating-overlay idiom instead: opacity + a small drop, on the same $expand
// tokens (matching the calendar), which reads as "the layer settled in from the
// strip" and cures the previous instant snap.
.event-reveal
  position: absolute
  top: 100%
  left: 0
  right: 0
  z-index: $z-dropdown
  opacity: 0
  transform: translateY(-$small)
  pointer-events: none
  transition: opacity $expand-duration $expand-easing, transform $expand-duration $expand-easing
  &.open
    opacity: 1
    transform: translateY(0)
    pointer-events: auto

// The visible layer surface — square corners, element background, hairline and
// a soft shadow so it reads as a floating layer over the feed. (.reveal-clip is
// a bare structural wrapper — no clipping, so the shadow is never cut off.)
.reveal-inner
  padding: $small $medium
  background-color: $bg-element
  border: 1px solid $border
  box-shadow: 0 2px 8px $shadow-color
  max-height: 320px
  overflow-y: auto

// Upcoming list rows — quiet informational text (no event-page route exists to
// link to); each a block line so a selection copies one event per line.
.rest-item
  line-height: 1.4

.rest-title
  color: $text

.rest-meta
  color: $text-muted

// Description overlay head is inline flow — "Title, время, участников: N,
// организатор: X" copies as one line.
.overlay-head
  line-height: 1.4

.overlay-title
  font-weight: 600
  color: $text

.overlay-meta
  color: $text-muted

.overlay-organizer
  +muted-link

// Line-height comes from the global .bbcode-content (SSOT) on the same element.
.overlay-description
  margin-top: $small

.overlay-loading
  display: block
  margin-top: $small
</style>
