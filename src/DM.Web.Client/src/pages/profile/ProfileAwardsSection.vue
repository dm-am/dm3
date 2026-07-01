<script setup lang="ts">
/**
 * ProfileAwardsSection — блок «Награды» внутри таба «Достижения».
 *
 * Курируемые награды: выдаются Admin/SeniorModerator через админ-страницы.
 * Каталог наград (`AwardType`) — timeless (6 строк навсегда); конкретный
 * конкурс хранится в `ContestSeries` и привязывается через FK на UserAward.
 *
 * Тайл:
 *   - Иконка: для contest_* мест в Art-серии — palette; иначе — type.iconName
 *   - Year-бейдж (год серии) в правом нижнем углу
 *   - Title под иконкой:
 *       * Для contest_first/second/third — title серии («23-й литературный
 *         конкурс»), место читается по tier-color (gold/silver/bronze)
 *       * Для спец-наград (popular_vote, best_critic, guesser) — type.title
 *         («Народное признание», «Лучший критик», «Угадайка»)
 *
 * Rich popover показывает type.description + ссылку на топик итогов.
 */
import { computed, onMounted, ref, watch } from "vue";
import { achievementApi } from "@/shared/api";
import { ContestType, type UserAward } from "@/shared/api/models/achievements";
import { GameIcon } from "@/shared/ui/Icon";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { Tooltip } from "@/shared/ui/Tooltip";
import { formatContestSeriesTitle } from "@/shared/lib/achievements/formatThreshold";
import dayjs from "dayjs";

const props = defineProps<{ username: string }>();

const awards = ref<UserAward[]>([]);
const loaded = ref(false);
const loading = ref(false);

async function fetchAwards(username: string) {
  loading.value = true;
  loaded.value = false;
  try {
    const { data } = await achievementApi.getUserAwards(username);
    awards.value = data?.resources ?? [];
  } finally {
    loading.value = false;
    loaded.value = true;
  }
}

onMounted(() => fetchAwards(props.username));
watch(
  () => props.username,
  (next) => fetchAwards(next),
);

// Tier маппит в визуальный класс — пять диапазонов (gold/silver/bronze
// + steel + base) одинаковы что для наград, что для достижений.
function tierClass(tier: number | null): string {
  if (tier === 1) return "tier-gold";
  if (tier === 2) return "tier-silver";
  if (tier === 3) return "tier-bronze";
  if (tier === 4) return "tier-steel";
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
 * Title для тайла и popover'а. Для мест — title серии; для спец-наград —
 * type.title как есть.
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
 * Иконка тайла. Места в Art-конкурсе показываем palette (как тематичный
 * аналог trophy-cup). Остальное — type.iconName.
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
    <SecondaryText>Загрузка…</SecondaryText>
  </section>

  <section v-else-if="hasAwards" class="awards-section">
    <BlockTitle>Награды</BlockTitle>
    <div class="awards-grid">
      <Tooltip v-for="a in awards" :key="a.id">
        <div class="award" :class="tierClass(a.type.tier)">
          <div class="award-icon-wrap">
            <GameIcon :name="awardIcon(a)" class="award-icon" />
            <span
              v-if="a.contestSeries"
              class="award-year"
              aria-hidden="true"
              >{{ a.contestSeries.year }}</span
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
            </div>

            <p class="award-popover__desc">{{ a.type.description }}</p>

            <div
              v-if="a.workUrl || a.contestSeries?.topicUrl"
              class="award-popover__links"
            >
              <a
                v-if="a.workUrl"
                :href="a.workUrl"
                class="award-popover__link"
                target="_blank"
                rel="noopener"
                >Топик с работой</a
              >
              <a
                v-if="a.contestSeries?.topicUrl"
                :href="a.contestSeries.topicUrl"
                class="award-popover__link"
                target="_blank"
                rel="noopener"
                >Топик с итогами</a
              >
            </div>

            <div class="award-popover__footer">
              Получена {{ dayjs(a.awardedUtc).format("DD.MM.YYYY") }}
            </div>
          </div>
        </template>
      </Tooltip>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.awards-section
  display: flex
  flex-direction: column
  // Унифицировано с .achievements-section: явный gap между BlockTitle
  // и сеткой + обнуление intrinsic-margin'ов BlockTitle.
  gap: $small

  :deep(h2)
    margin: 0

.awards-grid
  display: flex
  flex-wrap: wrap
  gap: $medium
  align-items: flex-start

.award
  display: flex
  flex-direction: column
  align-items: center
  gap: $tiny
  width: 110px
  cursor: help
  color: $text

  .award-icon
    font-size: 72px

// Обертка иконки — relative-якорь для year-бейджа, чтобы badge
// привязывался к правому нижнему углу самой иконки, а не к тайлу.
// Аналогично .chain-icon-wrap в ProfileAchievementsSection.
.award-icon-wrap
  position: relative
  display: inline-block
  line-height: 0

// Contest year — pill badge in the bottom-right corner of the icon.
// Tier color is inherited from the icon's inherits-color in each
// tier class below (see .tier-gold .award-year etc).
.award-year
  position: absolute
  right: -4px
  bottom: -4px
  min-width: 30px
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
  color: #fff
  border: 2px solid $bg-page
  border-radius: $minor
  font-variant-numeric: tabular-nums
  text-shadow: 0 0 1px rgba(0, 0, 0, 0.4)

.award-title
  font-size: $secondary-font-size
  text-align: center
  line-height: 1.2

.tier-gold
  .award-icon
    color: #d4af37
  .award-year
    background-color: #d4af37
.tier-silver
  .award-icon
    color: #c0c0c0
  .award-year
    background-color: #c0c0c0
.tier-bronze
  .award-icon
    color: #cd7f32
  .award-year
    background-color: #cd7f32
.tier-steel
  .award-icon
    color: $text-meta
  .award-year
    background-color: $text-meta
.tier-base
  .award-icon
    color: $heading
  .award-year
    background-color: $heading

// --- Rich popover ---
.award-popover
  display: flex
  flex-direction: column
  gap: 6px
  min-width: 220px
  white-space: normal

  &__header
    display: flex
    align-items: baseline
    gap: $small

  &__title
    font-size: 14px
    color: $tooltip-text

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

  &__link
    color: inherit
    text-decoration: underline
    text-underline-offset: 2px

    &:hover
      opacity: 0.85

  &__footer
    font-size: 11px
    color: $tooltip-text
    opacity: 0.55
    margin-top: 4px
</style>
