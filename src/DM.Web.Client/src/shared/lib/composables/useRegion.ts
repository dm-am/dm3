import { ref, computed, onMounted } from "vue";
import { AccountApi } from "@/shared/api";

export interface RegionConfig {
  id: string;
  name: string;
  webUrl: string;
  isCurrent: boolean;
}

export function useRegion() {
  const mirrors = ref<RegionConfig[]>([]);
  const isHydrated = ref(false);
  const isTransferring = ref(false);
  const isLoading = ref(true);

  async function fetchMirrors() {
    try {
      const { data } = await AccountApi.getMirrors();
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

  async function switchRegion() {
    const target = alternateRegion.value;
    if (!target || isTransferring.value) return;

    isTransferring.value = true;
    const returnUrl = window.location.pathname + window.location.search;

    try {
      // Get transfer token with returnUrl (if authenticated)
      const { data } = await AccountApi.getTransferToken(target.id, returnUrl);

      if (data?.transferUrl) {
        // With session transfer (returnUrl is already included in transferUrl)
        window.location.href = data.transferUrl;
      } else {
        // Without auth — just redirect to same path
        window.location.href = target.webUrl + returnUrl;
      }
    } catch {
      // Fallback — redirect without session transfer
      window.location.href = target.webUrl + returnUrl;
    }
  }

  return {
    mirrors,
    currentRegion,
    alternateRegion,
    canSwitch,
    switchTooltip,
    switchRegion,
    isHydrated,
    isTransferring,
    isLoading,
  };
}
