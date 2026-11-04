/**
 * A module-scoped registry for "expand all / collapse all" of the page: the
 * ScrollNav toggle lives in the root layout, outside any provide. Only what
 * does not survive a reload takes part; the router clears it on navigation, and
 * a handle registered after a bulk action is synced to it.
 */

import { computed, shallowRef, triggerRef } from "vue";

export interface ExpandableHandle {
  id: symbol;
  isExpanded: () => boolean;
  expand: () => void;
  collapse: () => void;
}

const handles = shallowRef<Map<symbol, ExpandableHandle>>(new Map());

// Tracks the last bulk action so late-registering handles can match it.
// Reset on manual user toggle (notifyExpandableChanged) or navigation (clearRegistry).
let pendingAction: "expand" | "collapse" | null = null;

function bump() {
  triggerRef(handles);
}

/**
 * Returns the unregister callback, which must run in onBeforeUnmount; a handle
 * registered after a bulk action is synced to it.
 */
export function registerExpandable(handle: ExpandableHandle): () => void {
  handles.value.set(handle.id, handle);

  // Auto-sync late arrivals with the last bulk action
  if (pendingAction === "expand" && !handle.isExpanded()) {
    handle.expand();
  } else if (pendingAction === "collapse" && handle.isExpanded()) {
    handle.collapse();
  }

  bump();
  return () => {
    handles.value.delete(handle.id);
    bump();
  };
}

/** A manual toggle: clears the pending bulk action. */
export function notifyExpandableChanged(): void {
  pendingAction = null;
  bump();
}

/**
 * Re-evaluates allExpanded without clearing the pending bulk action, for
 * settle events that bulk changes fire too.
 */
export function refreshExpandableStates(): void {
  bump();
}

/** Clear the entire registry. Called by router.afterEach on navigation. */
export function clearRegistry(): void {
  handles.value.clear();
  pendingAction = null;
  bump();
}

export const hasAny = computed(() => handles.value.size > 0);

export const allExpanded = computed(() => {
  if (handles.value.size === 0) return false;
  for (const handle of handles.value.values()) {
    if (!handle.isExpanded()) return false;
  }
  return true;
});

export function expandAll(): void {
  pendingAction = "expand";
  // Snapshot handles: expand() callbacks may trigger notifyExpandableChanged()
  // or re-registrations that modify the Map during iteration.
  const snapshot = [...handles.value.values()];
  for (const handle of snapshot) {
    handle.expand();
  }
  // Restored: expand callbacks may have cleared it, and late handles still sync.
  pendingAction = "expand";
  bump();
}

export function collapseAll(): void {
  pendingAction = "collapse";
  const snapshot = [...handles.value.values()];
  for (const handle of snapshot) {
    handle.collapse();
  }
  pendingAction = "collapse";
  bump();
}
