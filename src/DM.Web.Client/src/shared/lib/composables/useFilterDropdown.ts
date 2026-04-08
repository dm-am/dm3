import { ref, computed } from "vue";

/**
 * Navigation path for hierarchical filter dropdowns.
 * Supports up to 4 levels of navigation:
 * - filter: top-level filter (status, host, dates, tag, etc.)
 * - group: sub-group (tag category, date type, etc.)
 * - status: status sub-option (Active, Closed) - used by games filter
 * - recruitment: recruitment sub-option - used by games filter
 */
export interface FilterNavPath {
  filter: string;
  group?: string;
  /** Status sub-option (e.g., Active, Closed) - for games filter */
  status?: string;
  /** Recruitment sub-option - for games filter */
  recruitment?: boolean;
}

/**
 * Composable for filter dropdown state management.
 * Handles:
 * - Dropdown open/close state
 * - Hierarchical navigation (filter -> group)
 * - Back navigation
 */
export function useFilterDropdown() {
  const showDropdown = ref(false);
  const navPath = ref<FilterNavPath | null>(null);

  const isOpen = computed(() => showDropdown.value);
  const currentFilter = computed(() => navPath.value?.filter ?? null);
  const currentGroup = computed(() => navPath.value?.group ?? null);

  /**
   * Open dropdown, optionally navigating to a specific filter
   */
  function openDropdown(filter?: string, group?: string) {
    showDropdown.value = true;
    if (filter) {
      navPath.value = { filter, group };
    }
  }

  /**
   * Close dropdown and reset navigation
   */
  function closeDropdown() {
    showDropdown.value = false;
    navPath.value = null;
  }

  /**
   * Toggle dropdown state
   */
  function toggleDropdown() {
    if (showDropdown.value) {
      closeDropdown();
    } else {
      showDropdown.value = true;
    }
  }

  /**
   * Navigate to a filter level
   */
  function navigateTo(filter: string, group?: string) {
    navPath.value = { filter, group };
  }

  /**
   * Navigate to a sub-group within current filter
   */
  function navigateToGroup(group: string) {
    if (navPath.value) {
      navPath.value = { filter: navPath.value.filter, group };
    }
  }

  /**
   * Navigate back one level
   */
  function navigateBack() {
    if (!navPath.value) return;

    // If at group level, go back to filter level
    if (navPath.value.group) {
      navPath.value = { filter: navPath.value.filter };
      return;
    }

    // Otherwise go to root
    navPath.value = null;
  }

  /**
   * Reset navigation and close dropdown
   */
  function resetAndClose() {
    navPath.value = null;
    closeDropdown();
  }

  return {
    showDropdown,
    navPath,
    isOpen,
    currentFilter,
    currentGroup,
    openDropdown,
    closeDropdown,
    toggleDropdown,
    navigateTo,
    navigateToGroup,
    navigateBack,
    resetAndClose,
  };
}
