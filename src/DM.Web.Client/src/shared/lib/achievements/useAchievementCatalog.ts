import { ref, type Ref } from "vue";
import { achievementApi } from "@/shared/api";
import type {
  AchievementCategory,
  AchievementType,
} from "@/shared/api/models/achievements";

/**
 * Singleton catalog cache: загружается один раз и переиспользуется
 * между профилем и admin-страницами. Это admin-каталог из 13 категорий
 * + 52 тиров — данные стабильные, кешируем module-scope.
 *
 * Возвращает реактивные refs. `load()` идемпотентна: повторный вызов
 * во время загрузки или после успешной загрузки ничего не делает.
 * `reload()` форсит свежий fetch (для админских правок).
 */
const categories: Ref<AchievementCategory[] | null> = ref(null);
const types: Ref<AchievementType[] | null> = ref(null);
const loading = ref(false);
let pending: Promise<void> | null = null;

export function useAchievementCatalog() {
  async function load(): Promise<void> {
    if (categories.value !== null && types.value !== null) return;
    if (pending !== null) return pending;
    pending = (async () => {
      loading.value = true;
      try {
        const [c, t] = await Promise.all([
          achievementApi.getAchievementCategories(),
          achievementApi.getAchievementTypes(),
        ]);
        categories.value = c.data?.resources ?? [];
        types.value = t.data?.resources ?? [];
      } finally {
        loading.value = false;
        pending = null;
      }
    })();
    return pending;
  }

  async function reload(): Promise<void> {
    categories.value = null;
    types.value = null;
    pending = null;
    return load();
  }

  return { categories, types, loading, load, reload };
}
