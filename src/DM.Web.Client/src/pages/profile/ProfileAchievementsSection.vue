<script setup lang="ts">
/**
 * ProfileAchievementsSection — компактный блок «Достижения».
 *
 * Архитектура:
 *  - Каталог тиров (`AchievementType`) уже привязан к категории
 *    (`AchievementType.category` — SSOT для iconName/metric/description/sortOrder).
 *    Группируем тиры по `category.id` → получаем цепочку.
 *  - Прогресс не хранится: вычисляется из user-метрик через
 *    `getMetricValue` (SSOT с backend `AchievementMetricResolver`).
 *  - Одна строка на цепочку, без дублирования category-заголовков
 *    и без отдельной карточки на каждый из 4 тиров: иконка + название
 *    активного тира + tier-точки + прогресс к следующему порогу.
 *  - Активный тир — следующий незаработанный (если есть) либо последний.
 *  - Tier-цвет в иконке и tier-точках отражает текущее достижение.
 *  - Rich popover (через `<Tooltip #content>`) показывает категорию,
 *    описание, прогресс «X / Y», и полную таблицу тиров с их порогами.
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
import { Tooltip } from "@/shared/ui/Tooltip";
import { getMetricValue } from "@/shared/lib/achievements/getMetricValue";
import {
  formatThreshold,
  metricDisplayNumber,
} from "@/shared/lib/achievements/formatThreshold";

const props = defineProps<{
  username: string;
  /** Профиль владельца — дает значения метрик для прогресса. */
  user: User;
}>();

const earned = ref<UserAchievement[]>([]);
const catalog = ref<AchievementType[]>([]);
const loading = ref(false);
const loaded = ref(false);

async function load(username: string) {
  loading.value = true;
  loaded.value = false;
  try {
    const [earnedRes, catalogRes] = await Promise.all([
      achievementApi.getUserAchievements(username),
      achievementApi.getAchievementTypes(),
    ]);
    earned.value = earnedRes.data?.resources ?? [];
    catalog.value = catalogRes.data?.resources ?? [];
  } finally {
    loading.value = false;
    loaded.value = true;
  }
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
  /** Прогресс в % (0-100, обрезан по threshold). */
  pct: number;
}

interface Chain {
  categoryId: string;
  sortOrder: number;
  iconName: string;
  title: string;
  description: string;
  tiers: ChainTier[];
  earnedCount: number;
  /** Тир, на который смотрим: первый незаработанный, либо последний. */
  active: ChainTier;
  /** True если все 4 тира заработаны — цепочка завершена. */
  completed: boolean;
  /** Номер высшего заработанного тира (1..4), 0 если ничего не получено. */
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
      const clamped = Math.min(Math.max(progress, 0), type.threshold);
      const pct =
        type.threshold > 0 ? Math.round((clamped / type.threshold) * 100) : 0;
      return { type, earnedUtc: got?.earnedUtc ?? null, progress, pct };
    });

    const earnedCount = tiers.filter((t) => t.earnedUtc).length;
    const completed = earnedCount === tiers.length && tiers.length > 0;
    // Активный тир — первый незаработанный (что показывает «куда расти»).
    // Если все заработано, берем последний — будет показан как completed.
    const active = tiers.find((t) => !t.earnedUtc) ?? tiers[tiers.length - 1];
    const maxEarnedTier = tiers.reduce(
      (m, t) =>
        t.earnedUtc && (t.type.tier ?? 0) > m ? (t.type.tier ?? 0) : m,
      0,
    );

    result.push({
      categoryId: category.id,
      sortOrder: category.sortOrder,
      iconName: category.iconName,
      title: category.title,
      description: category.description,
      tiers,
      earnedCount,
      active,
      completed,
      maxEarnedTier,
    });
  }
  result.sort((a, b) => a.sortOrder - b.sortOrder);
  return result;
});

const hasAnyChain = computed(() => chains.value.length > 0);

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

