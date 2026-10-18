import { ref, onMounted, onUnmounted, type Ref } from "vue";
import type { TooltipPlacement, TooltipPosition } from "./types";
// VIEWPORT_EDGE was VIEWPORT_PADDING here and `edge` in the date popover — one
// distance under two private names, which is how two layers of the same site
// end up keeping different margins from the same edge.
import { TOOLTIP_OFFSET, VIEWPORT_EDGE } from "@/shared/lib/constants/geometry";

interface Size {
  width: number;
  height: number;
}

/**
 * Where a bubble of `bubble`'s size goes against the rectangle it points at:
 * on the asked side, flipped to the opposite one when the window has no room,
 * clamped into the window, and how far the arrow has to slide back towards
 * the anchor after the clamp.
 *
 * The anchor is a rectangle and not an element: a tooltip trigger gives its
 * box, the hidden text of a post the one line of it under the pointer, where
 * the box of a long block would put the bubble over its first line, off the
 * screen.
 */
export function calculateTooltipPosition(
  anchor: DOMRect,
  bubble: Size,
  placement: TooltipPlacement,
  viewport: Size,
): TooltipPosition {
  let finalPlacement = placement;
  let top = 0;
  let left = 0;

  // Calculate initial position based on placement
  switch (placement) {
    case "top":
      top = anchor.top - bubble.height - TOOLTIP_OFFSET;
      left = anchor.left + (anchor.width - bubble.width) / 2;
      // Flip to bottom if not enough space
      if (top < VIEWPORT_EDGE) {
        top = anchor.bottom + TOOLTIP_OFFSET;
        finalPlacement = "bottom";
      }
      break;

    case "bottom":
      top = anchor.bottom + TOOLTIP_OFFSET;
      left = anchor.left + (anchor.width - bubble.width) / 2;
      // Flip to top if not enough space
      if (top + bubble.height > viewport.height - VIEWPORT_EDGE) {
        top = anchor.top - bubble.height - TOOLTIP_OFFSET;
        finalPlacement = "top";
      }
      break;

    case "left":
      top = anchor.top + (anchor.height - bubble.height) / 2;
      left = anchor.left - bubble.width - TOOLTIP_OFFSET;
      // Flip to right if not enough space
      if (left < VIEWPORT_EDGE) {
        left = anchor.right + TOOLTIP_OFFSET;
        finalPlacement = "right";
      }
      break;

    case "right":
      top = anchor.top + (anchor.height - bubble.height) / 2;
      left = anchor.right + TOOLTIP_OFFSET;
      // Flip to left if not enough space
      if (left + bubble.width > viewport.width - VIEWPORT_EDGE) {
        left = anchor.left - bubble.width - TOOLTIP_OFFSET;
        finalPlacement = "left";
      }
      break;
  }

  // Clamp horizontal position to viewport
  const clampedLeft = Math.max(
    VIEWPORT_EDGE,
    Math.min(left, viewport.width - bubble.width - VIEWPORT_EDGE),
  );

  // Calculate arrow offset (how much tooltip shifted from ideal centered position)
  // Positive = arrow moves right, Negative = arrow moves left
  const arrowOffset =
    finalPlacement === "top" || finalPlacement === "bottom"
      ? left - clampedLeft
      : 0;

  // Clamp vertical position to viewport
  top = Math.max(
    VIEWPORT_EDGE,
    Math.min(top, viewport.height - bubble.height - VIEWPORT_EDGE),
  );

  return { top, left: clampedLeft, placement: finalPlacement, arrowOffset };
}

/**
 * The position of a bubble on screen, kept against its anchor while the page
 * scrolls or the window resizes. Lives as long as the bubble does: the bubble
 * is mounted only while it shows, so a page with a hundred triggers keeps no
 * scroll listener for the ninety-nine that are shut.
 */
export function useTooltipPosition(
  anchor: () => DOMRect | null,
  bubbleRef: Ref<HTMLElement | null>,
  placement: Ref<TooltipPlacement>,
) {
  const position = ref<TooltipPosition>({
    top: 0,
    left: 0,
    placement: placement.value,
    arrowOffset: 0,
  });

  function updatePosition() {
    const rect = anchor();
    if (!rect || !bubbleRef.value) return;
    position.value = calculateTooltipPosition(
      rect,
      bubbleRef.value.getBoundingClientRect(),
      placement.value,
      { width: window.innerWidth, height: window.innerHeight },
    );
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

  return { position, updatePosition };
}
