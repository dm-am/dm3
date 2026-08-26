<template>
  <SidebarBlock token="SiteAddresses" title="Серверы сайта">
    <!-- Every address the site answers on, the one being read included, with
         a live reading of whether each answers from THIS visitor's network
         and how fast (useAddressPing). The list itself stays static
         (shared/config/site): the moment a reader needs the other address is
         the moment a request for it cannot be served.

         Every row is a link, the address being read included: the rows are
         one list of the same kind of thing, and one of them silently not
         being a link reads as a defect rather than as a hint about where you
         are. Following your own address is a reload of the page you are on,
         which costs nothing. A plain <a>, not router-link: leaving for
         another host is navigation out of this application, and the current
         path rides along so the same page opens on the other side. Drawn
         from the static list rather than from a request — the addresses are
         worth naming precisely when nothing is answering.

         No words about state anywhere visible, and no mark of "the one you
         are on": the numbers say it. The words exist only where a pointer or
         a screen reader asks — the scale's title, and a visually-hidden
         status span closing the row. The scale and the visible ms cell are
         aria-hidden (the hidden span reads the same fact once); the link
         alone names the row. The dash of the ms cell is drawn from the first
         render in the same reserved track, so nothing jumps when the
         measurement lands.

         The decorative "- " of the neighbouring blocks is gone: these rows
         are cells of a board now, not lines of a list. The mark sits BESIDE
         the link, not inside it (owner's call, 2026-08-25): it is data the
         way the digits are, and after it lost the link's colour for that
         reason, keeping the click would have left a surface that neither
         looks nor is a link yet navigates. It stays aria-hidden and out of
         selection, so a row still copies as "<имя>" with at most the ms
         figure the visitor selected on purpose. -->
    <li class="site-addresses">
      <template v-for="address in addresses" :key="address.host">
        <span class="address-cell"
          ><span
            v-if="'icon' in address.mark"
            class="address-mark"
            aria-hidden="true"
            ><SvgIcon :name="address.mark.icon" /></span
          ><span v-else class="address-mark address-label" aria-hidden="true">{{
            address.mark.label
          }}</span
          ><a
            class="address-link"
            :href="`https://${address.host}${route.fullPath}`"
            >{{ address.name }}</a
          ></span
        >
        <span
          class="signal"
          :class="{ down: pingOf(address.host).status === 'down' }"
          :title="statusText(address.host) ?? undefined"
          aria-hidden="true"
        >
          <span
            v-for="bar in 4"
            :key="bar"
            class="bar"
            :class="{ filled: bar <= filledBars(address.host) }"
          ></span>
        </span>
        <span class="latency" aria-hidden="true">{{
          latencyText(address.host)
        }}</span>
        <span
          v-if="statusText(address.host) !== null"
          class="visually-hidden address-status"
          >{{ statusText(address.host) }}</span
        >
      </template>
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import { useRoute } from "vue-router";
import SidebarBlock from "./SidebarBlock.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { SITE_ADDRESSES } from "@/shared/config/site";
import {
  useAddressPing,
  type AddressPing,
} from "@/shared/lib/composables/useAddressPing";

// The path changes and the links have to follow it — hence no v-once on the
// rows, unlike the static ContactLinks next door.
const route = useRoute();
const addresses = SITE_ADDRESSES;
const pings = useAddressPing();

// Filled bars of the four-bar scale by round trip, cumulative like a Wi-Fi
// indicator: <=80 ms shows all four, <=160 three, <=300 two, anything slower
// one. An address that answers at all never shows an empty scale — empty is
// the not-yet-measured state, red the not-answering one.
const SIGNAL_STEPS_MS = [80, 160, 300];

// The hosts come from the same SITE_ADDRESSES the composable keyed its state
// by; the fallback exists for the type, not for a case.
function pingOf(host: string): AddressPing {
  return pings[host] ?? { status: "pending", latencyMs: null };
}

function filledBars(host: string): number {
  const ping = pingOf(host);
  if (ping.status !== "up" || ping.latencyMs === null) return 0;
  const latency = ping.latencyMs;
  return 4 - SIGNAL_STEPS_MS.filter((step) => latency > step).length;
}

function latencyText(host: string): string {
  const ping = pingOf(host);
  return ping.status === "up" && ping.latencyMs !== null
    ? `${ping.latencyMs} мс`
    : "-";
}

// One sentence serves both askers: the pointer hovering the scale (title)
// and the screen reader closing the row (the visually-hidden span). Null
// while the first measurement is still in flight — there is nothing honest
// to claim yet.
function statusText(host: string): string | null {
  const ping = pingOf(host);
  if (ping.status === "up" && ping.latencyMs !== null) {
    return `отвечает из вашей сети, ${ping.latencyMs} мс`;
  }
  return ping.status === "down" ? "не отвечает из вашей сети" : null;
}
</script>

<style scoped lang="sass">
// ONE grid for the block, not a grid per row: the mark, name, signal and ms
// columns must land on the same x in every row, and only shared tracks
// guarantee that. The 56px ms track is reserved from the first render so the
// dash and the number occupy the same place; the trailing 1fr is the air on
// the right. Every in-flow cell names its column, or auto-placement would
// fill that air with the next row's mark.
.site-addresses
  display: grid
  grid-template-columns: 24px max-content max-content max-content 1fr
  column-gap: 14px
  // $minor, half a step above the plain link blocks: these rows carry a
  // chart and digits, and at $tiny they read as one clump (variant D).
  row-gap: $minor
  // center, not baseline: a row mixes glyph mark, text, bar chart and digits,
  // and centering is the only rhythm all four share. The board looked ragged
  // on the owner's screen with everything standing on the text baseline.
  align-items: center
  // Step B2 of the owner's iteration protocol (W3.9): bare rows, no card.
  // B1's box was rejected for the sidebar - its frame carried more weight
  // than a status block deserves next to the plain link modules.

// The link spans the mark and name tracks as ONE grid item: the mark belongs
// to the link (its colour, its click target), a display: contents anchor
// would lose its focus outline, and inline flow inside keeps the row copying
// as "<имя>" in one line, never "mark \n name".
.address-cell
  grid-column: 1 / 3

// The mark's slot is the width of its track, and its margin repeats the
// grid's own column gap, so the name starts exactly where the name track
// does and the two names share their x. line-height 1, or the 1.2em label
// inflates the row above the line rhythm.
.address-mark
  display: inline-block
  width: 24px
  // 12px by the owner-s eye (2026-08-25, third pass): 4px glued, 8px still
  // tight, 14px pushed away.
  margin-right: ($small + $minor)
  text-align: center
  line-height: 1
  // Muted like the ms figures, not the link's colour (owner, 2026-08-25):
  // the mark identifies the row the way the digits do - painting it as a
  // link promised a second destination that does not exist. It stays inside
  // the anchor, so the click target is unchanged.
  color: $text-muted

// The globe keeps the ink size the owner signed off in the plain block
// (0.8984em at the 1.2em slot = 17px): 14px turned its meridians to mush.
// What changed against that block is only the vertical seat - centered with
// the row instead of dipping under the baseline like a font glyph, because a
// board row mixes marks, bars and digits and centering is their one shared
// rhythm.
.address-mark svg
  width: 17px
  height: 17px
  // The seat the owner signed off in the plain block: ink bottom a hair under
  // the baseline, the way the source font drew the glyph next to text.
  vertical-align: -0.0703em

// The letters stand in for an icon. 1em, and the number is measured, not
// guessed: PT Sans caps at 1em ink 11px — exactly the ink the old flag
// emoji's RU letters had at the block's 1.2em, the look the owner signed
// off on. 0.75em (8px) read too small next to the 17px globe, 1.2em (13px)
// too big. No vertical-align: the letters sit on the row's baseline, which
// is what aligns them with the name beside them.
//
// user-select: none — the second member of the unselectable set after
// DashSeparator (RULE-22), by the owner's call 2026-08-18: the icon mark
// cannot take part in selection, and letters that did made the two rows
// select and copy differently.
// Letters sit with letters: the label shares the name's baseline. middle
// floated RU off the line the name stands on (owner's screenshot, 2026-08-25).
.address-label
  font-size: 1em
  user-select: none

// The scale is CSS boxes, not an icon: four bars stepping up to 13px. A flex
// container with no text synthesizes its baseline from the box bottom, so
// the bars stand on the text baseline like letters. Mute for both the
// clipboard and the reader — the status span carries the same fact in words.
.signal
  grid-column: 3
  display: flex
  align-items: flex-end
  gap: 2px
  height: 14px
  user-select: none

.bar
  width: 4px
  // One green instrument (owner's pick, variant D, 2026-08-25): unfilled
  // steps are the same accent at a quarter strength - "same device, off" -
  // instead of the grey ladder that read as dirt between name and digits.
  background-color: $accent-green
  opacity: 0.22

  &.filled
    opacity: 1

// Height steps of the four bars: 5, 8, 11, 14px.
.bar:nth-child(1)
  height: 5px

.bar:nth-child(2)
  height: 8px

.bar:nth-child(3)
  height: 11px

.bar:nth-child(4)
  height: 14px

// Dead, not merely slow: the whole comb goes red, dimmed so the row does not
// shout — the other address is drawn precisely for the visitor whose usual
// one stopped answering.
.signal.down .bar
  background-color: $accent-red
  opacity: $muted-opacity

.latency
  grid-column: 4
  text-align: center
  font-variant-numeric: tabular-nums
  color: $text-muted
  // Real pings run four digits; a wrapped "мс" doubled the row height.
  white-space: nowrap

// The words a screen reader gets and the clipboard does not: clipped by the
// global .visually-hidden, kept out of selection here, so a copied row picks
// up no state sentence.
.address-status
  user-select: none
</style>
