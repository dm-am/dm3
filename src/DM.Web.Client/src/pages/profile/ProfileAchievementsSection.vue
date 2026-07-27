<script setup lang="ts">
/**
 * ProfileAchievementsSection — the "Достижения" block.
 *
 * Architecture:
 *  - The tier catalog (`AchievementType`) already carries its category
 *    (`AchievementType.category` — SSOT for iconName/metric/description/
 *    sortOrder). Grouping tiers by `category.id` yields a chain.
 *  - Progress is not stored: it is computed from user metrics via
 *    `getMetricValue` (SSOT with backend `AchievementMetricResolver`).
 *  - A tile shows the SENIOR EARNED tier of the chain: its title and the
 *    tier color on the icon. Chains without a single earned tier are
 *    rendered too, in a locked style: the whole tile is ghosted with
 *    opacity (near-transparent icon, faded title and progress digits),
 *    no tier tint, no roman badge, and the TIER I TITLE (the first level
 *    to reach) as the tile caption — the ghost opacity keeps it from
 *    reading as earned.
 *  - Under the title: a progress bar + "X / Y" numbers toward the next
 *    unearned tier. A completed chain shows a full bar and the total
 *    metric value.
 *  - Rich popover (via `<Tooltip #content>`): category title with a
 *    "Завершено" / "X / Y" badge, the metric description, a progress
 *    line toward the next tier and the full table of tiers with their
 *    thresholds and earned/locked state.
 *  - The parent (ProfileAchievements) listens to the `state` emit and
 *    coordinates the tab-level empty state with the awards block.
 */
import { computed, onMounted, ref, watch } from "vue";
import { achievementApi } from "@/shared/api";
import type {
  AchievementType,
  UserAchievement,
} from "@/shared/api/models/achievements";
import type { User } from "@/shared/api/models/community";
import { GameIcon } from "@/shared/ui/Icon";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Tooltip } from "@/shared/ui/Tooltip";
import { getMetricValue } from "@/shared/lib/achievements/getMetricValue";
import {
  formatThreshold,
  metricDisplayNumber,
} from "@/shared/lib/achievements/formatThreshold";

const props = defineProps<{
  username: string;
  /** Profile owner — provides the metric values for progress. */
  user: User;
}>();

/** Load/content status for the tab-level empty-state coordination in
 * ProfileAchievements: the shared "Пока нет наград и достижений" text
 * shows only when BOTH sections settle empty. Since every catalog chain
 * is rendered (locked chains included), "empty" here means the catalog
 * itself is empty — with a seeded catalog the section always settles
 * as "content". */
const emit = defineEmits<{
  state: [value: "loading" | "error" | "empty" | "content"];
}>();

const earned = ref<UserAchievement[]>([]);
const catalog = ref<AchievementType[]>([]);
const loading = ref(false);
const loaded = ref(false);
const error = ref(false);

async function load(username: string) {
  loading.value = true;
  loaded.value = false;
  error.value = false;
  emit("state", "loading");
  const [earnedRes, catalogRes] = await Promise.all([
    achievementApi.getUserAchievements(username),
    achievementApi.getAchievementTypes(),
  ]);
  if (earnedRes.error || catalogRes.error) {
    error.value = true;
  } else {
    earned.value = earnedRes.data?.resources ?? [];
    catalog.value = catalogRes.data?.resources ?? [];
  }
  loading.value = false;
  loaded.value = true;
  emit(
    "state",
    error.value ? "error" : hasAnyChain.value ? "content" : "empty",
  );
}

onMounted(() => load(props.username));
watch(
  () => props.username,
  (next) => load(next),
);

const earnedByTypeId = computed(() => {
  const map = new Map<string, UserAchievement>();
  for (const a of earned.value) map.set(a.type.id, a);
  return map;
});

interface ChainTier {
  type: AchievementType;
  earnedUtc: string | null;
  progress: number;
}

