/**
 * useExpandableRegistry - module-scoped singleton for coordinating
 * "expand all / collapse all" across every ephemeral expandable element
 * on the current page.
 *
 * Why a singleton (not provide/inject): the ScrollNav toggle button lives
 * in the root layout, outside any route-subtree's provide boundary.
 *
 * Scope — what participates:
 *   - <ExpandableList> accordion rows (via useExpandable)
 *   - <TruncatedContent> "Показать полностью" blocks
 *   - BBCode [spoiler] heads rendered via initSpoilers
 *   - BBCode [nsfw] heads rendered via initNsfw
 *   - Tiptap Spoiler/Nsfw node views (editor variants)
 *   - GamePost review collapse sections
 *
 * Persistent widgets (sidebar sections backed by localStorage) are NOT
 * registered — "expand all" only affects things that don't survive reload.
 *
 * Route navigation wipes the registry via router.afterEach(clearRegistry).
 *
 * Late registration: when a user clicks "Развернуть все", handles that
 * register AFTER the action (e.g. BBCode spoilers inside lazily-loaded
 * review text) are auto-expanded to maintain consistency.
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
 * Register a handle with the registry. Returns an unregister callback,
 * which MUST be called from onBeforeUnmount to avoid stale handles.
 *
 * If the user recently clicked "Развернуть все" / "Свернуть все",
 * the new handle is automatically expanded/collapsed to match.
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

/**
 * Tell the registry that a participant's expand state changed by a MANUAL
 * user toggle (spoiler head, accordion header, "показать полностью").
 * Clears the pending bulk action so future registrations don't auto-sync.
 */
export function notifyExpandableChanged(): void {
  pendingAction = null;
  bump();
}

/**
 * Re-evaluate the aggregate expand state (allExpanded) WITHOUT clearing the
 * pending bulk action. For settle events that fire for bulk-driven changes
 * too (e.g. TruncatedContent's transitionend) — clearing there would cancel
 * the bulk action's late-registration sync mid-animation.
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
  // Restore pendingAction — expand callbacks may have cleared it via
  // notifyExpandableChanged(), but we still want late-registering handles
  // (e.g. BBCode spoilers inside lazily-loaded review HTML) to auto-expand.
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
