<script setup lang="ts">
import { computed } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import HelpIcon from "./HelpIcon.vue";
import { HELP_LINKS, type HelpLink } from "@/shared/config/helpLinks";

/** [before, linkText, after] of a solution linked in part, or null. */
function splitSolution(link: HelpLink): [string, string, string] | null {
  if (!link.linkText || !link.url) return null;
  const idx = link.solution.indexOf(link.linkText);
  if (idx === -1) return null;
  const before = link.solution.slice(0, idx);
  const after = link.solution.slice(idx + link.linkText.length);
  return [before, link.linkText, after];
}

// Split once per link, not once per use in the template
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
        <span class="help-arrow-col" aria-hidden="true">{{
          symbols.arrowRight
        }}</span>
        <span class="help-solution-col">
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
              rel="noopener noreferrer"
              class="help-link"
              >{{ split[1] }}</a
            >{{ split[2] }}
          </template>
          <template v-else-if="link.url && link.external">
            <a
              :href="link.url"
              target="_blank"
              rel="noopener noreferrer"
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
          <template v-else>
            {{ link.solution }}
          </template>
        </span>
      </div>
    </div>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Tables" as *

.help-section
  margin: $big 0

.help-table
  +table

.help-row
  display: grid
  grid-template-columns: $big auto max-content 1fr
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
  // The color comes from the global `a` rule; only the bold of links inside sentences is local.
  .help-link
    font-weight: 700
</style>
