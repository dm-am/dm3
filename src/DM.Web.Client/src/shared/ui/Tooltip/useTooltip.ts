import { ref, computed, onMounted, onUnmounted, type Ref } from "vue";
import type { TooltipPlacement, TooltipPosition } from "./types";

const TOOLTIP_OFFSET = 12;
const VIEWPORT_PADDING = 8;

export function useTooltip(
  triggerRef: Ref<HTMLElement | null>,
  tooltipRef: Ref<HTMLElement | null>,
  placement: Ref<TooltipPlacement>,
) {
  const isVisible = ref(false);
  const position = ref<TooltipPosition>({
    top: 0,
    left: 0,
    placement: "top",
    arrowOffset: 0,
  });

  function calculatePosition(): TooltipPosition {
    if (!triggerRef.value || !tooltipRef.value) {
      return { top: 0, left: 0, placement: placement.value, arrowOffset: 0 };
    }

    const triggerRect = triggerRef.value.getBoundingClientRect();
    const tooltipRect = tooltipRef.value.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    let finalPlacement = placement.value;
    let top = 0;
    let left = 0;
    let idealLeft = 0; // Store ideal position before clamping

    // Calculate initial position based on placement
    switch (placement.value) {
      case "top":
        top = triggerRect.top - tooltipRect.height - TOOLTIP_OFFSET;
        left = triggerRect.left + (triggerRect.width - tooltipRect.width) / 2;
        idealLeft = left;
        // Flip to bottom if not enough space
        if (top < VIEWPORT_PADDING) {
          top = triggerRect.bottom + TOOLTIP_OFFSET;
          finalPlacement = "bottom";
        }
        break;

      case "bottom":
        top = triggerRect.bottom + TOOLTIP_OFFSET;
        left = triggerRect.left + (triggerRect.width - tooltipRect.width) / 2;
        idealLeft = left;
        // Flip to top if not enough space
        if (top + tooltipRect.height > viewportHeight - VIEWPORT_PADDING) {
          top = triggerRect.top - tooltipRect.height - TOOLTIP_OFFSET;
          finalPlacement = "top";
        }
        break;

      case "left":
        top = triggerRect.top + (triggerRect.height - tooltipRect.height) / 2;
        left = triggerRect.left - tooltipRect.width - TOOLTIP_OFFSET;
        idealLeft = left;
        // Flip to right if not enough space
        if (left < VIEWPORT_PADDING) {
          left = triggerRect.right + TOOLTIP_OFFSET;
          finalPlacement = "right";
        }
        break;

      case "right":
        top = triggerRect.top + (triggerRect.height - tooltipRect.height) / 2;
        left = triggerRect.right + TOOLTIP_OFFSET;
        idealLeft = left;
        // Flip to left if not enough space
        if (left + tooltipRect.width > viewportWidth - VIEWPORT_PADDING) {
          left = triggerRect.left - tooltipRect.width - TOOLTIP_OFFSET;
          finalPlacement = "left";
        }
        break;
    }

    // Clamp horizontal position to viewport
    const clampedLeft = Math.max(
      VIEWPORT_PADDING,
      Math.min(left, viewportWidth - tooltipRect.width - VIEWPORT_PADDING),
    );

    // Calculate arrow offset (how much tooltip shifted from ideal centered position)
    // Positive = arrow moves right, Negative = arrow moves left
    const arrowOffset =
      finalPlacement === "top" || finalPlacement === "bottom"
        ? left - clampedLeft
        : 0;

    // Clamp vertical position to viewport
    top = Math.max(
      VIEWPORT_PADDING,
      Math.min(top, viewportHeight - tooltipRect.height - VIEWPORT_PADDING),
    );

    return { top, left: clampedLeft, placement: finalPlacement, arrowOffset };
  }

  function updatePosition() {
    position.value = calculatePosition();
  }

  function show() {
    isVisible.value = true;
    // Position is calculated after tooltip is rendered (in next tick)
  }

  function hide() {
    isVisible.value = false;
  }

  // Handle scroll and resize
  // Use capture: true to catch scroll events from nested scrollable containers
  onMounted(() => {
    window.addEventListener("scroll", updatePosition, {
      passive: true,
      capture: true,
    });
    window.addEventListener("resize", updatePosition, { passive: true });
  });

  onUnmounted(() => {
    window.removeEventListener("scroll", updatePosition, { capture: true });
    window.removeEventListener("resize", updatePosition);
  });

  return {
    isVisible,
    position,
    show,
    hide,
    updatePosition,
  };
}
