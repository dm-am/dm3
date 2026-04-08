// API and data fetching
export { useApiResource, useApiList } from "./useApiResource";
export type {
  UseApiResourceOptions,
  UseApiResourceReturn,
} from "./useApiResource";

// Async operations
export { useAsyncAction } from "./useAsyncAction";
export type { AsyncActionState } from "./useAsyncAction";

// UI state
export { useExpandable } from "./useExpandable";
export { useScrollToElement } from "./useScrollToElement";

// Data fetching patterns
export { useFetchData } from "./useFetchData";

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

export { useUsernameValidation } from "./useUsernameValidation";
export type { UseUsernameValidationOptions } from "./useUsernameValidation";

export { useNewPasswordField } from "./useNewPasswordField";
export type { UseNewPasswordFieldOptions } from "./useNewPasswordField";

// User management
export { useModeratedProfile } from "./useModeratedProfile";
export { useProfileEdit } from "./useProfileEdit";

// Region and mirrors
export { useRegion } from "./useRegion";
export type { RegionConfig } from "./useRegion";

// Paging preferences
export { usePaging } from "./usePaging";

// Filter dispatcher (for URL-synced filters)
export { createFilterDispatcher } from "./createFilterDispatcher";
export type { FilterDispatcherConfig, FilterDispatcher } from "./createFilterDispatcher";

// Dropdown keyboard navigation
export { useDropdownKeyboard } from "./useDropdownKeyboard";
export type {
  UseDropdownKeyboardOptions,
  UseDropdownKeyboardReturn,
} from "./useDropdownKeyboard";

// Filter composables
export { useFilterSearch } from "./useFilterSearch";
export { useFilterDropdown } from "./useFilterDropdown";
export type { FilterNavPath } from "./useFilterDropdown";

// Real-time communication
export { useSignalR, useGlobalSignalR } from "./useSignalR";

// Notifications
export { useToast } from "./useToast";
export type { ToastType, Toast } from "./useToast";

// Content truncation (expand/collapse)
export { useContentTruncation } from "./useContentTruncation";
export type {
  ContentTruncationOptions,
  ContentTruncationReturn,
} from "./useContentTruncation";
