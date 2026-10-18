/**
 * @vitest-environment jsdom
 */

/**
 * The field and "Добавить" every blacklist of the site puts a name on with,
 * on the page and not in a window (owner, 12 October 2026: "Нахуя это делать
 * окном?"). It is the row "Управление ролями" invites an assistant with: a
 * name chosen from the suggestions, and the main button beside it.
 *
 * What these catch:
 *  - "Добавить" open before a name is chosen, or open while its request is on
 *    the wire, so a second press sends a second request;
 *  - the name sent with the spaces round it;
 *  - a refusal said anywhere but under the row, or the name gone from the
 *    field with it, so it has to be found again;
 *  - the field left full after a success;
 *  - the field suggesting the names the server refuses (`exclude`), or only
 *    the people who came by this month: the troll who has not is the one the
 *    list is for.
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

/** A name picked from the suggestions, as the field reports it. */
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

  it('держит "Добавить" заштрихованной, пока имя не выбрано', async () => {
    const w = mountRow();

    expect(field(w).props("placeholder")).toBe("Имя пользователя");
    expect(addButton(w).attributes("disabled")).toBeDefined();
    expect(addButton(w).classes()).toContain("primary");

    await choose(w, "Мордред");

    expect(addButton(w).attributes("disabled")).toBeUndefined();
  });

  it("шлет выбранное имя без пробелов и на успехе очищает поле", async () => {
    const add = vi.fn().mockResolvedValue(null);
    const w = mountRow(add);

    await choose(w, "  Мордред ");
    await addButton(w).trigger("click");
    await flushPromises();

    expect(add).toHaveBeenCalledWith("Мордред");
    expect(field(w).props("modelValue")).toBe("");
    expect(w.find(".action-result").exists()).toBe(false);
  });

  it("не шлет второй раз, пока ждет ответа", async () => {
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

  it("пишет отказ под строкой и оставляет имя в поле", async () => {
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
    // Under the row of the field and the button, not inside it.
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

  it("убирает отказ, когда выбрано другое имя", async () => {
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

  it("не подсказывает исключенных и ищет среди всех, давно не заходивших тоже", () => {
    const w = mountRow(undefined, ["Артур", "Мерлин"]);

    expect(field(w).props("exclude")).toEqual(["Артур", "Мерлин"]);
    expect(field(w).props("includeInactive")).toBe(true);
  });

  it("стоит строкой на странице, а не окном", () => {
    const w = mountRow();

    expect(w.find("[role=dialog]").exists()).toBe(false);
    expect(w.get(".bl-add-row").element.contains(field(w).element)).toBe(true);
    expect(w.get(".bl-add-row").element.contains(addButton(w).element)).toBe(
      true,
    );
  });
});
