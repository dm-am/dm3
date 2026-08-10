// API and data fetching
export { useApiResource, useApiList } from "./useApiResource";
export type {
  UseApiResourceOptions,
  UseApiResourceReturn,
} from "./useApiResource";

// One local request with its own loading/error and a race guard — the shape a
// screen uses when its answer is not worth a store.
export { useGuardedRequest } from "./useGuardedRequest";
export type {
  UseGuardedRequestOptions,
  UseGuardedRequestReturn,
} from "./useGuardedRequest";

// Async operations
export { useAsyncAction } from "./useAsyncAction";
export type { AsyncActionState } from "./useAsyncAction";

// UI state
export { useExpandable } from "./useExpandable";
export { useScrollToElement } from "./useScrollToElement";

// Global registry coordinating "expand/collapse all" across the page.
// Participants: ExpandableList rows, TruncatedContent, BBCode spoiler/nsfw.
export {
  registerExpandable,
  notifyExpandableChanged,
  refreshExpandableStates,
  clearRegistry,
  hasAny as hasAnyExpandable,
  allExpanded as allExpandablesExpanded,
  expandAll as expandAllExpandables,
  collapseAll as collapseAllExpandables,
} from "./useExpandableRegistry";
export type { ExpandableHandle } from "./useExpandableRegistry";

// Data fetching patterns
export { useFetchData } from "./useFetchData";

// Per-viewer data: the one place that knows what "the viewer changed" means
export { useViewerChange } from "./useViewerChange";

// Authentication and validation
export { useHibpCheck } from "./useHibpCheck";
export type { HibpCheckOptions } from "./useHibpCheck";

export { useValidatedField, validators } from "./useValidatedField";
export type {
  ValidationResult,
  SyncValidator,
  AsyncValidator,
  UseValidatedFieldOptions,
  ValidatedField,
} from "./useValidatedField";

export { useNewPasswordField } from "./useNewPasswordField";
export type { UseNewPasswordFieldOptions } from "./useNewPasswordField";

// Paging preferences
export { usePaging } from "./usePaging";

// Filter dispatcher (for URL-synced filters)
export { createFilterDispatcher } from "./createFilterDispatcher";
export type {
  FilterDispatcherConfig,
  FilterDispatcher,
} from "./createFilterDispatcher";

// Filter composables
export { useFilterSearch } from "./useFilterSearch";
export { useFilterDropdown } from "./useFilterDropdown";
export type { FilterNavPath } from "./useFilterDropdown";

// Real-time communication
export { useGlobalSignalR } from "./useSignalR";

// Notifications
export { useToast } from "./useToast";
export type { ToastType, Toast } from "./useToast";

// Document title (per-route + dynamic page titles)
export {
  useDocumentTitle,
  formatDocumentTitle,
  joinTitleSegments,
  TITLE_SEPARATOR,
} from "./useDocumentTitle";

// Expandable content section — THE building block: unified reveal animation
// + "Развернуть/Свернуть все" registry + manual-toggle semantics in one call.
export { useExpandableSection } from "./useExpandableSection";

// Smooth height animation for content-swap expand/collapse (the low-level
// half of useExpandableSection; use the section composable in components).
export { useAnimatedHeightToggle } from "./useAnimatedHeightToggle";

// Content truncation (expand/collapse)
export { useContentTruncation } from "./useContentTruncation";
export type {
  ContentTruncationOptions,
  ContentTruncationReturn,
} from "./useContentTruncation";

// FLIP reorder animation (active-first strips: Tabs, BoardNavigation)
export { useFlipReorder } from "./useFlipReorder";
export type { FlipReorderOptions } from "./useFlipReorder";

// Virtual scroll (@tanstack/vue-virtual wrapper)
export { useVirtualScroll } from "./useVirtualScroll";
export type {
  VirtualScrollOptions,
  VirtualScrollReturn,
} from "./useVirtualScroll";
