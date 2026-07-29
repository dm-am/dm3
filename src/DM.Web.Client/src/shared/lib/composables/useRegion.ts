import { ref, computed, onMounted } from "vue";
import { accountApi } from "@/shared/api";

export interface RegionConfig {
  id: string;
  name: string;
  webUrl: string;
  isCurrent: boolean;
}

export function useRegion() {
  const mirrors = ref<RegionConfig[]>([]);
  const isHydrated = ref(false);
  const isSwitching = ref(false);
  const isLoading = ref(true);

  async function fetchMirrors() {
    try {
      const { data } = await accountApi.getMirrors();
      if (data?.mirrors) {
        mirrors.value = data.mirrors;
      }
    } catch {
      // Fallback: only current site
      mirrors.value = [
        {
          id: "main",
          name: "Основной сайт",
          webUrl: window.location.origin,
          isCurrent: true,
        },
      ];
    } finally {
      isLoading.value = false;
    }
  }

  onMounted(() => {
    isHydrated.value = true;
    fetchMirrors();
  });

  const currentRegion = computed(
    () => mirrors.value.find((m) => m.isCurrent) ?? mirrors.value[0],
  );

  const alternateRegion = computed(() =>
    mirrors.value.find((m) => !m.isCurrent),
  );

  // Can only switch if there's an alternate mirror available
  const canSwitch = computed(
    () => !isLoading.value && alternateRegion.value !== undefined,
  );

  const switchTooltip = computed(() => {
    if (isLoading.value) return "Загрузка...";
    if (!canSwitch.value) return "Зеркало недоступно";
    return `Перейти на ${alternateRegion.value?.name}`;
  });

  // The session does not travel with the visitor: each mirror authenticates its
  // own visitors. Switching keeps the path so the same page opens on the other
  // side, and the visitor signs in there if they need to be signed in.
  function switchRegion() {
    const target = alternateRegion.value;
    if (!target || isSwitching.value) return;

    isSwitching.value = true;
    window.location.href =
      target.webUrl + window.location.pathname + window.location.search;
  }

  return {
    mirrors,
    currentRegion,
    alternateRegion,
    canSwitch,
    switchTooltip,
    switchRegion,
    isHydrated,
    isSwitching,
    isLoading,
  };
}
