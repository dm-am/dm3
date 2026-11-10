import { type MaybeRefOrGetter, onBeforeUnmount, ref, toValue } from "vue";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "./useExpandableRegistry";

/**
 * Expandable sections, one open at a time or `multiple`; each toggle syncs
 * with the registry of ScrollNav's "Развернуть все". `ids` may be reactive, so
 * the registry acts on the current items.
 */
export interface UseExpandableOptions {
  /** Allow multiple items to be expanded simultaneously. Default: false. */
  multiple?: boolean;
  /** The item ids, as an array, a ref, a computed or a getter. */
  ids: MaybeRefOrGetter<readonly string[]>;
}

export function useExpandable(options: UseExpandableOptions) {
  const { multiple = false, ids } = options;

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
    notifyExpandableChanged();
  }

  function expandAllItems() {
    const current = toValue(ids);
    if (current.length === 0) return;
    if (multiple) {
      expandedSet.value = new Set(current);
    } else {
      // Single-open: "expand all" opens the first, so something changes.
      expandedId.value = current[0] ?? null;
    }
    notifyExpandableChanged();
  }

  function collapseAllItems() {
    if (multiple) {
      expandedSet.value = new Set();
    } else {
      expandedId.value = null;
    }
    notifyExpandableChanged();
  }

  function allExpanded(): boolean {
    const current = toValue(ids);
    if (current.length === 0) return false;
    if (multiple) return current.every((id) => expandedSet.value.has(id));
    // Single-open has all of them open only when there is one.
    return current.length === 1 && expandedId.value === current[0];
  }

  const unregister = registerExpandable({
    id: Symbol("useExpandable"),
    isExpanded: allExpanded,
    expand: expandAllItems,
    collapse: collapseAllItems,
  });
  onBeforeUnmount(unregister);

  return { toggle, isExpanded };
}
