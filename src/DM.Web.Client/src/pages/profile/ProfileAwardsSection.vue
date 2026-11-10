<script setup lang="ts">
import { formatDate } from "@/shared/lib/utils/datetime";
/**
 * The "Награды" block of the "Достижения" tab: curated awards. A contest
 * placement is titled by its series and placed by the metal; a special award
 * by its own title. A series award carries a "Лит #22" pill on its icon.
 */
import { computed, onMounted, ref, watch } from "vue";
import { achievementApi } from "@/entities/achievement";
import { ContestType, type UserAward } from "@/shared/api/models/achievements";
import GameIcon from "@/shared/ui/Icon/GameIcon.vue";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Tooltip } from "@/shared/ui/Tooltip";
import {
  awardMetal,
  formatContestSeriesTitle,
  metalStyle,
} from "@/entities/achievement";
import { toExternalHref, toInternalPath } from "@/shared/lib/utils/internalUrl";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import type { AchievementsBlockState } from "./achievementsTabState";

const props = defineProps<{ username: string }>();

/** Whether the section settled empty, for the tab's shared empty text. */
const emit = defineEmits<{
  state: [value: AchievementsBlockState];
}>();

const awards = ref<UserAward[]>([]);
const loaded = ref(false);

// The profile can change under the section: the guard drops the older answer.
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

/** A contest placement, whose meaning is the linked series and not its title. */
function isPlaceAward(code: string): boolean {
  return code.startsWith("contest_");
}

/** The title of a tile and its popover: the series for a placement, else the type's. */
function awardDisplayTitle(a: UserAward): string {
  if (isPlaceAward(a.type.code) && a.contestSeries) {
    return formatContestSeriesTitle(
      a.contestSeries.contestType,
      a.contestSeries.number,
    );
  }
  return a.type.title;
}

/** The contest of a series award ("Литературный конкурс #23, 2024"), under the popover title. */
function contestContextLabel(a: UserAward): string | null {
  if (!a.contestSeries) return null;
  const kind =
    a.contestSeries.contestType === ContestType.Literary
      ? "Литературный конкурс"
      : "Арт-конкурс";
  return `${kind} #${a.contestSeries.number} (${a.contestSeries.year})`;
}

/** The pill on the icon of a series award: "Лит #22". */
function contestBadgeLabel(a: UserAward): string {
  if (!a.contestSeries) return "";
  const kind =
    a.contestSeries.contestType === ContestType.Literary ? "Лит" : "Арт";
  return `${kind} #${a.contestSeries.number}`;
}

/** The topics an award's card links to: the work, then the contest's results. */
function awardLinks(a: UserAward) {
  return [
    { url: a.workUrl, label: "Топик с работой" },
    { url: a.contestSeries?.topicUrl, label: "Топик с итогами" },
  ].filter((link): link is { url: string; label: string } => !!link.url);
}

/** The icon of a tile: a palette for an art placement, else the type's. */
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
        <!-- The tier picks one of the four metals; icon and badge read it. -->
        <div class="award" :style="metalStyle(awardMetal(a.type.tier))">
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

            <!-- A link typed by a moderator that is neither a path of the site nor an http(s) address stays a label. -->
            <div
              v-if="a.workUrl || a.contestSeries?.topicUrl"
              class="award-popover__links"
            >
              <span
                v-for="link in awardLinks(a)"
                :key="link.label"
                class="award-popover__link-item"
                ><span class="award-popover__bracket">[</span
                ><router-link
                  v-if="toInternalPath(link.url)"
                  :to="toInternalPath(link.url)!"
                  class="award-popover__link"
                  >{{ link.label }}</router-link
                ><a
                  v-else-if="toExternalHref(link.url)"
                  :href="toExternalHref(link.url)!"
                  class="award-popover__link"
                  rel="noopener"
                  >{{ link.label }}</a
                ><span v-else>{{ link.label }}</span
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
  // As .achievements-section: the gap, the heading's own margins off.
  gap: $small

  :deep(h2)
    margin: 0

.awards-grid
  display: grid
  grid-template-columns: repeat($award-columns, minmax(0, 1fr))
  gap: $medium
  align-items: start

.award
  display: flex
  flex-direction: column
  align-items: center
  gap: $minor
  cursor: help
  color: $text

  // The tier's metal (--tier-metal); a tier without one stays neutral.
  .award-icon
    font-size: $award-icon-size
    color: var(--tier-metal, $text-muted)

// The anchor of the corner pill, as .chain-icon-wrap.
.award-icon-wrap
  position: relative
  display: inline-block
  line-height: 0

// The series pill: the metal under light lettering and a border in the page
// colour, as the achievements' pill; as wide as its words.
.award-series
  position: absolute
  right: -$minor
  bottom: -$minor
  height: $tier-pill-size
  padding: 0 $minor
  display: inline-flex
  align-items: center
  justify-content: center
  box-sizing: border-box
  font-size: 9px
  font-weight: 700
  letter-spacing: 0.3px
  line-height: 1
  white-space: nowrap
  color: var(--tier-badge-text)
  background-color: var(--tier-metal, $text-muted)
  border: 2px solid $bg-page
  border-radius: $minor
  font-variant-numeric: tabular-nums
  text-shadow: 0 0 1px var(--tier-badge-halo)

// Two lines reserved so captions stand even; a longer title wraps whole.
.award-title
  width: 100%
  box-sizing: border-box
  font-size: $secondary-font-size
  // A name, not a heading: the text colour, not $heading.
  font-weight: bold
  color: $text
  letter-spacing: 0.1px
  text-align: center
  line-height: 1.2
  overflow-wrap: break-word
  min-height: 2.4em

.award-popover
  display: flex
  flex-direction: column
  gap: $tile-hint-step
  min-width: 220px
  white-space: normal

  &__header
    display: flex
    flex-direction: column
    gap: $tiny

  &__title
    font-size: $secondary-font-size
    color: $tooltip-text

  // The contest of the series: number and year.
  &__context
    font-size: $tertiary-font-size
    color: $tooltip-text
    opacity: $opacity-soft

  &__desc
    margin: 0
    font-size: $secondary-font-size
    line-height: 1.4
    color: $tooltip-text
    opacity: $opacity-soft

  &__links
    display: flex
    flex-direction: column
    gap: $tiny
    margin: $minor 0 0
    padding: $tile-hint-step 0 0
    border-top: 1px solid rgba(255, 255, 255, 0.08)
    font-size: $secondary-font-size
    color: $tooltip-text

  // "[Топик с работой]": muted brackets, the link in $tooltip-link, underlined on hover.
  &__link-item
    display: block

  &__bracket
    color: $tooltip-text
    opacity: $muted-opacity

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
    opacity: $opacity-half
    margin-top: $minor
</style>
