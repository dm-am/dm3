/** Three pinned topics, saves answered one at a time, and the scope the composable runs in. */
import { vi } from "vitest";
import { effectScope, ref, type EffectScope } from "vue";
import type { Topic } from "@/entities/forum";
import { usePinnedOrder } from "./usePinnedOrder";

export const topic = (id: string) =>
  ({ id, title: `Топик ${id}` }) as unknown as Topic;

/** A request the test answers, one at a time in the order they went out. */
export function heldSends() {
  const answers: Array<(value: { error?: unknown }) => void> = [];
  const send = vi.fn<(ids: string[]) => Promise<{ error?: unknown }>>(
    () => new Promise((resolve) => answers.push(resolve)),
  );
  return {
    send,
    answer: (value: { error?: unknown } = {}) => answers.shift()!(value),
  };
}

let scope: EffectScope;

export function openScope() {
  scope = effectScope();
}

export function closeScope() {
  scope.stop();
}

export function board(send = heldSends().send) {
  const pinned = ref<Topic[] | null>([topic("a"), topic("b"), topic("c")]);
  const enabled = ref(true);
  const refused = vi.fn();
  const pins = scope.run(() =>
    usePinnedOrder({
      pinned,
      enabled,
      send: send as never,
      refused,
    }),
  )!;
  const shown = () => pins.order.value.map((t) => String(t.id));
  return { pins, pinned, enabled, refused, shown };
}

export const key = (name: string) =>
  ({ key: name, preventDefault: vi.fn() }) as unknown as KeyboardEvent;
