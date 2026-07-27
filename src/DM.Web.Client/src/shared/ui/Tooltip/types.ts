export type TooltipPlacement = "top" | "right" | "bottom" | "left";

export interface TooltipProps {
  /** Tooltip text content. If undefined/empty, tooltip won't show */
  text?: string;
  /** Tooltip position relative to trigger */
  placement?: TooltipPlacement;
  /** Delay before showing tooltip (ms) */
  delay?: number;
  /** Disable tooltip */
  disabled?: boolean;
  /** Make the trigger span keyboard-focusable (tabindex="0"). Use when the
   * slotted content isn't itself a focusable element (e.g. plain text). */
  focusable?: boolean;
}

export interface TooltipPosition {
  top: number;
  left: number;
  placement: TooltipPlacement;
  /** Arrow offset from center (px) when tooltip is clamped to viewport */
  arrowOffset: number;
}
