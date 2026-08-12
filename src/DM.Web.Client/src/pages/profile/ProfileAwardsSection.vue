<script setup lang="ts">
import { formatDate } from "@/shared/lib/utils/datetime";
/**
 * ProfileAwardsSection — the "Награды" block inside the "Достижения" tab.
 *
 * Curated awards: granted by Admin/SeniorModerator via the admin pages.
 * The award catalog (`AwardType`) is timeless; a specific
 * contest is stored in `ContestSeries` and linked via an FK on UserAward.
 *
 * Tile:
 *   - Icon: for contest_* placements in an Art series — palette; otherwise type.iconName
 *   - Corner badge on the icon (series-linked awards only): a single pill
 *     in the bottom-right corner with the contest kind + series number
 *     ("Лит #22" / "Арт #2"). The series YEAR is not on the tile — it
 *     lives in the popover context line ("Литературный конкурс #23 (2024)").
 *   - Title under the icon:
 *       * For contest_first/second/third — the series title ("23-й литературный
 *         конкурс"); the placement is read from the tier color (gold/silver/bronze)
 *       * For special awards (popular_vote, best_critic, guesser) — type.title
 *         ("Народное признание, например" - the last two words are part of the
 *         name, "Лучший критик", "Угадайка")
 *
 * The rich popover shows type.description + a link to the results topic.
 */
import { computed, onMounted, ref, watch } from "vue";
import { achievementApi } from "@/entities/achievement";
import { ContestType, type UserAward } from "@/shared/api/models/achievements";
import { GameIcon } from "@/shared/ui/Icon";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Tooltip } from "@/shared/ui/Tooltip";
import { formatContestSeriesTitle } from "@/entities/achievement";
import { toExternalHref, toInternalPath } from "@/shared/lib/utils/internalUrl";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const props = defineProps<{ username: string }>();

/** Load/content status for the tab-level empty-state coordination in
 * ProfileAchievements: the shared "Пока нет наград и достижений" text
 * shows only when BOTH sections settle empty. */
const emit = defineEmits<{
  state: [value: "loading" | "error" | "empty" | "content"];
}>();

const awards = ref<UserAward[]>([]);
const loaded = ref(false);

// The section refetches when the profile changes under it, so two answers can be
// on the wire at once. Without a guard the slower one wins and the tab reports a
// state — which the parent uses to decide the shared empty text — for a profile
// the reader has already left.
const { loading, error, run } = useGuardedRequest({
  message: "Не удалось загрузить награды",
  clearErrorOnStart: true,
});

function fetchAwards(username: string) {
  loaded.value = false;
  emit("state", "loading");
  return run(
    () => achievementApi.getUserAwards(username),
    (data) => {
      awards.value = data?.resources ?? [];
    },
  ).finally(() => {
    loaded.value = true;
    emit(
      "state",
      error.value ? "error" : awards.value.length > 0 ? "content" : "empty",
    );
  });
}

onMounted(() => fetchAwards(props.username));
watch(
  () => props.username,
  (next) => fetchAwards(next),
);

// Tier maps to a visual class. 1/2/3 are the contest metals (gold/silver/
// bronze); 5 is "diamond" — reserved for a unique, place-less honor (the
// "Почетный гоблин"), so it never reads as a contest placement.
function tierClass(tier: number | null): string {
  if (tier === 1) return "tier-gold";
  if (tier === 2) return "tier-silver";
  if (tier === 3) return "tier-bronze";
  if (tier === 4) return "tier-steel";
  if (tier === 5) return "tier-diamond";
  return "tier-base";
}

/**
 * Place award: contest_first/second/third. Its title is the place label
 * from the catalog, while the actual "which contest" meaning lives in
 * the linked ContestSeries, so for display we substitute the title with
 * the series format. Special awards (popular_vote, best_critic, guesser)
 * are self-contained — their title is the full name already.
 */
function isPlaceAward(code: string): boolean {
  return code.startsWith("contest_");
}

/**
 * Title for the tile and popover. For placements — the series title; for special awards —
 * type.title as is.
 */
function awardDisplayTitle(a: UserAward): string {
  if (isPlaceAward(a.type.code) && a.contestSeries) {
    return formatContestSeriesTitle(
      a.contestSeries.contestType,
      a.contestSeries.number,
    );
  }
  return a.type.title;
}

/**
 * Second line of the popover header with contest context ("Литературный конкурс
 * #23, 2024"). Shown only when the award is tied to a series —
 * for placements the title ALREADY contains the series via
 * `awardDisplayTitle`, so the number and year are enough here without
 * repeating the word "конкурс" twice.
 *
 * ContestSeries has no season/kind field at the moment (only
 * contestType/number/year) — when such a field is added on the backend,
 * "(зимний" / "(летний" will need to be inserted before the year here.
 */
function contestContextLabel(a: UserAward): string | null {
  if (!a.contestSeries) return null;
  const kind =
    a.contestSeries.contestType === ContestType.Literary
      ? "Литературный конкурс"
      : "Арт-конкурс";
  return `${kind} #${a.contestSeries.number} (${a.contestSeries.year})`;
}

