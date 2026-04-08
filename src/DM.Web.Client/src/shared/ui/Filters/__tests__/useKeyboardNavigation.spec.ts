/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi } from "vitest";
import { ref } from "vue";
import { useKeyboardNavigation } from "../composables/useKeyboardNavigation";

describe("useKeyboardNavigation", () => {
  const createMockEvent = (key: string): KeyboardEvent => {
    return {
      key,
      preventDefault: vi.fn(),
    } as unknown as KeyboardEvent;
  };

  // ============================================================================
  // ARROW NAVIGATION
  // ============================================================================

  describe("Arrow Navigation", () => {
    it("moves down on ArrowDown", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { highlightedIndex, handleKeydown } = useKeyboardNavigation({
        items,
        onSelect,
      });

      handleKeydown(createMockEvent("ArrowDown"));
      expect(highlightedIndex.value).toBe(0);

      handleKeydown(createMockEvent("ArrowDown"));
      expect(highlightedIndex.value).toBe(1);
    });

    it("moves up on ArrowUp", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { highlightedIndex, handleKeydown, setHighlight } = useKeyboardNavigation({
        items,
        onSelect,
      });

      setHighlight(2);

      handleKeydown(createMockEvent("ArrowUp"));
      expect(highlightedIndex.value).toBe(1);

      handleKeydown(createMockEvent("ArrowUp"));
      expect(highlightedIndex.value).toBe(0);
    });

    it("stops at bounds when not circular", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { highlightedIndex, handleKeydown, setHighlight } = useKeyboardNavigation({
        items,
        onSelect,
        circular: false,
      });

      // At start, can't go up
      setHighlight(0);
      handleKeydown(createMockEvent("ArrowUp"));
      expect(highlightedIndex.value).toBe(0);

      // At end, can't go down
      setHighlight(2);
      handleKeydown(createMockEvent("ArrowDown"));
      expect(highlightedIndex.value).toBe(2);
    });

    it("wraps around when circular", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { highlightedIndex, handleKeydown, setHighlight } = useKeyboardNavigation({
        items,
        onSelect,
        circular: true,
      });

      // At end, wraps to start
      setHighlight(2);
      handleKeydown(createMockEvent("ArrowDown"));
      expect(highlightedIndex.value).toBe(0);

      // At start, wraps to end
      setHighlight(0);
      handleKeydown(createMockEvent("ArrowUp"));
      expect(highlightedIndex.value).toBe(2);
    });

    it("starts from beginning on ArrowDown when not highlighted", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { highlightedIndex, handleKeydown } = useKeyboardNavigation({
        items,
        onSelect,
      });

      expect(highlightedIndex.value).toBe(-1);

      handleKeydown(createMockEvent("ArrowDown"));
      expect(highlightedIndex.value).toBe(0);
    });

    it("starts from end on ArrowUp when not highlighted", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { highlightedIndex, handleKeydown } = useKeyboardNavigation({
        items,
        onSelect,
      });

      expect(highlightedIndex.value).toBe(-1);

      handleKeydown(createMockEvent("ArrowUp"));
      expect(highlightedIndex.value).toBe(2);
    });
  });

  // ============================================================================
  // SKIP CONDITION
  // ============================================================================

  describe("Skip Condition", () => {
    it("skips items matching skipCondition", () => {
      const items = ref([
        { value: "a", isHeader: false },
        { value: "header", isHeader: true },
        { value: "b", isHeader: false },
      ]);
      const onSelect = vi.fn();

      const { highlightedIndex, handleKeydown } = useKeyboardNavigation({
        items,
        onSelect,
        skipCondition: (item) => item.isHeader,
      });

      // First item
      handleKeydown(createMockEvent("ArrowDown"));
      expect(highlightedIndex.value).toBe(0);

      // Skip header, go to third item
      handleKeydown(createMockEvent("ArrowDown"));
      expect(highlightedIndex.value).toBe(2);
    });
  });

  // ============================================================================
  // ENTER KEY
  // ============================================================================

  describe("Enter Key", () => {
    it("calls onSelect with highlighted item", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { handleKeydown, setHighlight } = useKeyboardNavigation({
        items,
        onSelect,
      });

      setHighlight(1);
      handleKeydown(createMockEvent("Enter"));

      expect(onSelect).toHaveBeenCalledWith(1, "b");
    });

    it("does not call onSelect when nothing highlighted", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { handleKeydown } = useKeyboardNavigation({
        items,
        onSelect,
      });

      handleKeydown(createMockEvent("Enter"));

      expect(onSelect).not.toHaveBeenCalled();
    });
  });

  // ============================================================================
  // ESCAPE KEY
  // ============================================================================

  describe("Escape Key", () => {
    it("calls onEscape", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();
      const onEscape = vi.fn();

      const { handleKeydown } = useKeyboardNavigation({
        items,
        onSelect,
        onEscape,
      });

      handleKeydown(createMockEvent("Escape"));

      expect(onEscape).toHaveBeenCalled();
    });
  });

  // ============================================================================
  // BACKSPACE KEY
  // ============================================================================

  describe("Backspace Key", () => {
    it("calls onBackspace when provided", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();
      const onBackspace = vi.fn();

      const { handleKeydown } = useKeyboardNavigation({
        items,
        onSelect,
        onBackspace,
      });

      handleKeydown(createMockEvent("Backspace"));

      expect(onBackspace).toHaveBeenCalled();
    });

    it("does nothing when onBackspace not provided", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { handleKeydown } = useKeyboardNavigation({
        items,
        onSelect,
      });

      const result = handleKeydown(createMockEvent("Backspace"));
      expect(result).toBe(false);
    });
  });

  // ============================================================================
  // RESET & SET HIGHLIGHT
  // ============================================================================

  describe("Reset & Set Highlight", () => {
    it("resetHighlight sets index to -1", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { highlightedIndex, resetHighlight, setHighlight } = useKeyboardNavigation({
        items,
        onSelect,
      });

      setHighlight(2);
      expect(highlightedIndex.value).toBe(2);

      resetHighlight();
      expect(highlightedIndex.value).toBe(-1);
    });

    it("setHighlight sets specific index", () => {
      const items = ref(["a", "b", "c"]);
      const onSelect = vi.fn();

      const { highlightedIndex, setHighlight } = useKeyboardNavigation({
        items,
        onSelect,
      });

      setHighlight(1);
      expect(highlightedIndex.value).toBe(1);
    });
  });

  // ============================================================================
  // EMPTY ITEMS
  // ============================================================================

  describe("Empty Items", () => {
    it("handles empty items array", () => {
      const items = ref<string[]>([]);
      const onSelect = vi.fn();

      const { highlightedIndex, handleKeydown } = useKeyboardNavigation({
        items,
        onSelect,
      });

      handleKeydown(createMockEvent("ArrowDown"));
      expect(highlightedIndex.value).toBe(-1);
    });
  });
});
