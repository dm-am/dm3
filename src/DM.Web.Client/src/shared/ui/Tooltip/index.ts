export { default as Tooltip } from "./Tooltip.vue";
export { default as TooltipContent } from "./TooltipContent.vue";
export type { TooltipPlacement, TooltipPosition } from "./types";
export { calculateTooltipPosition, useTooltipPosition } from "./useTooltip";
export {
  anchoredTooltipElement,
  anchoredTooltipOwner,
  hideAnchoredTooltip,
  showAnchoredTooltip,
} from "./anchoredTooltip";
export {
  TAP_CLOSE_DELAY,
  closesByItself,
  isCompatMouseEvent,
  isTap,
  tapTargetOf,
  trackInput,
} from "./touchRule";