/**
 * Combined label for the single bottom-right icon badge: capitalized
 * contest kind + series number ("Лит #22", "Арт #2"). Rendered only for
 * series-linked awards (the template guards on `a.contestSeries`).
 */
function contestBadgeLabel(a: UserAward): string {
  if (!a.contestSeries) return "";
  const kind =
    a.contestSeries.contestType === ContestType.Literary ? "Лит" : "Арт";
  return `${kind} #${a.contestSeries.number}`;
}

/**
 * Tile icon. Art contest placements show palette (as a thematic
 * trophy-cup analog). Everything else — type.iconName.
 */
function awardIcon(a: UserAward): string {
  if (
    isPlaceAward(a.type.code) &&
    a.contestSeries?.contestType === ContestType.Art
  ) {
    return "palette";
  }
  return a.type.iconName;
}

const hasAwards = computed(() => awards.value.length > 0);
</script>

<template>
  <section v-if="loading && !loaded" class="awards-section">
    <BlockTitle>Награды</BlockTitle>
    <SecondaryText>Загрузка...</SecondaryText>
  </section>

  <section v-else-if="error" class="awards-section">
    <BlockTitle>Награды</BlockTitle>
    <ErrorState :message="error" :retry="() => fetchAwards(username)" />
  </section>

  <section v-else-if="hasAwards" class="awards-section">
    <BlockTitle>Награды</BlockTitle>
    <div class="awards-grid">
      <Tooltip v-for="a in awards" :key="a.id" focusable>
        <div class="award" :class="tierClass(a.type.tier)">
          <div class="award-icon-wrap">
            <GameIcon :name="awardIcon(a)" class="award-icon" />
            <span
              v-if="a.contestSeries"
              class="award-series"
              aria-hidden="true"
              >{{ contestBadgeLabel(a) }}</span
            >
          </div>
          <div class="award-title">{{ awardDisplayTitle(a) }}</div>
        </div>

        <template #content>
          <div class="award-popover">
            <div class="award-popover__header">
              <strong class="award-popover__title">{{
                awardDisplayTitle(a)
              }}</strong>
              <span
                v-if="contestContextLabel(a)"
                class="award-popover__context"
                >{{ contestContextLabel(a) }}</span
              >
            </div>

            <p class="award-popover__desc">{{ a.type.description }}</p>

            <!-- Three branches, not two. workUrl and topicUrl are free text a
                 moderator typed and nothing checks them on the way in, so a
                 value that is neither a path of this site nor an http(s)
                 address (javascript:, data:) has no href it can be rendered
                 with and stays a plain label. See lib/utils/internalUrl. -->
            <div
              v-if="a.workUrl || a.contestSeries?.topicUrl"
              class="award-popover__links"
            >
              <span v-if="a.workUrl" class="award-popover__link-item"
                ><span class="award-popover__bracket">[</span
                ><router-link
                  v-if="toInternalPath(a.workUrl)"
                  :to="toInternalPath(a.workUrl)!"
                  class="award-popover__link"
                  >Топик с работой</router-link
                ><a
                  v-else-if="toExternalHref(a.workUrl)"
                  :href="toExternalHref(a.workUrl)!"
                  class="award-popover__link"
                  rel="noopener"
                  >Топик с работой</a
                ><span v-else>Топик с работой</span
                ><span class="award-popover__bracket">]</span></span
              >
              <span
                v-if="a.contestSeries?.topicUrl"
                class="award-popover__link-item"
                ><span class="award-popover__bracket">[</span
                ><router-link
                  v-if="toInternalPath(a.contestSeries.topicUrl)"
                  :to="toInternalPath(a.contestSeries.topicUrl)!"
                  class="award-popover__link"
                  >Топик с итогами</router-link
                ><a
                  v-else-if="toExternalHref(a.contestSeries.topicUrl)"
                  :href="toExternalHref(a.contestSeries.topicUrl)!"
                  class="award-popover__link"
                  rel="noopener"
                  >Топик с итогами</a
                ><span v-else>Топик с итогами</span
                ><span class="award-popover__bracket">]</span></span
              >
            </div>

            <div class="award-popover__footer">
              Получена {{ formatDate(a.awardedUtc) }}
            </div>
          </div>
        </template>
      </Tooltip>
    </div>
  </section>
</template>

<style scoped lang="sass">
.awards-section
  display: flex
  flex-direction: column
  // Unified with .achievements-section: an explicit gap between BlockTitle
  // and the grid + zeroing BlockTitle's intrinsic margins.
  gap: $small

  :deep(h2)
    margin: 0

// Awards are the larger, rarer, curated tiles: 5 per row with a 72px icon,
// so an award reads with clearly more weight than an achievement (which is
// 8-per-row / 52px — see .chains). Five columns keep the icon snug in its
// cell (no wide empty gutter around it) and let each caption wrap onto two
// lines instead of being stretched across one.
.awards-grid
  display: grid
  grid-template-columns: repeat(5, minmax(0, 1fr))
  gap: $medium
  align-items: start

  @media (max-width: $bp-mobile)
    grid-template-columns: repeat(3, minmax(0, 1fr))

