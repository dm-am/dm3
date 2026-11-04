<script setup lang="ts">
import { useAuthStore } from "@/entities/user";
import { storeToRefs } from "pinia";
import Greeting from "./Greeting.vue";
import GuestActions from "./GuestActions.vue";
import MainMenu from "./MainMenu.vue";
import SiteStatistics from "./SiteStatistics.vue";

const userStore = useAuthStore();
const { user } = storeToRefs(userStore);
</script>

<template>
  <header class="header">
    <div class="user-info">
      <router-link
        class="logo"
        :to="{ name: 'home' }"
        aria-label="Dungeon Master, на главную"
      />
      <div class="user-actions">
        <!-- One box for the greeting: as flex items its words would lose their spaces -->
        <Greeting v-if="user?.username" />
        <GuestActions v-else />
      </div>
    </div>

    <MainMenu class="top-menu" />

    <div class="stats-col">
      <SiteStatistics />
    </div>
  </header>
</template>

<style scoped lang="sass">
// Sets the three columns on one baseline above the strip's torn edge.
$baseline-pad: 9px

// The logo's box, and the width of its drawing by the viewBox of logo.svg.
$logo-width: 275px
$logo-offset: -5px
$logo-drawn-width: calc($header-row-height * 4079.6 / 827.08)

$header-strip: url('@/assets/images/decorations/header-decoration.png')

.header
  // Where the greeting stands: the logo's start in its column and its drawing's width.
  --logo-start: #{$big + $logo-offset}
  --logo-drawn: #{$logo-drawn-width}
  position: relative
  display: grid
  grid-template-columns: $sidebar-width minmax(0, 1fr) $sidebar-width
  grid-template-areas: "user menu stats"
  box-sizing: border-box
  // One row high, and higher only when the greeting wraps.
  min-height: $header-height

  // The strip twice at one row's height: the torn edge at the bottom, the flat paper at the top.
  &::before
    content: ''
    position: absolute
    inset: 0
    background-image: $header-strip, $header-strip
    background-position: left bottom, left top
    background-size: auto $header-height
    background-repeat: repeat-x
    filter: $filter-invert

// Over the paper strip: positioned, and after it in the order of the page.
.user-info,
.top-menu,
.stats-col
  position: relative

.user-info
  grid-area: user
  min-width: 0
  padding-bottom: $baseline-pad
  box-sizing: border-box
  white-space: nowrap
  cursor: default
  display: flex
  flex-direction: column
  justify-content: center

.logo
  display: block
  flex: none
  margin-bottom: $tiny
  margin-left: var(--logo-start)
  height: $header-row-height
  width: $logo-width
  background: transparent url('@/assets/images/logos/logo.svg') no-repeat
  background-size: contain

// Centred on the logo's drawing by a spacer after it; a longer line presses the spacer to nothing.
.user-actions
  display: flex
  justify-content: center
  margin-left: $medium
  min-width: 0
  font-size: $secondary-font-size
  color: $text

  > *
    flex: none

  &::after
    content: ''
    flex: 0 1 max(0px, calc(100% + 2 * #{$medium} - 2 * var(--logo-start) - var(--logo-drawn)))

// From the left, right after the logo as on DM2; one line or two, in the middle of the header.
.top-menu
  grid-area: menu
  min-width: 0
  align-content: center
  padding: 0 $big $baseline-pad
  box-sizing: border-box

.stats-col
  grid-area: stats
  min-width: 0
  display: flex
  align-items: center
  justify-content: flex-start
  padding-bottom: $baseline-pad
  padding-right: $big
  box-sizing: border-box
</style>