interface Chain {
  categoryId: string;
  sortOrder: number;
  iconName: string;
  /** Category title — the popover header. */
  title: string;
  /** Category metric description (what exactly is counted). */
  description: string;
  /** Every tier of the chain, threshold-sorted — the popover table. */
  tiers: ChainTier[];
  earnedCount: number;
  /** Senior earned tier — the face of the tile (title + tier color);
   * null — the chain is fully locked (nothing earned yet). */
  earned: ChainTier | null;
  /** First unearned tier (the progress goal); null — all earned. */
  next: ChainTier | null;
  /** True when every tier of the chain is earned. */
  completed: boolean;
  /** True when no tier is earned — the tile renders in locked style. */
  locked: boolean;
  /** Number of the senior earned tier (1..4, 0 — locked chain) —
   * drives the tier color. */
  maxEarnedTier: number;
}

const chains = computed<Chain[]>(() => {
  const grouped = new Map<string, AchievementType[]>();
  for (const t of catalog.value) {
    const list = grouped.get(t.category.id) ?? [];
    list.push(t);
    grouped.set(t.category.id, list);
  }

  const result: Chain[] = [];
  for (const types of grouped.values()) {
    types.sort((a, b) => a.threshold - b.threshold);

    const category = types[0].category;
    const tiers: ChainTier[] = types.map((type) => {
      const got = earnedByTypeId.value.get(type.id);
      const progress = getMetricValue(category.metric, props.user);
      return { type, earnedUtc: got?.earnedUtc ?? null, progress };
    });

    // Every catalog chain is rendered — chains without a single earned
    // tier show up locked (muted icon, tier I title, progress to I).
    const earnedTiers = tiers.filter((t) => t.earnedUtc);

    // Tiers are threshold-sorted, so the last earned one is the senior.
    const earnedTop =
      earnedTiers.length > 0 ? earnedTiers[earnedTiers.length - 1] : null;

    result.push({
      categoryId: category.id,
      sortOrder: category.sortOrder,
      iconName: category.iconName,
      title: category.title,
      description: category.description,
      tiers,
      earnedCount: earnedTiers.length,
      earned: earnedTop,
      next: tiers.find((t) => !t.earnedUtc) ?? null,
      completed: earnedTiers.length === tiers.length,
      locked: earnedTiers.length === 0,
      maxEarnedTier: earnedTop?.type.tier ?? 0,
    });
  }
  result.sort((a, b) => a.sortOrder - b.sortOrder);
  return result;
});

const hasAnyChain = computed(() => chains.value.length > 0);

// Tile caption: the senior earned tier's thematic title; for a fully
// locked chain — the TIER I title (the first level to reach). Tiers are
// threshold-sorted, so tiers[0] is tier I. The ghost opacity of the
// locked style keeps it from reading as earned.
function tileTitle(chain: Chain): string {
  return chain.earned ? chain.earned.type.title : chain.tiers[0].type.title;
}

function tierClass(tier: number | null): string {
  if (tier === 1) return "tier-bronze";
  if (tier === 2) return "tier-silver";
  if (tier === 3) return "tier-gold";
  if (tier === 4) return "tier-platinum";
  return "tier-base";
}

const ROMAN = ["", "I", "II", "III", "IV"];
function roman(n: number): string {
  return ROMAN[n] ?? "";
}

// Display-unit current / goal numbers toward the next tier (days→years for
// registration, raw count otherwise). One SSOT keeps the tile, the popover
// and the aria-label in the same unit — never "4054 из 15 лет".
function displayCurrent(chain: Chain): number {
  const next = chain.next;
  if (!next) return 0;
  return metricDisplayNumber(
    next.type.category.metric,
    Math.min(Math.max(next.progress, 0), next.type.threshold),
  );
}
function displayGoal(chain: Chain): number {
  const next = chain.next;
  if (!next) return 0;
  return metricDisplayNumber(next.type.category.metric, next.type.threshold);
}
function displayTotal(chain: Chain): number {
  // Only reachable for completed chains (chain.earned is set there).
  if (!chain.earned) return 0;
  return metricDisplayNumber(
    chain.earned.type.category.metric,
    chain.earned.progress,
  );
}

// Progress toward the next tier as a percent (0-100); a completed chain
// reads as a full bar.
function nextPct(chain: Chain): number {
  const next = chain.next;
  if (!next || next.type.threshold <= 0) return 100;
  const clamped = Math.min(Math.max(next.progress, 0), next.type.threshold);
  return Math.round((clamped / next.type.threshold) * 100);
}

