<script setup lang="ts">
/**
 * The "Достижения" block: tiers grouped by category into chains, progress
 * computed from the user's metrics (entities/achievement). A tile shows the
 * senior earned tier; a chain with none is drawn ghosted under its first tier.
 */
import { computed, onMounted, ref, watch } from "vue";
import { achievementApi } from "@/entities/achievement";
import type {
  AchievementType,
  UserAchievement,
} from "@/shared/api/models/achievements";
import type { User } from "@/shared/api/models/community";
import GameIcon from "@/shared/ui/Icon/GameIcon.vue";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Tooltip } from "@/shared/ui/Tooltip";
import ProfileAchievementPopover from "./ProfileAchievementPopover.vue";
import {
  achievementChains,
  chainTitle,
  tierNumeral,
  chainCurrent,
  chainGoal,
  chainTotal,
  chainPercent,
  awardMetal,
  metalStyle,
  type AchievementChain,
} from "@/entities/achievement";
import {
  shownOnPage,
  useGuardedRequest,
} from "@/shared/lib/composables/useGuardedRequest";
import type { AchievementsBlockState } from "./achievementsTabState";

const props = defineProps<{
  username: string;
  /** The profile owner, whose metrics give the progress. */
  user: User;
}>();

/** Whether the section settled empty, for the tab's shared empty text; with a catalog it never is. */
const emit = defineEmits<{
  state: [value: AchievementsBlockState];
}>();

const earned = ref<UserAchievement[]>([]);
const catalog = ref<AchievementType[]>([]);
const loaded = ref(false);

// The profile can change under the section: the guard drops the older pair of answers.
const { loading, error, run } = useGuardedRequest({
  message: "Не удалось загрузить достижения",
  clearErrorOnStart: true,
});

function load(username: string) {
  loaded.value = false;
  emit("state", "loading");
  return run(
    async () => {
      // Each answer is taken over as it lands; the section says a failure in its own box.
      const [earnedRes, catalogRes] = await Promise.all([
        achievementApi.getUserAchievements(username).then(shownOnPage),
        achievementApi.getAchievementTypes().then(shownOnPage),
      ]);
      // The chains need both halves, so either failure is the section's.
      return {
        data: {
          earned: earnedRes.data?.resources ?? [],
          catalog: catalogRes.data?.resources ?? [],
        },
        error: earnedRes.error ?? catalogRes.error,
      };
    },
    (data) => {
      earned.value = data?.earned ?? [];
      catalog.value = data?.catalog ?? [];
    },
  ).finally(() => {
    loaded.value = true;
    emit(
      "state",
      error.value ? "error" : hasAnyChain.value ? "content" : "empty",
    );
  });
}

onMounted(() => load(props.username));
watch(
  () => props.username,
  (next) => load(next),
);

const chains = computed<AchievementChain[]>(() =>
  achievementChains(catalog.value, earned.value, props.user),
);

const hasAnyChain = computed(() => chains.value.length > 0);
</script>

<template>
  <!-- No heading while loading: an empty catalog hides the whole section. -->
  <section v-if="loading && !loaded" class="achievements-section">
    <SecondaryText>Загрузка...</SecondaryText>
  </section>

  <section v-else-if="error" class="achievements-section">
    <BlockTitle>Достижения</BlockTitle>
    <ErrorState :message="error" :retry="() => load(username)" />
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
          :class="{ 'chain--locked': chain.locked }"
          :style="metalStyle(awardMetal(chain.earnedTier))"
        >
          <div class="chain-icon-wrap">
            <GameIcon :name="chain.iconName" class="chain-icon" />
            <span
              v-if="chain.earnedPosition > 0"
              class="chain-degree"
              aria-hidden="true"
              >{{ tierNumeral(chain.earnedPosition) }}</span
            >
          </div>

          <span class="chain-title">{{ chainTitle(chain) }}</span>

          <div
            class="chain-bar"
            role="progressbar"
            :aria-valuenow="chain.completed ? 100 : chainPercent(chain)"
            :aria-valuemin="0"
            :aria-valuemax="100"
            :aria-label="`${chain.title}: ${chain.completed ? 'завершено' : `${chainCurrent(chain)}/${chainGoal(chain)}`}`"
          >
            <div
              class="chain-bar-fill"
              :style="{
                width: (chain.completed ? 100 : chainPercent(chain)) + '%',
              }"
            />
          </div>

          <!-- Digits only, the same as the popover; a real " / " so a copy reads "2200 / 5000". -->
          <div class="chain-progress" aria-hidden="true">
            <template v-if="chain.completed">{{ chainTotal(chain) }}</template>
            <template v-else
              >{{ chainCurrent(chain) }} / {{ chainGoal(chain) }}</template
            >
          </div>
        </div>

        <template #content>
          <ProfileAchievementPopover :chain="chain" />
        </template>
      </Tooltip>
    </div>
  </section>
