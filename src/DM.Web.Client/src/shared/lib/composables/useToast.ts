import { ref, readonly } from "vue";

export type ToastType = "success" | "error" | "info" | "warning";

export interface Toast {
  id: string;
  type: ToastType;
  message: string;
  duration: number;
  remaining: number;
  paused: boolean;
}

const toasts = ref<Toast[]>([]);
const timers = new Map<string, ReturnType<typeof setInterval>>();

let idCounter = 0;

function dismiss(id: string) {
  const timer = timers.get(id);
  if (timer) {
    clearInterval(timer);
    timers.delete(id);
  }
  toasts.value = toasts.value.filter((t) => t.id !== id);
}

function pause(id: string) {
  const toast = toasts.value.find((t) => t.id === id);
  if (toast) {
    toast.paused = true;
  }
}

function resume(id: string) {
  const toast = toasts.value.find((t) => t.id === id);
  if (toast) {
    toast.paused = false;
  }
}

const DURATIONS: Record<ToastType, number> = {
  success: 3000,
  error: 0, // 0 = no auto-dismiss
  info: 5000,
  warning: 8000,
};

function show(type: ToastType, message: string, duration?: number) {
  // Deduplicate: don't show if same message already exists
  const existing = toasts.value.find((t) => t.message === message);
  if (existing) {
    // Reset timer for existing toast
    existing.remaining = existing.duration;
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
  });

  if (finalDuration > 0) {
    const interval = setInterval(() => {
      const toast = toasts.value.find((t) => t.id === id);
      if (!toast) {
        clearInterval(interval);
        timers.delete(id);
        return;
      }

      if (!toast.paused) {
        toast.remaining -= 100;
        if (toast.remaining <= 0) {
          dismiss(id);
        }
      }
    }, 100);

    timers.set(id, interval);
  }
}

export function useToast() {
  return {
    toasts: readonly(toasts),
    dismiss,
    pause,
    resume,
    success: (msg: string, duration?: number) => show("success", msg, duration),
    error: (msg: string, duration?: number) => show("error", msg, duration),
    info: (msg: string, duration?: number) => show("info", msg, duration),
    warning: (msg: string, duration?: number) => show("warning", msg, duration),
  };
}