// Progress toward the next tier as a single string ("197 из 500 лайков").
// Built in script (not interpolated across template text nodes) so a copied
// selection always has clean spaces — no whitespace-condense glue, no stray
// newlines. Unit label SSOT is formatThreshold.
function progressLabel(chain: Chain): string {
  const next = chain.next;
  if (!next) return "Максимальный уровень";
  return `${displayCurrent(chain)} из ${formatThreshold(
    next.type.category.metric,
    next.type.threshold,
  )}`;
}
</script>

<template>
  <!-- Loading deliberately has no BlockTitle: an empty catalog hides the
       whole section, and flashing a "Достижения" heading that then
       disappears would be worse than a bare inline loading hint. -->
  <section v-if="loading && !loaded" class="achievements-section">
    <SecondaryText>Загрузка…</SecondaryText>
  </section>

  <section v-else-if="error" class="achievements-section">
    <BlockTitle>Достижения</BlockTitle>
    <ErrorState
      message="Не удалось загрузить достижения"
      :retry="() => load(username)"
    />
  </section>

  <section v-else-if="hasAnyChain" class="achievements-section">
    <BlockTitle>Достижения</BlockTitle>
    <div class="chains">
      <Tooltip
        v-for="chain in chains"
        :key="chain.categoryId"
        placement="top"
        focusable
      >
        <div
          class="chain"
          :class="[
            tierClass(chain.maxEarnedTier),
            { 'chain--locked': chain.locked },
          ]"
        >
          <div class="chain-icon-wrap">
            <GameIcon :name="chain.iconName" class="chain-icon" />
            <span
              v-if="chain.maxEarnedTier > 0"
              class="chain-degree"
              :class="tierClass(chain.maxEarnedTier)"
              aria-hidden="true"
              >{{ roman(chain.maxEarnedTier) }}</span
            >
          </div>

          <span class="chain-title">{{ tileTitle(chain) }}</span>

          <div
            class="chain-bar"
            role="progressbar"
            :aria-valuenow="chain.completed ? 100 : nextPct(chain)"
            :aria-valuemin="0"
            :aria-valuemax="100"
            :aria-label="`${chain.title}: ${chain.completed ? 'завершено' : `${displayCurrent(chain)}/${displayGoal(chain)}`}`"
          >
            <div
              class="chain-bar-fill"
              :style="{
                width: (chain.completed ? 100 : nextPct(chain)) + '%',
              }"
            />
          </div>

          <!-- Numbers under the bar: digits only, no units (units read from
               the icon/title). Display-unit (years for registration) — the
               same numbers as the popover. Real " / " text node so a copied
               selection reads "2200 / 5000", not glued "2200/5000". -->
          <div class="chain-progress" aria-hidden="true">
            <template v-if="chain.completed">{{
              displayTotal(chain)
            }}</template>
            <template v-else
              >{{ displayCurrent(chain) }} / {{ displayGoal(chain) }}</template
            >
          </div>
        </div>

        <template #content>
          <div class="chain-popover" :class="tierClass(chain.maxEarnedTier)">
            <div class="chain-popover__header">
              <strong class="chain-popover__title">{{ chain.title }}</strong>
              <span v-if="chain.completed" class="chain-popover__badge">
                Завершено
              </span>
              <span
                v-else
                class="chain-popover__badge chain-popover__badge--muted"
              >
                {{ chain.earnedCount }} / {{ chain.tiers.length }}
              </span>
            </div>

            <p class="chain-popover__desc">{{ chain.description }}</p>

            <!-- Single text node from one computed string: copies exactly as
                 "197 из 500 лайков" — guaranteed spaces, uniform color. -->
            <p v-if="!chain.completed" class="chain-popover__progress">
              {{ progressLabel(chain) }}
            </p>

            <ul class="chain-popover__tiers">
              <li
                v-for="t in chain.tiers"
                :key="t.type.id"
                class="chain-popover__tier"
                :class="[
                  tierClass(t.type.tier),
                  t.earnedUtc
                    ? 'chain-popover__tier--earned'
                    : 'chain-popover__tier--locked',
                ]"
              >
                <span class="chain-popover__tier-degree">{{
                  roman(t.type.tier ?? 0)
                }}</span>
                <span class="chain-popover__tier-title">{{
                  t.type.title
                }}</span>
                <span class="chain-popover__tier-threshold">
                  {{
                    formatThreshold(t.type.category.metric, t.type.threshold)
                  }}
                </span>
              </li>
            </ul>
          </div>
        </template>
      </Tooltip>
    </div>
  </section>
