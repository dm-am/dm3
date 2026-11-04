import { ref } from "vue";

/** Where a hierarchical filter dropdown stands: filter, group, games' sub-options. */
export interface FilterNavPath {
  filter: string;
  group?: string;
  /** Status sub-option (e.g., Active, Closed) - for games filter */
  status?: string;
  /** Recruitment sub-option - for games filter */
  recruitment?: boolean;
}

export function useFilterDropdown() {
  const showDropdown = ref(false);
  const navPath = ref<FilterNavPath | null>(null);

  function closeDropdown() {
    showDropdown.value = false;
    navPath.value = null;
  }

  function toggleDropdown() {
    if (showDropdown.value) {
      closeDropdown();
    } else {
      showDropdown.value = true;
    }
  }

  /** From a group to its filter, from a filter to the root. */
  function navigateBack() {
    if (!navPath.value) return;

    if (navPath.value.group) {
      navPath.value = { filter: navPath.value.filter };
      return;
    }

    navPath.value = null;
  }

  return {
    showDropdown,
    navPath,
    closeDropdown,
    toggleDropdown,
    navigateBack,
  };
}