.award
  display: flex
  flex-direction: column
  align-items: center
  gap: $minor
  cursor: help
  color: $text

  .award-icon
    font-size: 72px

// Icon wrapper — the relative anchor for the corner badges, so they
// attach to the icon's bottom corners rather than to the tile.
// Same pattern as .chain-icon-wrap in ProfileAchievementsSection.
.award-icon-wrap
  position: relative
  display: inline-block
  line-height: 0

// Contest badge ("Лит #22" / "Арт #2") — a single pill in the bottom-right
// corner of the icon: capitalized contest kind + series number. Renders
// only for awards linked to a contest series (the series YEAR shows in
// the popover context line, not on the tile). Width hugs the content (no
// fixed min-width — labels vary in length); geometry/font/border match
// the achievements' degree pill. Tier background color comes from the
// tier classes below (see .tier-gold etc).
.award-series
  position: absolute
  right: -4px
  bottom: -4px
  height: 16px
  padding: 0 4px
  display: inline-flex
  align-items: center
  justify-content: center
  box-sizing: border-box
  font-size: 9px
  font-weight: 700
  letter-spacing: 0.3px
  line-height: 1
  white-space: nowrap
  color: $heading
  background-color: var(--tier-badge-bg)
  // One ring, not two. The border in the page colour is what lifts the badge
  // off the icon it overlaps; a second hairline in the metal was added on top
  // of it and drew outside the border, so the badge read as a dark pill with a
  // white gap and a stray metal ring around it.
  border: 2px solid $bg-page
  border-radius: $minor
  font-variant-numeric: tabular-nums

// Caption, centered under the icon. A reserved 2-line min-height keeps the
// common 1- and 2-line titles all the same height (no ragged bottoms); a
// longer title wraps in full onto a third line rather than being clipped to
// one. overflow-wrap breaks an over-long word instead of overflowing the tile.
.award-title
  width: 100%
  box-sizing: border-box
  font-size: $secondary-font-size
  // The name of an entity, at the weight and the colour the site sets a name
  // in. $heading is the brown of h1 and of block titles; on a caption under an
  // icon it read as a heading of its own, and bold made that louder still.
  font-weight: bold
  color: $text
  letter-spacing: 0.1px
  text-align: center
  line-height: 1.2
  overflow-wrap: break-word
  min-height: 2.4em

// Tier metal colors — shared tokens (see ThemeVariables.css), also
// consumed by ModerationAwardTypes.vue for the same catalog dedup. The metal
// goes on the glyph of both the icon and the series badge; the badge's fill is
// the one dark token, so the tier colour reads on the letters instead of on
// 16px of pill behind black text.
.tier-gold
  .award-icon,
  .award-series
    color: var(--award-gold)
.tier-silver
  .award-icon,
  .award-series
    color: var(--award-silver)
.tier-bronze
  .award-icon,
  .award-series
    color: var(--award-bronze)
.tier-steel
  .award-icon,
  .award-series
    color: $text-meta
.tier-base
  .award-icon,
  .award-series
    color: $heading
// Diamond — a unique, place-less honor (currently only "Почетный гоблин").
// Rendered in the achievement platinum (the tier-IV colour from the
// achievements grid), so the honour reads as a distinct, premium mark rather
// than a contest metal. Colour: --award-diamond → --achievement-platinum (SSOT).
.tier-diamond
  .award-icon,
  .award-series
    color: var(--award-diamond)

// --- Rich popover ---
.award-popover
  display: flex
  flex-direction: column
  gap: 6px
  min-width: 220px
  white-space: normal

  &__header
    display: flex
    flex-direction: column
    gap: 2px

  &__title
    font-size: $secondary-font-size
    color: $tooltip-text

  // Second header line — contest series context (D1): number + year of
  // the series the award belongs to. Secondary size, muted.
  &__context
    font-size: $tertiary-font-size
    color: $tooltip-text
    opacity: 0.65

  &__desc
    margin: 0
    font-size: $secondary-font-size
    line-height: 1.4
    color: $tooltip-text
    opacity: 0.8

  &__links
    display: flex
    flex-direction: column
    gap: 2px
    margin: 4px 0 0
    padding: 6px 0 0
    border-top: 1px solid rgba(255, 255, 255, 0.08)
    font-size: $secondary-font-size
    color: $tooltip-text

  // Bracket motif ("[Топик с работой]"): muted brackets frame the link text,
  // link itself in $tooltip-link (readable on the dark tooltip surface,
  // unlike $link) with no underline at rest, underline on hover only —
  // matches the site-wide bracket-counter identity (C1).
  &__link-item
    display: block

  &__bracket
    color: $tooltip-text
    opacity: 0.5

  &__link
    color: $tooltip-link
    text-decoration: none

    &:hover
      color: $tooltip-link-hover
      text-decoration: underline
      text-underline-offset: 2px

  &__footer
    font-size: $tertiary-font-size
    color: $tooltip-text
    opacity: 0.55
    margin-top: 4px
</style>
