import { ref } from "vue";

/**
 * Composable for expandable section state management.
 * Used in: PenaltyTable, RulesExternalLinks, RulesBans, RulesMasters
 */
export function useExpandable() {
  const expanded = ref<string | null>(null);

  function toggle(id: string) {
    expanded.value = expanded.value === id ? null : id;
  }

  function isExpanded(id: string) {
    return expanded.value === id;
  }

  return {
    expanded,
    toggle,
    isExpanded,
  };
}
