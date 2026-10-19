/**
 * @vitest-environment jsdom
 */
import { afterEach, beforeEach, describe, it, expect, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import { setScrollContainer } from "@/shared/lib/scroll";
import ChangeWarningPointsDialog from "./ChangeWarningPointsDialog.vue";

const changeWarningPoints = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  warningApi: { changeWarningPoints },
}));

vi.mock("vue-final-modal", async () =>
  (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
);

const OWNED = { ownsRefusal: true, ownsRateLimit: true };

const SelectStub = {
  props: ["modelValue", "options", "id"],
  emits: ["update:modelValue"],
  // Compiled at runtime: a TypeScript cast here is a syntax error.
  template: `<select :id="id" class='select-stub' :value="modelValue"
    @change="$emit('update:modelValue', $event.target.value)">
    <option v-for="o in options" :key="o.value" :value="o.value">{{ o.label }}</option>
  </select>`,
};

function render(props: { warningId?: string; currentPoints?: number } = {}) {
  return mount(ChangeWarningPointsDialog, {
    attachTo: document.body,
    props: { warningId: "w-1", currentPoints: 2, ...props },
    global: { stubs: { Select: SelectStub } },
  });
}

type Wrapper = ReturnType<typeof render>;

const main = (w: Wrapper) => w.get(".controls button.primary");
const line = (w: Wrapper) => w.find(".form-refusal");
const shut = (w: Wrapper) =>
  w.findComponent(Dialog).emitted("update:modelValue") ?? [];

async function send(w: Wrapper, reason = "Не нарушение") {
  await w.get("textarea").setValue(reason);
  await main(w).trigger("click");
  await flushPromises();
}

const accepted = (points: number) => ({
  data: { resource: { id: "w-1", points } },
  error: null,
});

const refused = (status: number, extra: Record<string, unknown> = {}) => ({
  data: null,
  error: { type: "", title: "", status, traceId: "t", ...extra },
});

const toast = useToast();

enableAutoUnmount(afterEach);

afterEach(() => {
  document.body.innerHTML = "";
  setScrollContainer(null);
});

describe("the warning points dialog", () => {
  beforeEach(() => {
    changeWarningPoints.mockReset();
    for (const standing of [...toast.toasts.value]) toast.dismiss(standing.id);
  });

  it("is titled by what it changes and names its two actions", () => {
    const w = render();

    expect(w.get("h2").text()).toBe("Изменение баллов");
    expect(w.findAll(".controls button").map((b) => b.text())).toEqual([
      "Сохранить",
      "Отмена",
    ]);
    expect(w.findAll(".form-field-label label").map((l) => l.text())).toEqual([
      "Баллы",
      "Причина изменения",
    ]);
  });

  it("opens on the points the warning carries and offers the whole scale", () => {
    const w = render({ currentPoints: 3 });

    const select = w.find<HTMLSelectElement>(".select-stub");
    expect(select.element.value).toBe("3");
    expect(w.findAll(".select-stub option").map((o) => o.text())).toEqual([
      "0",
      "1",
      "2",
      "3",
      "4",
      "5",
      "6",
    ]);
  });

  it("sends the new points and the explanation as typed", async () => {
    changeWarningPoints.mockResolvedValue(accepted(0));

    const w = render();
    await w.find(".select-stub").setValue("0");
    await send(w, "  Нарушения не было, модератор ошибся  ");

    expect(changeWarningPoints).toHaveBeenCalledWith(
      "w-1",
      { points: 0, reason: "  Нарушения не было, модератор ошибся  " },
      OWNED,
    );
  });

  it("does not send a correction with no explanation behind it", async () => {
    const w = render();
    await w.find(".select-stub").setValue("0");
    await send(w, "   ");

    expect(changeWarningPoints).not.toHaveBeenCalled();
    expect(main(w).attributes("disabled")).toBeDefined();
    expect(shut(w)).toEqual([]);
  });

  it("does not send an explanation longer than the server takes", async () => {
    const w = render();
    await send(w, "я".repeat(2001));

    expect(changeWarningPoints).not.toHaveBeenCalled();
    expect(w.get(".form-field-error").text()).toBe("Слишком длинное значение");
  });

  it("hands the corrected warning back to the list and closes", async () => {
    changeWarningPoints.mockResolvedValue(accepted(0));

    const w = render();
    await send(w);

    expect(w.emitted("success")).toEqual([[{ id: "w-1", points: 0 }]]);
    expect(shut(w)).toEqual([[false]]);
  });

  it("keeps the form open and says the refusal in itself", async () => {
    changeWarningPoints.mockResolvedValue(refused(404));

    const w = render();
    await send(w);

    expect(line(w).text()).toBe("Не удалось изменить баллы");
    expect(w.emitted("success")).toBeUndefined();
    expect(shut(w)).toEqual([]);
    expect(toast.toasts.value).toEqual([]);
    expect(w.get<HTMLTextAreaElement>("textarea").element.value).toBe(
      "Не нарушение",
    );
  });

  it("shows the field the server named as wrong under that field", async () => {
    changeWarningPoints.mockResolvedValue(
      refused(400, { errors: { Reason: ["Empty"] } }),
    );

    const w = render();
    await send(w);

    expect(w.get(".form-field-error").text()).toBe("Обязательное поле");
    expect(line(w).exists()).toBe(false);
    expect(shut(w)).toEqual([]);
  });

  it("asks before a typed explanation is dropped", async () => {
    const w = render();
    await w.get("textarea").setValue("Не нарушение");

    await w.findAll(".controls button")[1].trigger("click");

    expect(w.find(".dialog-backdrop").text()).toContain(
      "Набранная причина пропадет",
    );
    expect(w.emitted("cancel")).toBeUndefined();
    expect(shut(w)).toEqual([]);
  });

  it("leaves at once when nothing is typed", async () => {
    const w = render();

    await w.findAll(".controls button")[1].trigger("click");

    expect(w.emitted("cancel")).toHaveLength(1);
    expect(shut(w)).toEqual([[false]]);
  });
});
