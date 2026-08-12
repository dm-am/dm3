<script setup lang="ts">
/**
 * ChatEventsPanel — one thin events strip pinned at the top of the chat frame,
 * layered over the scrolling feed.
 *
 * The line always leads with ONE focal event — the live one if something is
 * running, otherwise the nearest scheduled one — shown in full: the state word
 * ("Идет:" / "Скоро:"), the title, and the muted time ("до HH:mm" live /
 * "DD.MM в HH:mm" scheduled, + ", закрытый" for invite-only events). The word
 * is not decoration: a scheduled event used to differ from a running one by
 * font weight alone, so the nearest one read as happening now. Every other
 * upcoming event is demoted to one quiet count link "+N запланировано"
 * ("+N ..., ближайший DD.MM" while a live event holds the line); clicking it
 * unrolls the full upcoming list. There is no arrow browsing — the focal event
 * is derived (live ?? nearest), so there is no index to desync when an event
 * starts or ends in realtime.
 *
 * The strip is written in the site's one strip idiom: literal " | " text nodes
 * between items, every item a link-button. Search and the archive date are not
 * here — they live on the search row above the chat frame, in the filter-bar
 * idiom.
 *
 * The card is also where the focal event's one action lives (join, leave, and
 * the organizer's start/end — see features/chat-event-actions): the strip is a
 * summary, one line and the same line for every reader, while the label of an
 * action depends on who is looking and is decided by the participants list,
 * which arrives with the card and not with the strip's summary.
 *
 * Two floating layers hang below the strip — the upcoming list and the
 * description card — mutually exclusive, each kept mounted and toggled by an
 * ".open" class so they animate open/closed with the site's one reveal idiom
 * ($expand-duration/$expand-easing), and marked `inert` while closed so their
 * content is out of the tab order and the a11y tree. They float over the feed
 * (absolute, below the strip), so the message list never shifts. Both are
 * content rather than tools, so they answer the page-wide "Развернуть все".
 *
 * Copy-friendly: the whole line is one inline-flow run with real spaces, so
 * selecting it copies exactly "Идет: Title, до 22:48 | описание | +2
 * запланировано, ближайший 05.08" — one string, the way it reads.
 * onelineCopy.spec.ts holds that shape; a flex row put a line break at every
 * item boundary instead.
 */
import {
  computed,
  onBeforeUnmount,
  onMounted,
  onUnmounted,
  ref,
  watch,
} from "vue";
import dayjs from "dayjs";
import {
  DATE_TIME_FORMAT,
  DAY_MONTH_FORMAT,
  DAY_MONTH_TIME_FORMAT,
} from "@/shared/lib/utils/datetime";
import { storeToRefs } from "pinia";
import { useGlobalChatStore } from "@/entities/global-chat";
import { ChatEventActions } from "@/features/chat-event-actions";
import {
  notifyExpandableChanged,
  refreshExpandableStates,
  registerExpandable,
} from "@/shared/lib/composables/useExpandableRegistry";
import { ContentText } from "@/shared/ui/Content";

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

// The word the line leads with. Mandatory for both states: weight alone (600
// live / 500 scheduled) is a difference no reader can name, and a scheduled
// event without a word reads as one that is already running.
const stateWord = computed(() => (isPrimaryLive.value ? "Идет" : "Скоро"));

// Everything not on the focal line, collapsed behind the count: all upcoming
// while a live event leads, otherwise the upcoming after the shown nearest one.
const restEvents = computed(() =>
  liveEvent.value ? upcomingEvents.value : upcomingEvents.value.slice(1),
);
const restCount = computed(() => restEvents.value.length);

// "+2" and " запланировано" are two parts on purpose: a narrow screen keeps the
// number and drops the noun, so the count never eats the title's width.
const countLabel = computed(() => `+${restCount.value}`);

