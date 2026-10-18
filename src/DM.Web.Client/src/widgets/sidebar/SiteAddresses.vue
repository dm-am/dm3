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
         a screen reader asks — the site's Tooltip on the scale (the browser's
         own title bubble is not the site's, and it never showed for a touch
         or a keyboard), and a visually-hidden status span closing the row. The scale and the visible ms cell are
         aria-hidden (the hidden span reads the same fact once); the link
         alone names the row. The ms cell is drawn from the first render in
         the same reserved track - "..." while the measurement is out, "нет"
         when the address does not answer - so nothing jumps when the number
         lands.

         The decorative "- " of the neighbouring blocks is gone: these rows
         are cells of a board now, not lines of a list. The mark sits BESIDE
         the link, not inside it (owner's call, 2026-08-25): it is data the
         way the digits are, and after it lost the link's colour for that
         reason, keeping the click would have left a surface that neither
         looks nor is a link yet navigates. It stays aria-hidden and out of
         selection, so a row still copies as "<имя>" with at most the ms
         figure the visitor selected on purpose. -->
    <li class="site-addresses">
      <!-- A CSS table (display: table / table-cell), not a grid and not table
           markup: the board is rows of one kind of thing with columns that must
           line up, and a table is what copies as such - one line per address,
           cells apart - where grid items copy as a line each and a converter to
           markdown glues a whole grid into one bullet. The same idiom the staff
           table and the expandable lists use, for the same reason. Spans, so a
           reader hears each link and the status sentence after it, not a table. -->
      <div v-for="address in addresses" :key="address.host" class="address-row">
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
          ><span
            v-if="statusText(address.host) !== null"
            class="visually-hidden address-status"
            >{{ statusText(address.host) }}</span
          ></span
        >
        <span class="signal-cell">
          <Tooltip :text="scaleTitle(address.host) ?? undefined" focusable>
            <span
              class="signal"
              :class="{ down: pingOf(address.host).status === 'down' }"
              aria-hidden="true"
            >
              <span
                v-for="bar in 4"
                :key="bar"
                class="bar"
                :class="{ filled: bar <= filledBars(address.host) }"
              ></span>
            </span>
          </Tooltip>
        </span>
        <span class="latency" aria-hidden="true">{{
          latencyText(address.host)
        }}</span>
      </div>
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import { useRoute } from "vue-router";
import SidebarBlock from "./SidebarBlock.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import Tooltip from "@/shared/ui/Tooltip/Tooltip.vue";
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

// Whole tens, not units: consecutive measurements of one network differ by
// a few milliseconds, and a figure that changes on every look reads as an
// event when nothing happened. Tens keep the one job the figure has — telling
// the two addresses apart when their scales agree.
function shownMs(latencyMs: number): number {
  return Math.round(latencyMs / 10) * 10;
}

// Three answers to "how many milliseconds": the figure, "..." while the
// first round trip is still out (the cell is not empty and not a verdict -
// the measurement is in progress), and "нет" for an address that does not
// answer (owner's word, 2026-09-07). A dash used to stand in for both, and a
// dash reads as "no data" for the one and as nothing at all for the other.
function latencyText(host: string): string {
  const ping = pingOf(host);
  if (ping.status === "up" && ping.latencyMs !== null) {
    return `${shownMs(ping.latencyMs)} мс`;
  }
  return ping.status === "down" ? "нет" : "...";
}

// Two sentences for two askers, because they ask different things. The
// pointer, finger or focus on the scale asks what the bars are: a speed, and
// measured from THIS network - the one fact the row cannot say in numbers,
// and the one a reader would otherwise mistake for server health. The figure
// is not repeated: it stands right beside the scale. The screen reader,
// closing the row, asks for the whole state in words, figure included - the
// ms cell is hidden from it. Both are null while the first measurement is
// still in flight: there is nothing honest to claim yet, and the tooltip
// stays away for an empty text.
function scaleTitle(host: string): string | null {
  const ping = pingOf(host);
  if (ping.status === "up" && ping.latencyMs !== null) {
    return "Скорость ответа из вашей сети";
  }
  return ping.status === "down" ? "Не отвечает из вашей сети" : null;
}

function statusText(host: string): string | null {
  const ping = pingOf(host);
  if (ping.status === "up" && ping.latencyMs !== null) {
    return `Отвечает из вашей сети, ${shownMs(ping.latencyMs)} мс`;
  }
  return ping.status === "down" ? "Не отвечает из вашей сети" : null;
}
</script>

<style scoped lang="sass">
// One CSS table for the block: the mark-and-name, signal and ms columns land
// on the same x in every row because a table's columns are shared by nature.
// No border-spacing - it would pad the table's own edge too - the gaps are
// cell padding: 14px between columns, $small between rows (a full step above
// the plain link blocks, the owner's "чуть воздуха": these rows carry a chart
// and digits, and at $minor they read as one clump). The ms column reserves
// its width so the "...", the "нет" and the number occupy the same place.
// Step B2 of the owner's iteration protocol (W3.9): bare rows, no card.
.site-addresses
  display: table
  border-collapse: collapse

.address-row
  display: table-row

.address-row > *
  display: table-cell
  // middle, not baseline: a row mixes glyph mark, text, bar chart and digits,
  // and centering is the only rhythm all four share. The board looked ragged
  // on the owner's screen with everything standing on the text baseline.
  vertical-align: middle

.address-row > * + *
  padding-left: 14px

.address-row + .address-row > *
  padding-top: $small

// The mark stays inside the link's cell, before the name: it belongs to the
// link (its click target), and inline flow keeps the cell copying as "<имя>".
.address-cell
  white-space: nowrap

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
// The scale's cell has no text, so no line strut: its content is exactly the
// comb, and the cell's middle then puts the comb's centre on the row's centre.
.signal-cell
  line-height: 0

// The tooltip's trigger holds the scale inside the cell. It belongs to the
// child component and its root is a fragment, so the parent's scope never
// reaches it, hence :deep. Inline-flex, not flex: a block-level box inside a
// cell copies as a line break, and the ms figure then lands on its own line
// under the name (name, tab, line break, figure); an inline-level box copies as
// nothing, and the row stays one line.
.signal-cell :deep(.tooltip-trigger)
  display: inline-flex
  vertical-align: top

.signal
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
  width: 56px
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
