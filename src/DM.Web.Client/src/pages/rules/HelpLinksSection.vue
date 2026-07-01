<script setup lang="ts">
/**
 * HelpLinksSection - Quick help links for the rules page
 *
 * Displays common problems and where to get help.
 * Uses unified table styling from _Tables.sass.
 */

import { computed } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import HelpIcon from "@/shared/ui/Icon/HelpIcon.vue";
import { HELP_LINKS, type HelpLink } from "@/shared/config/helpLinks";

/**
 * Splits solution text around linkText for partial linking.
 * Returns [before, linkText, after] parts.
 */
function splitSolution(link: HelpLink): [string, string, string] | null {
  if (!link.linkText || !link.url) return null;
  const idx = link.solution.indexOf(link.linkText);
  if (idx === -1) return null;
  const before = link.solution.slice(0, idx);
  const after = link.solution.slice(idx + link.linkText.length);
  return [before, link.linkText, after];
}

// Pre-compute split solutions to avoid calling splitSolution 4x per link in template
const linksWithSplit = computed(() =>
  HELP_LINKS.map((link) => ({
    link,
    split: splitSolution(link),
  })),
);
</script>

<template>
  <section class="help-section">
    <BlockTitle>Куда обращаться</BlockTitle>

    <div class="help-table">
      <div
        v-for="{ link, split } in linksWithSplit"
        :key="link.key"
        class="help-row"
      >
        <span class="help-icon-col">
          <HelpIcon :type="link.icon" />
        </span>
        <span class="help-problem-col">{{ link.problem }}</span>
        <span class="help-arrow-col">{{ symbols.arrowRight }}</span>
        <span class="help-solution-col">
          <!-- Partial link: only linkText is linked -->
          <template v-if="split">
            {{ split[0]
            }}<router-link
              v-if="!link.external"
              :to="link.url!"
              class="help-link"
              >{{ split[1] }}</router-link
            ><a
              v-else
              :href="link.url"
              target="_blank"
              rel="noopener"
              class="help-link"
              >{{ split[1] }}</a
            >{{ split[2] }}
          </template>
          <!-- Full link: entire solution is linked -->
          <template v-else-if="link.url && link.external">
            <a
              :href="link.url"
              target="_blank"
              rel="noopener"
              class="help-link"
            >
              {{ link.solution }}
            </a>
          </template>
          <template v-else-if="link.url">
            <router-link :to="link.url" class="help-link">
              {{ link.solution }}
            </router-link>
          </template>
          <!-- No link: plain text -->
          <template v-else>
            {{ link.solution }}
          </template>
        </span>
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
  grid-template-columns: 32px auto max-content 1fr
  align-items: center
  gap: $small
  +table-row

.help-icon-col
  display: flex
  align-items: center
  justify-content: center
  color: $text-muted

.help-problem-col
  color: $text

.help-arrow-col
  color: $text-muted

.help-solution-col
  .help-link
    color: $link
    font-weight: 600
    text-decoration: none

    &:hover
      color: $link-hover
      text-decoration: underline

@media (max-width: $mobile-breakpoint)
  .help-row
    grid-template-columns: 24px 1fr
    grid-template-rows: auto auto

  .help-icon-col
    grid-row: 1 / 3

  .help-problem-col
    grid-column: 2

  .help-arrow-col
    display: none

  .help-solution-col
    grid-column: 2
    font-size: $secondary-font-size
</style>
