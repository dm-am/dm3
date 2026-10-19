<template>
  <SidebarBlock token="SiteAddresses" title="Серверы сайта">
    <!-- Every address, from the static list, with how it answers from this network -->
    <li class="site-addresses">
      <!-- A CSS table, so a row copies as one line; spans, so a reader hears links -->
      <div
        v-for="{ address, view } in rows"
        :key="address.host"
        class="address-row"
      >
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
            v-if="view.status !== null"
            class="visually-hidden address-status"
            >{{ view.status }}</span
          ></span
        >
        <span class="signal-cell">
          <Tooltip :text="view.title ?? undefined" focusable>
            <span
              class="signal"
              :class="{ down: view.down }"
              aria-hidden="true"
            >
              <span
                v-for="bar in 4"
                :key="bar"
                class="bar"
                :class="{ filled: bar <= view.bars }"
              ></span>
            </span>
          </Tooltip>
        </span>
        <span class="latency" aria-hidden="true">{{ view.latency }}</span>
      </div>
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import SidebarBlock from "./SidebarBlock.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import Tooltip from "@/shared/ui/Tooltip/Tooltip.vue";
import { SITE_ADDRESSES } from "@/shared/config/site";
import {
  useAddressPing,
  type AddressPing,
} from "@/shared/lib/composables/useAddressPing";

// No v-once: the links follow the path.
const route = useRoute();
const pings = useAddressPing();

// Bars by round trip: <=80 ms four, <=160 three, <=300 two, slower one; empty is not measured yet.
const SIGNAL_STEPS_MS = [80, 160, 300];

interface AddressView {
  down: boolean;
  bars: number;
  /** The figure, "..." while measuring, "нет" for an address that does not answer. */
  latency: string;
  /** What the bars measure; null while measuring. */
  title: string | null;
  /** The whole state, for a screen reader; null while measuring. */
  status: string | null;
}

function viewOf(ping: AddressPing): AddressView {
  if (ping.status === "up" && ping.latencyMs !== null) {
    const latency = ping.latencyMs;
    // Whole tens: a figure that changes on every look reads as an event.
    const shown = `${Math.round(latency / 10) * 10} мс`;
    return {
      down: false,
      bars: 4 - SIGNAL_STEPS_MS.filter((step) => latency > step).length,
      latency: shown,
      title: "Скорость ответа из вашей сети",
      status: `Отвечает из вашей сети, ${shown}`,
    };
  }
  const down = ping.status === "down";
  const silence = down ? "Не отвечает из вашей сети" : null;
  return {
    down,
    bars: 0,
    latency: down ? "нет" : "...",
    title: silence,
    status: silence,
  };
}

// The fallback is for the type: the composable keys its state by the same hosts.
const rows = computed(() =>
  SITE_ADDRESSES.map((address) => ({
    address,
    view: viewOf(pings[address.host] ?? { status: "pending", latencyMs: null }),
  })),
);
</script>

<style scoped lang="sass">
// One CSS table, so the columns line up; the gaps are cell padding, not border-spacing.
.site-addresses
  display: table
  border-collapse: collapse

.address-row
  display: table-row

.address-row > *
  display: table-cell
  // middle: the one rhythm a mark, text, bars and digits share.
  vertical-align: middle

.address-row > * + *
  padding-left: $medium

.address-row + .address-row > *
  padding-top: $small

// Inline flow, so the cell copies as the name.
.address-cell
  white-space: nowrap

// A slot of fixed width, so both names start at one x.
.address-mark
  display: inline-block
  width: 24px
  margin-right: ($small + $minor)
  text-align: center
  line-height: 1
  // Muted like the figures: the mark identifies the row, it is not a second link.
  color: $text-muted

// 17px: smaller turned the globe's meridians to mush.
.address-mark svg
  width: 17px
  height: 17px
  vertical-align: -0.0703em

// Letters standing in for an icon: unselectable, like the icon, so both rows copy alike.
.address-label
  font-size: 1em
  user-select: none

// No line strut in the scale's cell, so the comb's centre is the row's.
.signal-cell
  line-height: 0

// :deep, the trigger is the child's; inline-flex, as a block would copy as a line break.
.signal-cell :deep(.tooltip-trigger)
  display: inline-flex
  vertical-align: top

.signal
  display: flex
  align-items: flex-end
  gap: $tiny
  height: 14px
  user-select: none

.bar
  width: 4px
  // Unfilled steps are the same green, faint.
  background-color: $accent-green
  opacity: $opacity-faint

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

// Not answering: the whole comb red, dimmed.
.signal.down .bar
  background-color: $accent-red
  opacity: $muted-opacity

.latency
  width: 56px
  text-align: center
  font-variant-numeric: tabular-nums
  color: $text-muted
  white-space: nowrap

// For the screen reader, not for a copy.
.address-status
  user-select: none
</style>
