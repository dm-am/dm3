import { type MaybeRefOrGetter, onBeforeUnmount, ref, toValue } from "vue";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "./useExpandableRegistry";

/**
 * Expandable sections, one open at a time or `multiple`; with `register` each
 * toggle syncs with the registry of ScrollNav's "Развернуть все". `ids` may be
 * reactive, so the registry acts on the current items.
 */
export interface UseExpandableOptions {
  /** Allow multiple items to be expanded simultaneously. Default: false. */
  multiple?: boolean;
  /** Takes part in "expand all / collapse all". Default: true. */
  register?: boolean;
  /** The item ids, as an array, a ref, a computed or a getter. */
  ids?: MaybeRefOrGetter<readonly string[]>;
}

export function useExpandable(options: UseExpandableOptions = {}) {
  const { multiple = false, register = true, ids } = options;

  // Single-open mode stores the open id (or null).
  const expandedId = ref<string | null>(null);
  // Multi-open mode stores the open ids as a Set.
  const expandedSet = ref<Set<string>>(new Set());

  function currentIds(): readonly string[] {
    return ids ? toValue(ids) : [];
  }

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
    const current = currentIds();
    if (current.length === 0) return;
    if (multiple) {
      expandedSet.value = new Set(current);
    } else {
      // Single-open: "expand all" opens the first, so something changes.
      expandedId.value = current[0] ?? null;
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
    const current = currentIds();
    if (current.length === 0) return false;
    if (multiple) {
      for (const id of current) {
        if (!expandedSet.value.has(id)) return false;
      }
      return true;
    }
    // Single-open mode can never have "all" expanded if there's more
    // than one id. Treat as expanded only if the sole id is open.
    return current.length === 1 && expandedId.value === current[0];
  }

  if (register) {
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
