import { onBeforeUnmount, ref } from "vue";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "./useExpandableRegistry";

/**
 * Composable for expandable section state management.
 *
 * Supports two modes:
 *   - single-open (default): only one item can be expanded at a time.
 *     Opening another collapses the previous.
 *   - multi-open (`multiple: true`): any number of items can be open.
 *
 * When `register` is true (default), each `toggle()` call also syncs the
 * current expanded state with the global expandable registry (used by
 * ScrollNav's "Развернуть все / Свернуть все" button).
 *
 * Used in: ExpandableList (RulesPage penalties, RulesBans, RulesExternalLinks, RulesAuthors).
 */
export interface UseExpandableOptions {
  /** Allow multiple items to be expanded simultaneously. Default: false. */
  multiple?: boolean;
  /**
   * Register this composable instance with the global registry so it
   * participates in "expand all / collapse all". Default: true.
   */
  register?: boolean;
  /**
   * Static list of item ids this composable controls. Required when
   * `register: true` so expandAll() knows what to open.
   */
  ids?: readonly string[];
}

export function useExpandable(options: UseExpandableOptions = {}) {
  const { multiple = false, register = true, ids } = options;

  // Single-open mode stores the open id (or null).
  const expandedId = ref<string | null>(null);
  // Multi-open mode stores the open ids as a Set.
  const expandedSet = ref<Set<string>>(new Set());

  function isExpanded(id: string): boolean {
    return multiple ? expandedSet.value.has(id) : expandedId.value === id;
  }

  function toggle(id: string) {
    if (multiple) {
      const next = new Set(expandedSet.value);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      expandedSet.value = next;
    } else {
      expandedId.value = expandedId.value === id ? null : id;
    }
    if (register) notifyExpandableChanged();
  }

  function expandAllItems() {
    if (!ids || ids.length === 0) return;
    if (multiple) {
      expandedSet.value = new Set(ids);
    } else {
      // Single-open mode can only show one item at a time. "Expand all"
      // still opens at least one (the first) so the user sees *something*
      // change. Consumers wanting true multi-open should pass multiple: true.
      expandedId.value = ids[0] ?? null;
    }
    if (register) notifyExpandableChanged();
  }

  function collapseAllItems() {
    if (multiple) {
      expandedSet.value = new Set();
    } else {
      expandedId.value = null;
    }
    if (register) notifyExpandableChanged();
  }

  function allExpanded(): boolean {
    if (!ids || ids.length === 0) return false;
    if (multiple) {
      for (const id of ids) {
        if (!expandedSet.value.has(id)) return false;
      }
      return true;
    }
    // Single-open mode can never have "all" expanded if there's more
    // than one id. Treat as expanded only if the sole id is open.
    return ids.length === 1 && expandedId.value === ids[0];
  }

  if (register && ids && ids.length > 0) {
    const unregister = registerExpandable({
      id: Symbol("useExpandable"),
      isExpanded: allExpanded,
      expand: expandAllItems,
      collapse: collapseAllItems,
    });
    onBeforeUnmount(unregister);
  }

  return {
    // Exposed for templates.
    expanded: multiple ? expandedSet : expandedId,
    toggle,
    isExpanded,
    expandAllItems,
    collapseAllItems,
    allExpanded,
  };
}