</template>

<style scoped lang="sass">
.achievements-section
  display: flex
  flex-direction: column
  // Unified with .awards-section: explicit gap between the BlockTitle and
  // the grid + zeroed intrinsic margins on the BlockTitle.
  gap: $small

  :deep(h2)
    margin: 0

// King's Bounty awards-style: a clean grid of icons + a roman degree in the
// icon corner. Achievements are the smaller, denser section — 8 per row with
// a 52px icon (vs the awards' 5 columns / 72px) — so they read lighter than
// the curated awards. A tight COLUMN gap keeps the eight cells wide enough
// for the icon and a 2-line caption; a larger ROW gap gives the rows (which
// carry a progress bar) vertical breathing room. The Tooltip trigger gets
// `display: flex` (`display: contents` broke getBoundingClientRect and the
// tooltip drifted to the top-left corner).
.chains
  display: grid
  grid-template-columns: repeat(8, minmax(0, 1fr))
  row-gap: $small
  column-gap: $tiny
  align-items: start

  @media (max-width: 640px)
    grid-template-columns: repeat(4, minmax(0, 1fr))

  :deep(.tooltip-trigger)
    display: flex

.chain
  position: relative
  display: flex
  flex-direction: column
  align-items: center
  // Fill the grid cell (the Tooltip trigger is display:flex, so the tile is
  // a flex item and must be told to take the full width) — this makes the
  // centered icon/caption align exactly like the award tiles.
  width: 100%
  // Same icon-to-caption rhythm as .award (gap $minor); the degree pill
  // hangs -4px below the icon exactly like the award badge, so its
  // proximity to the caption is identical across sections.
  gap: $minor
  cursor: default

// The wrapper is the anchor for the absolutely-positioned roman degree —
// without it the badge would attach to the tile (not the icon) and drift
// right. Wrapper size = icon size (1em = 52px from the font-size below).
.chain-icon-wrap
  position: relative
  display: inline-block
  line-height: 0

// The icon is tinted with the tier color of the senior earned tier — the
// level reads at a glance, the roman badge doubles it with a number. At
// 52px vs the awards' 72px — awards are deliberately the larger section.
.chain-icon
  font-size: 52px
  color: var(--card-tier-color, $heading)

// Fully locked chain (no earned tiers yet): ghost muting via OPACITY, not
// just a color swap — next to tier-tinted earned icons a merely gray icon
// still reads as "some dark metal", not as "unearned". Same approach as
// the original design: the icon goes nearly transparent (0.22), the title
// and the progress digits drop to 0.45 (the same locked opacity as the
// popover tier rows). No tier tint; the roman badge is absent by template
// condition (maxEarnedTier = 0).
.chain--locked
  .chain-icon
    color: $text-muted
    opacity: 0.22
  .chain-title
    color: $text-muted
    opacity: 0.45
  .chain-progress
    opacity: 0.45

// Roman degree of the earned tier — a pill badge in the bottom-right
// corner of the icon. Tier-colored background, contrasting text, thin
// border in the tile color (separates it from the icon).
.chain-degree
  position: absolute
  right: -4px
  bottom: -4px
  min-width: 16px
  height: 16px
  padding: 0 4px
  display: inline-flex
  align-items: center
  justify-content: center
  box-sizing: border-box
  font-size: 9px
  font-weight: 700
  letter-spacing: 0.5px
  line-height: 1
  color: var(--tier-badge-text)
  background-color: var(--card-tier-color, $heading)
  border: 2px solid $bg-page
  border-radius: $minor
  font-variant-numeric: tabular-nums
  text-shadow: 0 0 1px rgba(0, 0, 0, 0.4)

// Thematic title of the earned tier, centered under the icon. Smaller than
// the award caption ($tertiary vs $secondary) — achievements are the lighter
// section, and the smaller type also fits the longest word inside the narrow
// 8-column cell without a mid-word break. A reserved 2-line min-height keeps
// 1- and 2-line captions the same height (no ragged bottoms); a longer title
// wraps in full rather than being clipped to one line.
.chain-title
  width: 100%
  box-sizing: border-box
  font-size: $tertiary-font-size
  font-weight: 500
  color: $heading
  letter-spacing: 0.1px
  text-align: center
  line-height: 1.2
  overflow-wrap: break-word
  min-height: 2.4em

