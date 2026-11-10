export type TooltipPlacement = "top" | "right" | "bottom" | "left";

/** What opened a bubble closes it: one source per bubble, so focus and hover cannot undo each other. */
export type OpenedBy = "hover" | "focus";

export interface TooltipPosition {
  top: number;
  left: number;
  placement: TooltipPlacement;
  /** Arrow offset from center (px) when tooltip is clamped to viewport */
  arrowOffset: number;
}
