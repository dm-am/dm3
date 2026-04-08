import { ref, type Ref, type ComputedRef } from "vue";

export interface UseDropdownKeyboardOptions<T> {
  /** Reactive array of items to navigate */
  items: Ref<T[]> | ComputedRef<T[]>;
  /** Callback when item is selected via Enter */
  onSelect: (item: T) => void;
  /** Callback when dropdown should close (Escape) */
  onClose: () => void;
  /** Optional predicate to skip certain items (e.g., headers) */
  skipPredicate?: (item: T) => boolean;
}

export interface UseDropdownKeyboardReturn {
  /** Currently highlighted index (-1 = none) */
  highlightedIndex: Ref<number>;
  /** Keydown handler to attach to input/container */
  handleKeydown: (event: KeyboardEvent) => void;
  /** Reset highlighted index to -1 */
  resetHighlight: () => void;
  /** Highlight specific index */
  setHighlight: (index: number) => void;
}

/**
 * Composable for unified keyboard navigation in dropdowns.
 *
 * Supports:
 * - ArrowDown: Move to next item (skipping items matching skipPredicate)
 * - ArrowUp: Move to previous item (skipping items matching skipPredicate)
 * - Enter: Select highlighted item
 * - Escape: Close dropdown
 *
 * @example
 * ```ts
 * const { highlightedIndex, handleKeydown } = useDropdownKeyboard({
 *   items: suggestions,
 *   onSelect: (user) => selectUser(user),
 *   onClose: () => showDropdown.value = false,
 *   skipPredicate: (item) => item.isHeader,
 * });
 * ```
 */
export function useDropdownKeyboard<T>(
  options: UseDropdownKeyboardOptions<T>
): UseDropdownKeyboardReturn {
  const { items, onSelect, onClose, skipPredicate } = options;
  const highlightedIndex = ref(-1);

  function findNextIndex(currentIndex: number, direction: 1 | -1): number {
    const itemsArray = items.value;
    if (itemsArray.length === 0) return -1;

    let nextIndex = currentIndex + direction;

    // Wrap around or clamp
    if (nextIndex < 0) {
      nextIndex = 0;
    } else if (nextIndex >= itemsArray.length) {
      nextIndex = itemsArray.length - 1;
    }

    // Skip items matching predicate
    if (skipPredicate) {
      while (
        nextIndex >= 0 &&
        nextIndex < itemsArray.length &&
        skipPredicate(itemsArray[nextIndex])
      ) {
        nextIndex += direction;
      }

      // If we went out of bounds, return current or -1
      if (nextIndex < 0 || nextIndex >= itemsArray.length) {
        return currentIndex >= 0 ? currentIndex : -1;
      }
    }

    return nextIndex;
  }

  function handleKeydown(event: KeyboardEvent) {
    const itemsArray = items.value;
    if (itemsArray.length === 0 && event.key !== "Escape") {
      return;
    }

    switch (event.key) {
      case "ArrowDown":
        event.preventDefault();
        highlightedIndex.value = findNextIndex(highlightedIndex.value, 1);
        break;

      case "ArrowUp":
        event.preventDefault();
        highlightedIndex.value = findNextIndex(highlightedIndex.value, -1);
        break;

      case "Enter":
        event.preventDefault();
        if (
          highlightedIndex.value >= 0 &&
          highlightedIndex.value < itemsArray.length
        ) {
          const item = itemsArray[highlightedIndex.value];
          if (!skipPredicate || !skipPredicate(item)) {
            onSelect(item);
          }
        }
        break;

      case "Escape":
        event.preventDefault();
        highlightedIndex.value = -1;
        onClose();
        break;
    }
  }

  function resetHighlight() {
    highlightedIndex.value = -1;
  }

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
