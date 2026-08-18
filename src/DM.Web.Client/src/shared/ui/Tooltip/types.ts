export type TooltipPlacement = "top" | "right" | "bottom" | "left";

export interface TooltipPosition {
  top: number;
  left: number;
  placement: TooltipPlacement;
  /** Arrow offset from center (px) when tooltip is clamped to viewport */
  arrowOffset: number;
}
