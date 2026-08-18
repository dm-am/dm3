<template>
  <SidebarBlock token="SiteAddresses" title="Серверы сайта">
    <!-- Every address the site answers on, the one being read included. It
         used to sit in the footer's legal column as a single link "from here
         to there"; here it is a reference, and a reference showing half of
         itself leaves the reader guessing what the other address is and which
         one he is on.

         Every row is a link, the address being read included: the rows are one
         list of the same kind of thing, and one of them silently not being a
         link reads as a defect rather than as a hint about where you are.
         Following your own address is a reload of the page you are on, which
         costs nothing.

         The row keeps the decorative "- " of every neighbouring block
         (aria-hidden, out of the accessibility tree). The mark lives INSIDE
         the link: it names the address just as the words do, so it takes the
         link's colour and its click target. Both marks sit in the same fixed
         slot, so the two names start at the same x. Block/inline flow (not
         flex) keeps each row copying as one line instead of "- \n<link>".

         Both marks are decorative twins of the name beside them, so both are
         aria-hidden and neither takes part in selection: an svg never does,
         and letters that did made the two rows select and copy differently.
         Every row therefore copies as the same shape, "- <имя>" (the
         mark-to-name gap is the slot's margin, not a text node).

         A plain <a>, not router-link: leaving for another host is navigation
         out of this application. The current path rides along so the same page
         opens on the other side. Drawn from the host in the address bar rather
         than from a request — the addresses are worth naming precisely when
         nothing is answering. -->
    <li class="site-addresses">
      <div
        v-for="address in addresses"
        :key="address.host"
        class="address-item"
      >
        <span class="muted" aria-hidden="true">-&nbsp;</span
        ><a :href="`https://${address.host}${route.fullPath}`"
          ><span
            v-if="'icon' in address.mark"
            class="address-mark"
            aria-hidden="true"
            ><SvgIcon :name="address.mark.icon" /></span
          ><span v-else class="address-mark address-label" aria-hidden="true">{{
            address.mark.label
          }}</span
          >{{ address.name }}</a
        >
      </div>
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import { useRoute } from "vue-router";
import SidebarBlock from "./SidebarBlock.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { SITE_ADDRESSES } from "@/shared/config/site";

// The path changes and the links have to follow it — hence no v-once on the
// rows, unlike the static ContactLinks next door.
const route = useRoute();
const addresses = SITE_ADDRESSES;
</script>

<style scoped lang="sass">
.site-addresses
  display: flex
  flex-direction: column
  gap: $tiny

// Plain block line (mark span + link are inline on a shared baseline).
// Deliberately NOT flex: element flex items copy with a newline between them.
.address-item
  display: block

.muted
  color: $text-muted

// Both marks occupy the same fixed slot so the two names start at the same x;
// the mark-to-name gap is the slot's margin, not a text node (see template).
// line-height 1, or the 1.2em label inflates the row above the line rhythm.
.address-mark
  display: inline-block
  width: 24px
  margin-right: $minor
  text-align: center
  line-height: 1

// The globe is the Segoe glyph's ink extracted into a tight 24-box, so the
// path has none of the font's built-in bearings. These numbers put the ink
// back exactly where the font drew it: measured off a canvas render of the
// glyph at the block's own 1.2em size — ink height 0.8984em, ink bottom
// 0.0703em below the baseline (diag numbers, 2026-08-17). The font-size on
// the svg keeps both em values in the same 1.2em currency the emoji used.
.address-mark svg
  font-size: 1.2em
  width: 0.8984em
  height: 0.8984em
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
.address-label
  font-size: 1em
  user-select: none
</style>
