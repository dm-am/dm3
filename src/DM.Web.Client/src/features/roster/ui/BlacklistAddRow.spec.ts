/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { UserAutocomplete } from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import BlacklistAddRow from "./BlacklistAddRow.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

function mountRow(
  add: (username: string) => Promise<GeneralError | null> = vi
    .fn()
    .mockResolvedValue(null),
  exclude?: string[],
) {
  return mount(BlacklistAddRow, {
    props: { add, exclude },
    global: { stubs: { RouterLink: true } },
  });
}

type Row = ReturnType<typeof mountRow>;

const field = (w: Row) => w.getComponent(UserAutocomplete);
const addButton = (w: Row) =>
  w.findAll("button").find((b) => b.text() === "Добавить")!;

async function choose(w: Row, username: string) {
  field(w).vm.$emit("update:modelValue", username);
  await flushPromises();
}

describe("BlacklistAddRow", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => vi.restoreAllMocks());

  it('keeps "Добавить" hatched until a name is chosen', async () => {
    const w = mountRow();

    expect(field(w).props("placeholder")).toBe("Имя пользователя");
    expect(addButton(w).attributes("disabled")).toBeDefined();
    expect(addButton(w).classes()).toContain("primary");

    await choose(w, "Мордред");

    expect(addButton(w).attributes("disabled")).toBeUndefined();
  });

  it("sends the chosen name trimmed and empties the field on success", async () => {
    const add = vi.fn().mockResolvedValue(null);
    const w = mountRow(add);

    await choose(w, "  Мордред ");
    await addButton(w).trigger("click");
    await flushPromises();

    expect(add).toHaveBeenCalledWith("Мордред");
    expect(field(w).props("modelValue")).toBe("");
    expect(w.find(".action-result").exists()).toBe(false);
  });

  it("does not send again while the answer is out", async () => {
    let answer: (value: GeneralError | null) => void = () => {};
    const add = vi.fn(
      () =>
        new Promise<GeneralError | null>((resolve) => {
          answer = resolve;
        }),
    );
    const w = mountRow(add);

    await choose(w, "Мордред");
    await addButton(w).trigger("click");
    await flushPromises();

    expect(addButton(w).attributes("disabled")).toBeDefined();
    await addButton(w).trigger("click");
    expect(add).toHaveBeenCalledTimes(1);

    answer(null);
    await flushPromises();
    expect(field(w).props("modelValue")).toBe("");
  });

  it("says the refusal under the row and keeps the name in the field", async () => {
    const add = vi.fn().mockResolvedValue({
      type: "",
      title: "Сначала уберите пользователя из игры",
      status: 409,
      traceId: "t",
    });
    const w = mountRow(add);

    await choose(w, "Гавейн");
    await addButton(w).trigger("click");
    await flushPromises();

    const line = w.get(".action-result");
    expect(line.text()).toBe("Сначала уберите пользователя из игры");
    const row = w.get(".bl-add-row").element;
    expect(
      row.compareDocumentPosition(line.element) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(row.contains(line.element)).toBe(false);
    expect(field(w).props("modelValue")).toBe("Гавейн");
    expect(addButton(w).attributes("disabled")).toBeUndefined();
    expect(saidOutLoud()).toEqual([]);
  });

  it("drops the refusal when another name is chosen", async () => {
    const w = mountRow(
      vi.fn().mockResolvedValue({
        type: "",
        title: "Пользователь уже в черном списке",
        status: 409,
        traceId: "t",
      }),
    );
    await choose(w, "Мордред");
    await addButton(w).trigger("click");
    await flushPromises();
    expect(w.find(".action-result").exists()).toBe(true);

    await choose(w, "Моргана");

    expect(w.find(".action-result").exists()).toBe(false);
  });

  it("suggests nobody excluded and searches everyone, the long absent too", () => {
    const w = mountRow(undefined, ["Артур", "Мерлин"]);

    expect(field(w).props("exclude")).toEqual(["Артур", "Мерлин"]);
    expect(field(w).props("includeInactive")).toBe(true);
  });
});