// Display-unit current / goal numbers for the active tier (days→years for
// registration, raw count otherwise). One SSOT keeps the tile, the popover and
// the aria-label in the same unit — never "4054 из 15 лет".
function displayCurrent(chain: Chain): number {
  return metricDisplayNumber(
    chain.active.type.category.metric,
    Math.min(chain.active.progress, chain.active.type.threshold),
  );
}
function displayGoal(chain: Chain): number {
  return metricDisplayNumber(
    chain.active.type.category.metric,
    chain.active.type.threshold,
  );
}
function displayTotal(chain: Chain): number {
  return metricDisplayNumber(
    chain.active.type.category.metric,
    chain.active.progress,
  );
}

// Progress toward the active tier as a single string ("197 из 500 лайков").
// Built in script (not interpolated across template text nodes) so the copied
// selection always has clean spaces — no whitespace-condense glue, no stray
// newlines. Unit label SSOT is formatThreshold.
function progressLabel(chain: Chain): string {
  return `${displayCurrent(chain)} из ${formatThreshold(
    chain.active.type.category.metric,
    chain.active.type.threshold,
  )}`;
}
</script>

<template>
  <section class="achievements-section">
    <BlockTitle>Достижения</BlockTitle>

    <SecondaryText v-if="loading && !loaded">Загрузка…</SecondaryText>

    <SecondaryText v-else-if="!hasAnyChain">
      Нет доступных достижений
    </SecondaryText>

    <div v-else class="chains">
      <Tooltip v-for="chain in chains" :key="chain.categoryId" placement="top">
        <div
          class="chain"
          :class="[
            tierClass(chain.maxEarnedTier),
            chain.completed
              ? 'chain--completed'
              : chain.earnedCount > 0
                ? 'chain--partial'
                : 'chain--locked',
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

          <span class="chain-title">{{ chain.active.type.title }}</span>

          <div
            class="chain-bar"
            role="progressbar"
            :aria-valuenow="chain.completed ? 100 : chain.active.pct"
            :aria-valuemin="0"
            :aria-valuemax="100"
            :aria-label="`${chain.title}: ${chain.completed ? 'завершено' : `${displayCurrent(chain)}/${displayGoal(chain)}`}`"
          >
            <div
              class="chain-bar-fill"
              :style="{
                width: chain.completed ? '100%' : chain.active.pct + '%',
              }"
            />
          </div>

          <!-- Цифры под баром: только числа без единиц (единицы понятны из
               иконки/названия). Display-unit (годы для регистрации) — те же
               числа, что и в popover. Real " / " text node so a copied
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
          <div class="chain-popover">
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
@import "src/assets/styles/Themes"

.achievements-section
  display: flex
  flex-direction: column
  gap: $small

  :deep(h2)
    margin: 0

// King's Bounty awards-style: чистая сетка иконок-наград + римская
// степень в углу иконки для полученных тиров. Tooltip — shared
// <Tooltip> с rich-content слотом (полная информация о цепочке).
// Trigger получает `display: flex` чтобы быть полноценной grid-ячейкой
// (`display: contents` ломал getBoundingClientRect и тултип уезжал
// в левый верхний угол).
.chains
  display: grid
  grid-template-columns: repeat(auto-fill, minmax(96px, 1fr))
  gap: $tiny

  :deep(.tooltip-trigger)
    display: flex

.chain
  position: relative
  display: flex
  flex-direction: column
  align-items: center
  justify-content: center
  // gap > сдвиг degree-badge (-4px вниз): иначе pill степени почти
  // касается прогресс-бара. 8px дает ~4px воздуха.
  gap: 8px
  padding: $minor
  border-radius: 4px
  width: 100%
  cursor: default

  // Locked: тот же $text-muted, но почти прозрачный — иконка
  // earned уже на muted, поэтому locked нужно сделать еще незаметнее,
  // иначе оба состояния сливаются.
  &.chain--locked .chain-icon
    color: $text-muted
    filter: none
    opacity: 0.22

  &.chain--completed .chain-bar-fill
    background-color: $progress-fill-overlay

