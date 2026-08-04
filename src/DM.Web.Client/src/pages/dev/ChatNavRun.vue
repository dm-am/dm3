<script setup lang="ts">
/**
 * ChatNavRun — DEV-ONLY: the chat's own navigation run, "Поиск | К дате".
 *
 * It belongs to no event variant. It shares the strip with all of them, and
 * that is the point of showing it in every frame: whatever a variant does with
 * the event, this run still has to fit next to it. Today it is the thing the
 * count of upcoming events lands on top of at 375px.
 *
 * Below 480px of frame the labels drop to their icons. That is the narrow
 * behaviour every variant here is drawn with, except the baseline, which draws
 * its own aside exactly as the site draws it now: no narrow rules at all.
 */
import { SvgIcon } from "@/shared/ui/Icon";
</script>

<template>
  <span
    ><button type="button" class="nav-item" title="Поиск (Ctrl+F)">
      <SvgIcon name="search" class="nav-icon" /><span class="nav-label"
        >Поиск</span
      ></button
    ><span class="nav-sep" aria-hidden="true">{{ " | " }}</span
    ><button type="button" class="nav-item">
      <SvgIcon name="calendar" class="nav-icon" /><span class="nav-label"
        >К дате</span
      >
    </button></span
  >
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.nav-item
  +inline-link-button
  &
    font-size: $secondary-font-size
    white-space: nowrap
    color: $text-muted
  &:hover:not(:disabled)
    color: $link
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px

.nav-sep
  color: $text-muted

.nav-icon
  width: 14px
  height: 14px
  vertical-align: -2px

// Margin on the label, not on the icon: when the label goes, the gap goes
// with it and the icon stays centred.
.nav-label
  margin-left: $minor

@container (max-width: 480px)
  .nav-label
    display: none
</style>