// Bar colors are unified with the poll bar (`ProgressBar.vue`):
// background — $progress-bg-overlay, fill — $progress-fill-overlay.
// The tier color stays on the icon and the roman degree to mark the
// level, while the bar itself reads like any progress scale site-wide.
.chain-bar
  width: 70%
  height: 6px
  background-color: $progress-bg-overlay
  border-radius: 3px
  overflow: hidden

  .chain-bar-fill
    height: 100%
    background-color: $progress-fill-overlay
    border-radius: 3px
    transition: width $transition-normal

// Progress digits under the bar: "2200 / 5000" — a short quantitative
// read on top of the percent scale. Digits only (no units), tabular-nums
// so the column does not wobble. Low-priority tile meta color/size.
.chain-progress
  font-size: $tertiary-font-size
  color: $text-muted
  font-variant-numeric: tabular-nums
  line-height: 1
  text-align: center

// Tier color tokens — the SAME award-metal tokens as ProfileAwardsSection
// (see ThemeVariables.css), so award and achievement icons render in
// identical metals. Platinum (tier IV) is achievement-only — awards have
// no fourth metal; it stays colder than silver (a teal cast) so the
// silver/platinum pair never gets confused.
.tier-bronze
  --card-tier-color: var(--award-bronze)
.tier-silver
  --card-tier-color: var(--award-silver)
.tier-gold
  --card-tier-color: var(--award-gold)
.tier-platinum
  --card-tier-color: var(--achievement-platinum)
.tier-base
  --card-tier-color: #{$heading}

// --- Rich popover ---
// The `#content` slot in `<Tooltip>` takes HTML; the tooltip provides
// padding and max-width, the rest is styled here. The tooltip's
// white-space: pre-line does not interfere — we render explicit blocks.
// The popover root carries the chain's tier class (the tooltip is
// teleported to body, so the tile's --card-tier-color does not cascade);
// each tier row then overrides it with its own tier class.

.chain-popover
  display: flex
  flex-direction: column
  gap: 8px
  // Wide enough for the longest catalog tier title ("Всегда есть что
  // сказать") + its threshold to sit on ONE line in the tiers table —
  // tier titles are never ellipsized (see &__tier-title).
  min-width: 300px
  white-space: normal

  &__header
    display: flex
    align-items: center
    gap: $small

  &__title
    flex: 1 1 auto
    font-size: $secondary-font-size
    color: $tooltip-text

  &__badge
    flex: 0 0 auto
    padding: 2px 6px
    font-size: 10px
    font-weight: 600
    line-height: 1
    color: var(--tier-badge-text)
    background-color: var(--card-tier-color, $heading)
    border-radius: 999px
    text-transform: uppercase
    letter-spacing: 0.5px

    &--muted
      color: $tooltip-text
      background-color: rgba(255, 255, 255, 0.12)

  &__desc
    margin: 0
    font-size: $secondary-font-size
    line-height: 1.4
    color: $tooltip-text
    opacity: 0.8

  &__progress
    margin: 0
    font-size: $secondary-font-size
    color: $tooltip-text

  &__tiers
    margin: 4px 0 0
    padding: 0
    list-style: none
    display: flex
    flex-direction: column
    gap: 2px
    border-top: 1px solid rgba(255, 255, 255, 0.08)
    padding-top: 6px

  &__tier
    display: grid
    grid-template-columns: 22px 1fr auto
    align-items: center
    gap: $small
    padding: 2px 0
    font-size: $secondary-font-size
    color: $tooltip-text

    &--locked
      opacity: 0.45

  &__tier-degree
    text-align: center
    font-size: 10px
    font-weight: 700
    color: var(--tier-badge-text)
    background-color: var(--card-tier-color, $heading)
    border-radius: $minor
    padding: 2px 0
    line-height: 1

  // Never truncated: every catalog title fits the popover width in one
  // line (see min-width above); anything longer wraps whole instead of
  // ellipsizing — a clipped tier title is unreadable.
  &__tier-title
    overflow-wrap: break-word

  &__tier-threshold
    font-variant-numeric: tabular-nums
    opacity: 0.75
    font-size: 11px
</style>