</template>

<style scoped lang="sass">
// The grid is measured against the column, not the window.
.achievements-section
  container: achievements / inline-size
  display: flex
  flex-direction: column
  // As .awards-section: the gap, the heading's own margins off.
  gap: $small

  :deep(h2)
    margin: 0

// Lighter than the awards; a tight column gap and a larger row gap for the
// bars. The Tooltip trigger is display: flex, as display: contents broke its
// positioning.
.chains
  display: grid
  grid-template-columns: repeat($achievement-columns, minmax(0, 1fr))
  row-gap: $small
  column-gap: $tiny
  align-items: start

  :deep(.tooltip-trigger)
    display: flex

@container achievements (max-width: #{$achievements-narrow-width})
  .chains
    grid-template-columns: repeat($achievement-columns-narrow, minmax(0, 1fr))

.chain
  position: relative
  display: flex
  flex-direction: column
  align-items: center
  // The full width of the cell, as the award tiles.
  width: 100%
  // The rhythm of .award: the pill hangs below the icon the same way.
  gap: $minor
  cursor: default

// The anchor of the roman pill, as large as the icon.
.chain-icon-wrap
  position: relative
  display: inline-block
  line-height: 0

// The metal of the senior earned tier (--tier-metal).
.chain-icon
  font-size: $achievement-icon-size
  color: var(--tier-metal, $text-muted)

// The earned tier's title, smaller than an award caption to fit the narrow
// cell; two lines reserved so captions stand even. Declared before
// .chain--locked, which overrides it.
.chain-title
  width: 100%
  box-sizing: border-box
  font-size: $tertiary-font-size
  // 600, not bold: full bold pushes the longest titles onto a third line.
  font-weight: 700
  // As the award caption: the name of a thing, not a heading.
  color: $text
  letter-spacing: 0.1px
  text-align: center
  line-height: 1.2
  overflow-wrap: break-word
  min-height: 2.4em
  // Centred in the reserve, so the caption stands as far from the icon as from the bar.
  display: flex
  flex-direction: column
  justify-content: center

// A chain with no earned tier is ghosted by opacity: a grey icon beside the
// metals still reads as some dark metal.
.chain--locked
  .chain-icon
    color: $text-muted
    opacity: $locked-icon-opacity
  .chain-title
    color: $text-muted
    opacity: $locked-text-opacity
  .chain-progress
    opacity: $locked-text-opacity

// The roman pill of the earned tier, drawn as the award badge: the metal under
// a light numeral, a border in the page colour.
.chain-degree
  position: absolute
  right: -$minor
  bottom: -$minor
  min-width: $tier-pill-size
  height: $tier-pill-size
  padding: 0 $minor
  display: inline-flex
  align-items: center
  justify-content: center
  box-sizing: border-box
  font-size: 9px
  font-weight: 700
  letter-spacing: $tier-pill-letter-spacing
  line-height: 1
  color: var(--tier-badge-text)
  background-color: var(--tier-metal, $text-muted)
  border: 2px solid $bg-page
  border-radius: $minor
  font-variant-numeric: tabular-nums
  text-shadow: 0 0 1px var(--tier-badge-halo)

// The colours of ProgressBar: the tier stays on the icon.
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

// Digits only, tabular, so the column does not wobble.
.chain-progress
  font-size: $tertiary-font-size
  color: $text-muted
  font-variant-numeric: tabular-nums
  line-height: 1
  text-align: center
</style>
