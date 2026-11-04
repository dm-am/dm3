import type {
  AchievementType,
  UserAchievement,
} from "@/shared/api/models/achievements";
import type { User } from "@/shared/api/models/community";
import { formatThreshold, metricDisplayNumber } from "./formatThreshold";
import { getMetricValue } from "./getMetricValue";

/** One tier of a chain, with when it was earned and the owner's progress. */
interface AchievementChainTier {
  type: AchievementType;
  /** Place in the chain by threshold, from 1: the numeral of the tier. */
  position: number;
  earnedUtc: string | null;
  progress: number;
}

/** The tiers of one category, as a profile shows them: one tile and one popover. */
export interface AchievementChain {
  categoryId: string;
  sortOrder: number;
  iconName: string;
  /** Category title — the popover header. */
  title: string;
  /** Category metric description (what exactly is counted). */
  description: string;
  /** Every tier of the chain, threshold-sorted — the popover table. */
  tiers: AchievementChainTier[];
  earnedCount: number;
  /** The senior earned tier, the face of the tile; null for a locked chain. */
  earned: AchievementChainTier | null;
  /** First unearned tier (the progress goal); null — all earned. */
  next: AchievementChainTier | null;
  /** True when every tier of the chain is earned. */
  completed: boolean;
  /** True when no tier is earned — the tile renders in locked style. */
  locked: boolean;
  /** The tier of the senior earned one, which picks the metal; null for a locked chain. */
  earnedTier: number | null;
  /** The place of the senior earned one in the chain (0 for a locked chain), its numeral. */
  earnedPosition: number;
}

/** Every chain of the catalog in its order; a chain with nothing earned is locked. */
export function achievementChains(
  catalog: AchievementType[],
  earned: UserAchievement[],
  user: User,
): AchievementChain[] {
  const earnedByTypeId = new Map<string, UserAchievement>();
  for (const a of earned) earnedByTypeId.set(a.type.id, a);

  const grouped = new Map<string, AchievementType[]>();
  for (const t of catalog) {
    const list = grouped.get(t.category.id) ?? [];
    list.push(t);
    grouped.set(t.category.id, list);
  }

  const result: AchievementChain[] = [];
  for (const types of grouped.values()) {
    types.sort((a, b) => a.threshold - b.threshold);

    const category = types[0].category;
    const progress = getMetricValue(category.metric, user);
    const tiers: AchievementChainTier[] = types.map((type, index) => {
      const got = earnedByTypeId.get(type.id);
      return {
        type,
        position: index + 1,
        earnedUtc: got?.earnedUtc ?? null,
        progress,
      };
    });

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
      earnedTier: earnedTop?.type.tier ?? null,
      earnedPosition: earnedTop?.position ?? 0,
    });
  }
  result.sort((a, b) => a.sortOrder - b.sortOrder);
  return result;
}

// A locked chain is captioned with tier I, the first one to reach.
export function chainTitle(chain: AchievementChain): string {
  return chain.earned ? chain.earned.type.title : chain.tiers[0].type.title;
}

const clamped = (tier: AchievementChainTier) =>
  Math.min(Math.max(tier.progress, 0), tier.type.threshold);

const ROMAN = ["", "I", "II", "III", "IV"];

/** The roman numeral of a place in a chain, "" for none. */
export function tierNumeral(n: number): string {
  return ROMAN[n] ?? "";
}

// In the metric's display unit, so the tile never reads "4054 из 15 лет".
export function chainCurrent(chain: AchievementChain): number {
  const next = chain.next;
  if (!next) return 0;
  return metricDisplayNumber(next.type.category.metric, clamped(next));
}

export function chainGoal(chain: AchievementChain): number {
  const next = chain.next;
  if (!next) return 0;
  return metricDisplayNumber(next.type.category.metric, next.type.threshold);
}

export function chainTotal(chain: AchievementChain): number {
  // Only reachable for completed chains (chain.earned is set there).
  if (!chain.earned) return 0;
  return metricDisplayNumber(
    chain.earned.type.category.metric,
    chain.earned.progress,
  );
}

// Progress toward the next tier as a percent (0-100); a completed chain
// reads as a full bar.
export function chainPercent(chain: AchievementChain): number {
  const next = chain.next;
  if (!next || next.type.threshold <= 0) return 100;
  return Math.round((clamped(next) / next.type.threshold) * 100);
}

// One string rather than template text nodes, so a copied selection has clean spaces.
export function chainProgressLabel(chain: AchievementChain): string {
  const next = chain.next;
  if (!next) return "";
  return `${chainCurrent(chain)} из ${formatThreshold(
    next.type.category.metric,
    next.type.threshold,
  )}`;
}
