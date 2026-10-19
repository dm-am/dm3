import { ref, readonly } from "vue";

export type ToastType = "success" | "error" | "info" | "warning";

/**
 * The one thing a message offers to do; its words are written inside the
 * message, between square brackets, so the offer is not said twice.
 */
export type ToastAction = () => void;

interface Toast {
  id: string;
  type: ToastType;
  message: string;
  /** How long the message stays, in ms; 0 for one that stays until closed. */
  duration: number;
  /** The time left when the clock last stopped or started, in ms. */
  remaining: number;
  paused: boolean;
  /** Counts the times the clock started from the full duration: the bar starts over with it. */
  run: number;
  action?: ToastAction;
}

const toasts = ref<Toast[]>([]);

/** One timeout a message, and when it was set: a pause takes the time gone off the rest. */
const timers = new Map<
  string,
  { timeout: ReturnType<typeof setTimeout>; startedAt: number }
>();

let idCounter = 0;

function stop(id: string) {
  const timer = timers.get(id);
  if (timer) {
    clearTimeout(timer.timeout);
    timers.delete(id);
  }
}

function start(toast: Toast) {
  const { id } = toast;
  timers.set(id, {
    timeout: setTimeout(() => dismiss(id), toast.remaining),
    startedAt: Date.now(),
  });
}

function dismiss(id: string) {
  stop(id);
  toasts.value = toasts.value.filter((t) => t.id !== id);
}

function pause(id: string) {
  const toast = toasts.value.find((t) => t.id === id);
  const timer = timers.get(id);
  if (!toast || !timer) return;
  toast.remaining = Math.max(
    0,
    toast.remaining - (Date.now() - timer.startedAt),
  );
  toast.paused = true;
  stop(id);
}

function resume(id: string) {
  const toast = toasts.value.find((t) => t.id === id);
  if (!toast?.paused) return;
  toast.paused = false;
  start(toast);
}

const DURATIONS: Record<ToastType, number> = {
  success: 3000,
  error: 0, // 0 = no auto-dismiss
  info: 5000,
  warning: 8000,
};

function show(
  type: ToastType,
  message: string,
  duration?: number,
  action?: ToastAction,
) {
  // Deduplicate: don't show if same message already exists
  const existing = toasts.value.find((t) => t.message === message);
  if (existing) {
    // The same message again starts its time over.
    stop(existing.id);
    existing.remaining = existing.duration;
    existing.run++;
    if (existing.duration > 0 && !existing.paused) start(existing);
    return;
  }

  const id = `toast-${++idCounter}`;
  const finalDuration = duration ?? DURATIONS[type];

  toasts.value.push({
    id,
    type,
    message,
    duration: finalDuration,
    remaining: finalDuration,
    paused: false,
    run: 0,
    action,
  });

  if (finalDuration > 0) start(toasts.value[toasts.value.length - 1]);
}

export function useToast() {
  return {
    toasts: readonly(toasts),
    dismiss,
    pause,
    resume,
    success: (msg: string, duration?: number, action?: ToastAction) =>
      show("success", msg, duration, action),
    error: (msg: string, duration?: number, action?: ToastAction) =>
      show("error", msg, duration, action),
    info: (msg: string, duration?: number, action?: ToastAction) =>
      show("info", msg, duration, action),
    warning: (msg: string, duration?: number, action?: ToastAction) =>
      show("warning", msg, duration, action),
  };
}
