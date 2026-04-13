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
 *
 * Persistent widgets (sidebar sections backed by localStorage) are NOT
 * registered — "expand all" only affects things that don't survive reload.
 *
 * Route navigation wipes the registry via router.afterEach(clearRegistry).
 */

import { computed, shallowRef, triggerRef } from "vue";

export interface ExpandableHandle {
  id: symbol;
  isExpanded: () => boolean;
  expand: () => void;
  collapse: () => void;
}

const handles = shallowRef<Map<symbol, ExpandableHandle>>(new Map());

function bump() {
  triggerRef(handles);
}

/**
 * Register a handle with the registry. Returns an unregister callback,
 * which MUST be called from onBeforeUnmount to avoid stale handles.
 */
export function registerExpandable(handle: ExpandableHandle): () => void {
  handles.value.set(handle.id, handle);
  bump();
  return () => {
    handles.value.delete(handle.id);
    bump();
  };
}

/**
 * Tell the registry that a participant's expand state changed externally
 * (e.g. user clicked the native accordion header). Keeps the global
 * allExpanded getter in sync.
 */
export function notifyExpandableChanged(): void {
  bump();
}

/** Clear the entire registry. Called by router.afterEach on navigation. */
export function clearRegistry(): void {
  handles.value.clear();
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
  for (const handle of handles.value.values()) {
    handle.expand();
  }
  bump();
}

export function collapseAll(): void {
  for (const handle of handles.value.values()) {
    handle.collapse();
  }
  bump();
}