// "..., ближайший DD.MM" is shown only while a live event holds the line — when
// an upcoming event is the focal one, it already IS the nearest.
const nearestText = computed(() => {
  if (!isPrimaryLive.value) return "";
  const first = upcomingEvents.value[0];
  return first
    ? `, ближайший ${dayjs(first.startsUtc).format(DAY_MONTH_FORMAT)}`
    : "";
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
    : `до ${end.format(DATE_TIME_FORMAT)}`;
}

// Muted tail after the title: time (may be empty for a live event whose end is
// unknown/passed) plus ", закрытый" for invite-only events. Leads with a comma
// only when something follows, so "Идет: Title" stays clean when both are empty.
const primaryMeta = computed(() => {
  const ev = primaryEvent.value;
  if (!ev) return "";
  const time = isPrimaryLive.value
    ? liveEndText(ev)
    : dayjs(ev.startsUtc).format(DAY_MONTH_TIME_FORMAT);
  const parts = [time, ev.isOpen ? "" : "закрытый"].filter(Boolean);
  return parts.length ? `, ${parts.join(", ")}` : "";
});

// ─────────────────────────────────────────────────────────────
// Floating layers — mutually exclusive, animated, focus-returning. One ref
// holds which one is up, because two booleans can disagree and these two
// layers never may: they occupy the same place over the feed.
// ─────────────────────────────────────────────────────────────
type RevealId = "description" | "upcoming";

const openReveal = ref<RevealId | null>(null);
const descriptionOpen = computed(() => openReveal.value === "description");
const upcomingOpen = computed(() => openReveal.value === "upcoming");
let lastTrigger: HTMLElement | null = null;

/**
 * Close whatever is up. `manual` tells the registry a person did it (which
 * clears a pending bulk action); a layer closing because its subject went
 * away only refreshes the aggregate state.
 */
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

// ─────────────────────────────────────────────────────────────
// Page-wide "Развернуть все / Свернуть все". Both layers are CONTENT — the
// focal event's card and the list of upcoming ones — so they belong to the
// registry; search and the archive date are tools and stay out of it by the
// same rule. One handle for the pair, because they are mutually exclusive:
// two handles could never be "all expanded" at once and the ScrollNav label
// would be stuck on "Развернуть все" forever. Registered only while there is
// an event to reveal, or the button would appear over an empty strip and do
// nothing when pressed.
// ─────────────────────────────────────────────────────────────
let unregisterReveal: (() => void) | null = null;

function syncRevealRegistration() {
  const hasSubject = !!primaryEvent.value;
  if (hasSubject && !unregisterReveal) {
    unregisterReveal = registerExpandable({
      id: Symbol("chat-events-panel"),
      isExpanded: () => openReveal.value !== null,
      // Pure state changes: notifyExpandableChanged() belongs to a manual
      // click only — calling it here would clear the bulk action mid-loop.
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

// Register synchronously in setup, so the ScrollNav button appears in the same
// frame as the strip it belongs to.
syncRevealRegistration();
onBeforeUnmount(() => unregisterReveal?.());

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
    : dayjs(ev.startsUtc).format(DATE_TIME_FORMAT);
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
// focal event changing (a live one starting/ending) while the card stays up.
watch([primaryEvent, descriptionOpen], ([ev, open]) => {
  if (open && ev) void store.fetchEventDetails(ev.id);
});

// Close a layer whose subject disappeared out from under it, and hand the
// registry the strip's new shape in the same step.
watch(primaryEvent, (ev) => {
  if (!ev) closeAll();
  syncRevealRegistration();
});
watch(restCount, (n) => {
  if (n === 0 && upcomingOpen.value) closeAll();
});

// ─────────────────────────────────────────────────────────────
// Dismissal: Escape and click outside close whatever is open; Escape returns
// focus to the control that opened it (click-outside lands where the user
// clicked, so it keeps its own focus).
// ─────────────────────────────────────────────────────────────
function onDocClick(e: MouseEvent) {
  if (rootRef.value && !rootRef.value.contains(e.target as Node))
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
      <!-- Focal event composite (or the empty placeholder), the description
           item, then the quiet count of everything else. The separators are
           literal " | " text nodes, and every label a reader would copy is
           written as a literal interpolation: a bare word after the tag picks
           up the formatter's line break as a leading space, which is how the
           strip used to copy as "|  +2". -->
      <div class="row-main">
        <template v-if="primaryEvent">
          <span class="row-text" aria-live="polite"
            ><span class="row-status">{{ stateWord }}:{{ " " }}</span
            ><span class="row-title" :class="{ upcoming: !isPrimaryLive }">{{
              primaryEvent.title
            }}</span
            ><span class="row-meta">{{ primaryMeta }}</span></span
          ><span class="row-sep" aria-hidden="true">{{ " | " }}</span
          ><button
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
        <span v-else class="row-none">Нет запланированных эвентов</span>
      </div>
    </div>

    <!-- Upcoming list: unrolls from the strip, floats over the feed. -->
    <div
      v-if="restCount > 0"
      id="chat-event-list"
      class="event-reveal rest-list"
      :class="{ open: upcomingOpen }"
      :inert="!upcomingOpen"
    >
      <div class="reveal-clip">
        <div class="reveal-inner">
          <div v-for="ev in restEvents" :key="ev.id" class="rest-item">
            <span class="rest-title">{{ ev.title }}</span
            ><span class="rest-meta"
              >, {{ dayjs(ev.startsUtc).format(DAY_MONTH_TIME_FORMAT)
              }}<template v-if="!ev.isOpen">, закрытый</template></span
            >
          </div>
        </div>
      </div>
    </div>

    <!-- Description card for the focal event. -->
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
          <!-- The one action this viewer may take. It lives in the card and
               not on the strip: the label is per-viewer, and it is decided by
               the participants list, which only the card's own request
               brings. Above the description, so a long description never
               pushes it out of the card's scroll. -->
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

// The strip's own box: one row, vertically centred, never scrolled. It holds a
// single child now that search and the archive date sit above the frame, so the
// flex here only centres and pads — the line itself is inline flow inside it.
.panel-row
  display: flex
  align-items: center
  justify-content: space-between
  gap: $medium
  padding: $small $medium
  line-height: 1.4
  white-space: nowrap

// Inline flow, not flex: a flex item is blockified, so the strip copied as
// "Идет: ...\n\n| +2 запланировано". Inline parts copy as one line, and the
// visible gaps are the spaces of the literal " | " nodes rather than geometry.
// The ellipsis moves up here with the flow: the strip clips at its own right
// edge instead of the focal text clipping inside a flex track, which is what
// makes the title the LAST thing a narrow screen gives up.
.row-main
  display: block
  flex: 1 1 auto
  min-width: 0
  overflow: hidden
  text-overflow: ellipsis

// The focal composite: one selectable inline run (status + title + meta).
.row-text
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

// The quiet count of everything else.
.row-more
  color: $text-muted

.row-sep
  color: $text-muted

.rest-near
  color: $text-muted

// Items of the strip: the description and the count of the remaining events.
// Both are link-buttons, the site's one strip idiom — the chevron that used to
// sit here was the single non-link element of the line, which made the "|"
// next to it read as a separator for an icon. Calm at rest ($text-muted, so
// the line stays one colour), link-blue + underline on hover, $link/600 while
// the layer it opened is up: blue appears on intent, not scattered.
.strip-item
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

// On a phone the line gives up words before it gives up the title: first the
// date of the nearest event, then the noun of the count, which leaves "+2".
// Measured at 375px, where the running event's title had no width left at all.
@media (max-width: $bp-mobile)
  .rest-near
    display: none

  .rest-word
    display: none

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
