/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { mount, type VueWrapper } from "@vue/test-utils";
import { nextTick } from "vue";
import { useToast } from "@/shared/lib/composables/useToast";
import ToastContainer from "./ToastContainer.vue";

/** The stack of messages as the reader finds it in the corner. */
const stack = () => [...document.body.querySelectorAll(".toast-item")];
const toastNamed = (text: string) =>
  stack().find((item) => item.textContent?.includes(text)) as HTMLElement;

const progressOf = (item: Element) =>
  (item.querySelector(".toast-progress") as HTMLElement | null)?.style.width ??
  null;

let toast: ReturnType<typeof useToast>;
let container: VueWrapper;

beforeEach(() => {
  vi.useFakeTimers();
  toast = useToast();
  container = mount(ToastContainer, { attachTo: document.body });
});

afterEach(() => {
  for (const { id } of [...toast.toasts.value]) toast.dismiss(id);
  container.unmount();
  vi.useRealTimers();
});

describe("ToastContainer", () => {
  it("draws nothing while there is no message", () => {
    expect(document.body.querySelector(".toast-container")).toBeNull();
  });

  it("stacks the messages in the order they came, each of its kind", async () => {
    toast.success("Сохранено");
    toast.error("Не удалось сохранить");
    toast.info("Ссылка скопирована");
    toast.warning("Сессия скоро истечет");
    await nextTick();

    expect(
      stack().map((item) => [
        item.querySelector(".toast-message")?.textContent,
        item.className
          .split(" ")
          .find((c) => c.startsWith("toast-") && c !== "toast-item"),
        item.getAttribute("role"),
      ]),
    ).toEqual([
      ["Сохранено", "toast-success", "status"],
      ["Не удалось сохранить", "toast-error", "alert"],
      ["Ссылка скопирована", "toast-info", "status"],
      ["Сессия скоро истечет", "toast-warning", "status"],
    ]);
  });

  it("takes a message away with its cross", async () => {
    toast.error("Не удалось сохранить");
    toast.info("Ссылка скопирована");
    await nextTick();

    (
      toastNamed("Не удалось сохранить").querySelector(
        ".toast-dismiss",
      ) as HTMLButtonElement
    ).click();
    await nextTick();

    expect(stack().map((item) => item.textContent?.trim())).toEqual([
      "Ссылка скопирована",
    ]);
  });

  it("runs the words in brackets and takes the message away", async () => {
    const run = vi.fn();
    toast.info("Пост сохранен, [перейти к нему] или остаться", 0, run);
    await nextTick();

    const item = toastNamed("Пост сохранен");
    const press = item.querySelector(".toast-action") as HTMLButtonElement;
    expect(item.querySelector(".toast-message")?.textContent).toBe(
      "Пост сохранен, перейти к нему или остаться",
    );
    expect(press.textContent?.trim()).toBe("перейти к нему");

    press.click();
    await nextTick();

    expect(run).toHaveBeenCalledOnce();
    expect(stack()).toHaveLength(0);
  });

  it("leaves brackets as words when the message carries nothing to run", async () => {
    toast.info("Тег [b] закрыт");
    await nextTick();

    const item = toastNamed("Тег");
    expect(item.querySelector(".toast-action")).toBeNull();
    expect(item.querySelector(".toast-message")?.textContent).toBe(
      "Тег [b] закрыт",
    );
  });

  it("draws the time left as a bar that shrinks, and none for an error", async () => {
    toast.success("Сохранено");
    toast.error("Не удалось сохранить");
    await nextTick();
    expect(progressOf(toastNamed("Сохранено"))).toBe("100%");
    expect(progressOf(toastNamed("Не удалось сохранить"))).toBeNull();

    vi.advanceTimersByTime(1500);
    await nextTick();

    expect(progressOf(toastNamed("Сохранено"))).toBe("50%");
  });

  it("goes away by itself when its time is out, an error never", async () => {
    toast.success("Сохранено");
    toast.error("Не удалось сохранить");
    await nextTick();

    vi.advanceTimersByTime(3000);
    await nextTick();

    expect(stack().map((item) => item.textContent?.trim())).toEqual([
      "Не удалось сохранить",
    ]);
  });

  it("holds the time while the pointer is over the message", async () => {
    toast.success("Сохранено");
    await nextTick();
    const item = toastNamed("Сохранено");

    item.dispatchEvent(new MouseEvent("mouseenter"));
    vi.advanceTimersByTime(5000);
    await nextTick();
    expect(progressOf(toastNamed("Сохранено"))).toBe("100%");

    item.dispatchEvent(new MouseEvent("mouseleave"));
    vi.advanceTimersByTime(3000);
    await nextTick();
    expect(stack()).toHaveLength(0);
  });
});
