/**
 * @vitest-environment jsdom
 */
import { afterEach, beforeEach, describe, it, expect, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import { setScrollContainer } from "@/shared/lib/scroll";
import LiftBanDialog from "./LiftBanDialog.vue";

const liftBan = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  banApi: { liftBan },
}));

vi.mock("vue-final-modal", async () =>
  (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
);

const OWNED = { ownsRefusal: true, ownsRateLimit: true };

function render() {
  return mount(LiftBanDialog, {
    attachTo: document.body,
    props: { banId: "b-1", username: "Experienced" },
  });
}

type Wrapper = ReturnType<typeof render>;

const main = (w: Wrapper) => w.get(".controls button.primary");
const shut = (w: Wrapper) =>
  w.findComponent(Dialog).emitted("update:modelValue") ?? [];

async function send(w: Wrapper, reason: string) {
  await w.get("textarea").setValue(reason);
  await main(w).trigger("click");
  await flushPromises();
}

enableAutoUnmount(afterEach);

afterEach(() => {
  document.body.innerHTML = "";
  setScrollContainer(null);
});

describe("the lift ban dialog", () => {
  beforeEach(() => liftBan.mockReset());

  it("is titled by what it does, names whose ban goes, and asks the reason", () => {
    const w = render();

    expect(w.get("h2").text()).toBe("Снятие бана");
    expect(w.get(".lift-context").text().replace(/\s+/g, " ")).toBe(
      "Бан Experienced снимется сразу, не дожидаясь срока",
    );
    expect(w.findAll(".form-field-label label").map((l) => l.text())).toEqual([
      "Причина снятия",
    ]);
    expect(w.findAll(".controls button").map((b) => b.text())).toEqual([
      "Снять",
      "Отмена",
    ]);
  });

  it("does not lift a ban with no reason behind it", async () => {
    const w = render();
    await send(w, "   ");

    expect(liftBan).not.toHaveBeenCalled();
    expect(main(w).attributes("disabled")).toBeDefined();
    expect(shut(w)).toEqual([]);
  });

  it("sends the reason as typed and closes on success", async () => {
    liftBan.mockResolvedValue({ data: null, error: null });

    const w = render();
    await send(w, "  Одно из предупреждений снижено до одного балла  ");

    expect(liftBan).toHaveBeenCalledWith(
      "b-1",
      "  Одно из предупреждений снижено до одного балла  ",
      OWNED,
    );
    expect(w.emitted("success")).toHaveLength(1);
    expect(shut(w)).toEqual([[false]]);
  });

  it("keeps the window open with the reason in it when the server refuses", async () => {
    liftBan.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    });

    const w = render();
    await send(w, "Разобрались");

    expect(w.emitted("success")).toBeUndefined();
    expect(shut(w)).toEqual([]);
    expect(w.get(".form-refusal").text()).not.toBe("");
    expect((w.get("textarea").element as HTMLTextAreaElement).value).toBe(
      "Разобрались",
    );
  });
});