// Обертка нужна как якорь для абсолютно позиционированной римской
// степени — иначе badge привязался бы к тайлу (а не к иконке) и
// уехал бы вправо от прогресс-бара. Размер обертки = размеру иконки
// (1em = 52px из font-size ниже).
.chain-icon-wrap
  position: relative
  display: inline-block
  line-height: 0

// Иконка — наш обычный muted-gray (#999), без tier-tint. Tier-уровень
// читается через цветной римский badge в углу.
.chain-icon
  font-size: 52px
  color: $text-muted
  line-height: 1
  filter: drop-shadow(0 1px 2px rgba(0, 0, 0, 0.12))

// Римская степень полученного тира — pill-badge в правом нижнем
// углу иконки. Tier-цвет фона, контрастный текст, тонкий border
// в цвет тайла (отделяет от иконки, чтобы не сливалось).
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
  color: #fff
  background-color: var(--card-tier-color, $heading)
  border: 2px solid $bg-page
  border-radius: $minor
  font-variant-numeric: tabular-nums
  text-shadow: 0 0 1px rgba(0, 0, 0, 0.4)

// Тематическое звание активного тира — над прогресс-баром,
// truncate'ится с ellipsis если узкий тайл (тайл ~107px, fit
// большинства имен).
.chain-title
  font-size: $secondary-font-size
  font-weight: 500
  color: $heading
  white-space: nowrap
  overflow: hidden
  text-overflow: ellipsis
  max-width: 100%
  text-align: center
  letter-spacing: 0.1px

// Цвета бара унифицированы с опросным баром (`ProgressBar.vue`):
// фон — $progress-bg-overlay, заливка — $progress-fill-overlay.
// Tier-цвет иконки и римской степени остается, чтобы выделить уровень,
// но сам бар читается как любая прогресс-шкала по сайту.
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

// Прогресс цифрами под баром: «2200 / 5000» — короткое количественное
// чтение поверх процентной шкалы. Только цифры (без единиц), tabular-nums
// чтобы столбец не «гулял». Цвет и кегль — те же, что у poll-type-indicator
// в Poll-виджете (низкоприоритетная мета на тайле).
.chain-progress
  font-size: $tertiary-font-size
  color: $text-muted
  font-variant-numeric: tabular-nums
  line-height: 1
  text-align: center

// Tier color tokens — оттенки наградного металла. Серебро намеренно
// светлее «холодного серого» locked-состояния (которое получает
// grayscale + brightness(0.4)) и tinted slight-blue, чтобы пара
// silver/locked различалась с одного взгляда. Платина — холоднее
// серебра (бирюзовый отлив), чтобы пара silver/platinum тоже не
// путалась.
.tier-bronze
  --card-tier-color: #cd7f32
.tier-silver
  --card-tier-color: #c8d0d8
.tier-gold
  --card-tier-color: #e8b923
.tier-platinum
  --card-tier-color: #7fcfd4
.tier-base
  --card-tier-color: #{$heading}

// --- Rich popover ---
// Слот `#content` в `<Tooltip>` принимает HTML; tooltip дает padding
// и max-width, остальное стилизуем здесь. white-space: pre-line
// у тултипа здесь не мешает — мы рендерим явные блоки.

.chain-popover
  display: flex
  flex-direction: column
  gap: 8px
  min-width: 220px
  white-space: normal

  &__header
    display: flex
    align-items: center
    gap: $small

  &__title
    flex: 1 1 auto
    font-size: 14px
    color: $tooltip-text

  &__badge
    flex: 0 0 auto
    padding: 2px 6px
    font-size: 10px
    font-weight: 600
    line-height: 1
    color: $tooltip-text
    background-color: var(--card-tier-color, $heading)
    border-radius: 999px
    text-transform: uppercase
    letter-spacing: 0.5px

    &--muted
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
    color: #fff
    background-color: var(--card-tier-color, $heading)
    border-radius: $minor
    padding: 2px 0
    line-height: 1

  &__tier-title
    overflow: hidden
    text-overflow: ellipsis
    white-space: nowrap

  &__tier-threshold
    font-variant-numeric: tabular-nums
    opacity: 0.75
    font-size: 11px
</style>
