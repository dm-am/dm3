import { ref, readonly } from "vue";

export type ToastType = "success" | "error" | "info" | "warning";

export interface Toast {
  id: string;
  type: ToastType;
  message: string;
  duration: number;
}

const toasts = ref<Toast[]>([]);

let idCounter = 0;

function dismiss(id: string) {
  toasts.value = toasts.value.filter((t) => t.id !== id);
}

function show(type: ToastType, message: string, duration = 5000) {
  const id = `toast-${++idCounter}`;
  toasts.value.push({ id, type, message, duration });
  if (duration > 0) {
    setTimeout(() => dismiss(id), duration);
  }
}

export function useToast() {
  return {
    toasts: readonly(toasts),
    dismiss,
    success: (msg: string) => show("success", msg),
    error: (msg: string) => show("error", msg, 8000),
    info: (msg: string) => show("info", msg),
    warning: (msg: string) => show("warning", msg, 8000),
  };
}
