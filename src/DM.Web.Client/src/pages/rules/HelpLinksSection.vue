<script setup lang="ts">
/**
 * HelpLinksSection - Quick help links for the rules page
 *
 * Displays common problems and where to get help.
 * Uses table layout consistent with the rest of the site.
 */

import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import HelpIcon from "@/shared/ui/Icon/HelpIcon.vue";
import { HELP_LINKS } from "@/shared/config/helpLinks";
</script>

<template>
  <section class="help-section">
    <BlockTitle>Куда обращаться</BlockTitle>

    <div class="help-table">
      <div v-for="link in HELP_LINKS" :key="link.key" class="help-row">
        <span class="help-icon-cell">
          <HelpIcon :type="link.icon" />
        </span>
        <span class="help-problem">{{ link.problem }}</span>
        <span class="help-arrow">→</span>
        <a
          v-if="link.url"
          :href="link.url"
          :target="link.external ? '_blank' : undefined"
          :rel="link.external ? 'noopener' : undefined"
          class="help-solution"
        >
          {{ link.solution }}
        </a>
        <span v-else class="help-solution-text">{{ link.solution }}</span>
      </div>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Tables"

.help-section
  margin: $big 0

.help-table
  +table

.help-row
  display: grid
  grid-template-columns: 28px auto max-content 1fr
  align-items: center
  gap: $small
  padding: $small $small
  +table-row

.help-icon-cell
  display: flex
  align-items: center
  justify-content: center
  color: $text-muted

.help-problem
  color: $text

.help-arrow
  color: $text-muted

.help-solution
  color: $link
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

.help-solution-text
  color: $text

@media (max-width: $mobile-breakpoint)
  .help-row
    grid-template-columns: 24px 1fr
    grid-template-rows: auto auto

  .help-icon-cell
    grid-row: 1 / 3

  .help-problem
    grid-column: 2

  .help-arrow
    display: none

  .help-solution,
  .help-solution-text
    grid-column: 2
    font-size: $secondary-font-size
</style>
