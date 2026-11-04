import { ref, type Ref } from "vue";
import { achievementApi } from "../api";
import type {
  AwardType,
  ContestSeries,
} from "@/shared/api/models/achievements";

const awardTypes: Ref<AwardType[] | null> = ref(null);
const series: Ref<ContestSeries[] | null> = ref(null);
/** A first read is under way: nothing is shown yet. A reload raises no flag, the rows stay. */
const loading = ref(false);
/** The last read of the public slice was refused, so its rows, if any, are not the answer. */
const failed = ref(false);
let pending: Promise<void> | null = null;

// With inactive records, kept apart so the profile never renders a hidden one.
const adminAwardTypes: Ref<AwardType[] | null> = ref(null);
const adminSeries: Ref<ContestSeries[] | null> = ref(null);
const adminFailed = ref(false);
let adminPending: Promise<void> | null = null;

/** @param fresh Ask the origin, for a reload after an edit. */
async function load(fresh = false): Promise<void> {
  if (!fresh && awardTypes.value !== null && series.value !== null) return;
  if (pending !== null) return pending;
  pending = (async () => {
    const shown = awardTypes.value !== null && series.value !== null;
    if (!shown) loading.value = true;
    try {
      const [a, s] = await Promise.all([
        achievementApi.getAwardTypes(fresh),
        achievementApi.getContestSeries(fresh),
      ]);
      failed.value = Boolean(a.error || s.error);
      if (failed.value) return;
      awardTypes.value = a.data?.resources ?? [];
      series.value = s.data?.resources ?? [];
    } finally {
      loading.value = false;
      pending = null;
    }
  })();
  return pending;
}

/** The admin catalogs, inactive included. Lazy like load(). */
async function loadAdmin(fresh = false): Promise<void> {
  if (!fresh && adminAwardTypes.value !== null && adminSeries.value !== null)
    return;
  if (adminPending !== null) return adminPending;
  adminPending = (async () => {
    const shown = adminAwardTypes.value !== null && adminSeries.value !== null;
    if (!shown) loading.value = true;
    try {
      const [a, s] = await Promise.all([
        achievementApi.getModerationAwardTypes(),
        achievementApi.getModerationContestSeries(),
      ]);
      adminFailed.value = Boolean(a.error || s.error);
      if (adminFailed.value) return;
      adminAwardTypes.value = a.data?.resources ?? [];
      adminSeries.value = s.data?.resources ?? [];
    } finally {
      loading.value = false;
      adminPending = null;
    }
  })();
  return adminPending;
}

/**
 * Both slices go stale on an edit; the admin one reloads only if it was
 * loaded. The rows shown stay until the answer replaces them, and a read
 * already under way finishes first, so an older answer never lands last.
 */
async function reload(): Promise<void> {
  const wasAdmin = adminAwardTypes.value !== null || adminSeries.value !== null;
  await pending;
  await load(true);
  if (!wasAdmin) return;
  await adminPending;
  await loadAdmin(true);
}

export function useContestSeries() {
  return {
    awardTypes,
    series,
    adminAwardTypes,
    adminSeries,
    loading,
    failed,
    adminFailed,
    load,
    loadAdmin,
    reload,
  };
}
