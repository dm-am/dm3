import { ref, type Ref } from "vue";

/**
 * Configuration for keyboard navigation.
 */
export interface KeyboardNavigationOptions<T> {
  /** Items to navigate through */
  items: Ref<T[]>;
  /** Callback when item is selected */
  onSelect: (index: number, item: T) => void;
  /** Callback when Escape is pressed */
  onEscape?: () => void;
  /** Callback when Backspace is pressed (with empty input) */
  onBackspace?: () => void;
  /** Condition to skip an item (e.g., skip headers) */
  skipCondition?: (item: T) => boolean;
  /** Whether navigation is circular */
  circular?: boolean;
}

/**
 * useKeyboardNavigation - Unified keyboard navigation for dropdown items.
 *
 * Provides arrow key navigation, Enter selection, and Escape handling.
 *
 * @example
 * ```ts
 * const { highlightedIndex, handleKeydown, resetHighlight } = useKeyboardNavigation({
 *   items: computed(() => filteredOptions),
 *   onSelect: (index, item) => selectOption(item),
 *   onEscape: () => closeDropdown(),
 *   skipCondition: (item) => item.isHeader,
 * });
 * ```
 */
export function useKeyboardNavigation<T>(options: KeyboardNavigationOptions<T>) {
  const {
    items,
    onSelect,
    onEscape,
    onBackspace,
    skipCondition,
    circular = false,
  } = options;

  const highlightedIndex = ref(-1);

  /**
   * Find next valid index (skipping items that match skipCondition).
   */
  function findNextIndex(currentIndex: number, direction: 1 | -1): number {
    const itemsArray = items.value;
    const length = itemsArray.length;

    if (length === 0) return -1;

    let newIndex = currentIndex + direction;

    // Handle bounds
    if (circular) {
      if (newIndex < 0) newIndex = length - 1;
      if (newIndex >= length) newIndex = 0;
    } else {
      if (newIndex < 0) newIndex = 0;
      if (newIndex >= length) newIndex = length - 1;
    }

    // Skip items matching skipCondition
    let attempts = 0;
    while (
      skipCondition &&
      skipCondition(itemsArray[newIndex]) &&
      attempts < length
    ) {
      newIndex += direction;
      if (circular) {
        if (newIndex < 0) newIndex = length - 1;
        if (newIndex >= length) newIndex = 0;
      } else {
        if (newIndex < 0 || newIndex >= length) break;
      }
      attempts++;
    }

    // Validate final index
    if (newIndex < 0 || newIndex >= length) return currentIndex;
    if (skipCondition && skipCondition(itemsArray[newIndex])) return currentIndex;

    return newIndex;
  }

  /**
   * Handle keyboard events.
   */
  function handleKeydown(event: KeyboardEvent): boolean {
    const itemsArray = items.value;

    switch (event.key) {
      case "ArrowDown":
        event.preventDefault();
        if (highlightedIndex.value === -1) {
          // Start from first item
          highlightedIndex.value = findNextIndex(-1, 1);
        } else {
          highlightedIndex.value = findNextIndex(highlightedIndex.value, 1);
        }
        return true;

      case "ArrowUp":
        event.preventDefault();
        if (highlightedIndex.value === -1) {
          // Start from last item
          highlightedIndex.value = findNextIndex(itemsArray.length, -1);
        } else {
          highlightedIndex.value = findNextIndex(highlightedIndex.value, -1);
        }
        return true;

      case "Enter":
        if (highlightedIndex.value >= 0 && highlightedIndex.value < itemsArray.length) {
          event.preventDefault();
          onSelect(highlightedIndex.value, itemsArray[highlightedIndex.value]);
          return true;
        }
        return false;

      case "Escape":
        event.preventDefault();
        onEscape?.();
        return true;

      case "Backspace":
        // Only handle if onBackspace is provided
        if (onBackspace) {
          onBackspace();
          return true;
        }
        return false;

      default:
        return false;
    }
  }

  /**
   * Reset highlight to initial state.
   */
  function resetHighlight() {
    highlightedIndex.value = -1;
  }

  /**
   * Set highlight to specific index.
   */
  function setHighlight(index: number) {
    highlightedIndex.value = index;
  }

  return {
    highlightedIndex,
    handleKeydown,
    resetHighlight,
    setHighlight,
  };
}
