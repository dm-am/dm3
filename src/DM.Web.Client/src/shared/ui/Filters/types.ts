/**
 * Shared types for unified filter components.
 */

// =============================================================================
// DROPDOWN ITEM TYPES
// =============================================================================

/**
 * Props for a single dropdown item.
 */
export interface DropdownItemProps {
  /** Display label */
  label: string;
  /** Optional hint/description shown below label */
  hint?: string;
  /** Optional avatar URL */
  avatarUrl?: string;
  /** Whether this item is highlighted (keyboard navigation) */
  highlighted?: boolean;
  /** Whether to indent this item (for hierarchy) */
  indent?: boolean;
  /** Whether this item has sub-options (shows arrow) */
  hasSubOptions?: boolean;
}

// =============================================================================
// DATE RANGE TYPES
// =============================================================================

/**
 * Props for DateRangePicker component.
 */
export interface DateRangePickerProps {
  /** From date value (YYYY-MM-DD format) */
  fromValue: string | null;
  /** To date value (YYYY-MM-DD format) */
  toValue: string | null;
  /** Label for "from" input */
  fromLabel?: string;
  /** Label for "to" input */
  toLabel?: string;
  /** Show clear button when values exist */
  showClearButton?: boolean;
}

// =============================================================================
// NUMERIC RANGE TYPES
// =============================================================================

/**
 * Props for NumericRangePicker component.
 */
export interface NumericRangePickerProps {
  /** Minimum value */
  minValue: number | null;
  /** Maximum value */
  maxValue: number | null;
  /** Label for min input */
  minLabel?: string;
  /** Label for max input */
  maxLabel?: string;
  /** Placeholder for min input */
  minPlaceholder?: string;
  /** Placeholder for max input */
  maxPlaceholder?: string;
  /** Allow negative numbers */
  allowNegative?: boolean;
}

// =============================================================================
// BUBBLE TYPES
// =============================================================================

/**
 * Value item for expandable bubble.
 */
export interface BubbleValue {
  /** Unique identifier */
  id: string;
  /** Display label */
  label: string;
}

/**
 * Props for FilterBubble component.
 */
export interface FilterBubbleProps {
  /** Prefix text (e.g., "Статус:") */
  prefix?: string;
  /** Display value */
  value: string;
}

/**
 * Props for ExpandableBubble component.
 */
export interface ExpandableBubbleProps {
  /** Prefix text (e.g., "Авторы:") */
  prefix: string;
  /** All values */
  values: BubbleValue[];
  /** Maximum visible values before collapse */
  maxVisible?: number;
}

// =============================================================================
// SORT TYPES
// =============================================================================

/**
 * Sort option definition.
 */
export interface SortOption {
  /** Value used in URL/API */
  value: string;
  /** Display label */
  label: string;
  /** Optional hint/description */
  hint?: string;
  /** Default sort direction for this option */
  defaultDirection?: "asc" | "desc";
}

/**
 * Props for SortButton component.
 */
export interface SortButtonProps {
  /** Available sort options */
  options: SortOption[];
  /** Current sort field */
  sortBy: string;
  /** Current sort direction */
  sortOrder: "asc" | "desc";
}

// =============================================================================
// OPTIONS LIST TYPES
// =============================================================================

/**
 * Single option for OptionsList.
 */
export interface ListOption {
  /** Value */
  value: string;
  /** Display label */
  label: string;
  /** Optional hint */
  hint?: string;
  /** Whether this option has sub-options */
  hasSubOptions?: boolean;
}

/**
 * Props for OptionsList component.
 */
export interface OptionsListProps {
  /** Available options */
  options: ListOption[];
  /** Currently highlighted index */
  highlightedIndex?: number;
  /** Filter query for filtering options */
  filterQuery?: string;
}
