/**
 * @vitest-environment jsdom
 */
import { afterEach, beforeEach, describe, it, expect, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import { setScrollContainer } from "@/shared/lib/scroll";
import WarningDialog from "./WarningDialog.vue";

const success = vi.hoisted(() => vi.fn());
const errorToast = vi.hoisted(() => vi.fn());

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({
    success,
    error: errorToast,
    info: vi.fn(),
    warning: vi.fn(),
  }),
}));

const createWarning = vi.hoisted(() => vi.fn());
const getWarnings = vi.hoisted(() => vi.fn());
const getQuotedText = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/moderation")>()),
  warningApi: { getWarnings },
}));

vi.mock("../api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../api")>()),
  default: { createWarning, getQuotedText },
}));

vi.mock("vue-final-modal", async () =>
  (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
);

function render(props: Record<string, unknown> = {}) {
  return mount(WarningDialog, {
    attachTo: document.body,
    props: { username: "Нарушитель", ...props },
    global: {
      plugins: [createPinia()],
      stubs: {
        RouterLink: { template: "<a><slot /></a>" },
        "router-link": { template: "<a><slot /></a>" },
        Select: {
          props: ["modelValue", "options", "id"],
          emits: ["update:modelValue"],
          // Compiled at runtime: a TypeScript cast here is a syntax error.
          template: `<select :id="id" class='select-stub' :value="modelValue"
            @change="$emit('update:modelValue', $event.target.value)">
            <option v-for="o in options" :key="o.value" :value="o.value">{{ o.label }}</option>
          </select>`,
        },
        BBCodeEditor: {
          props: ["modelValue", "disabled"],
          emits: ["update:modelValue"],
          template: `<textarea class='reason-stub' :value="modelValue" :disabled="disabled"
            @input="$emit('update:modelValue', $event.target.value)"></textarea>`,
        },
      },
    },
  });
}

type Wrapper = ReturnType<typeof render>;

const shut = (w: Wrapper) =>
  w.findComponent(Dialog).emitted("update:modelValue") ?? [];

async function issue(wrapper: Wrapper) {
  await wrapper.find(".reason-stub").setValue("Оскорбление в теме");
  await wrapper.get(".controls button.primary").trigger("click");
  await flushPromises();
}

const issued = {
  data: { resource: { id: "w-1", points: 1 } },
  error: null,
};

enableAutoUnmount(afterEach);

afterEach(() => {
  document.body.innerHTML = "";
  setScrollContainer(null);
});

describe("the warning dialog", () => {
  beforeEach(() => {
    success.mockReset();
    errorToast.mockReset();
    createWarning.mockReset();
    getWarnings.mockReset();
    getQuotedText.mockReset();
    getWarnings.mockResolvedValue({ data: { totalPoints: 2 } });
    getQuotedText.mockResolvedValue(null);
    createWarning.mockResolvedValue(issued);
  });

  it("raises the plate where the page behind it cannot show the warning", async () => {
    const wrapper = render();
    await issue(wrapper);

    expect(createWarning).toHaveBeenCalledWith(
      expect.objectContaining({ username: "Нарушитель", points: 1 }),
      { ownsRefusal: true, ownsRateLimit: true },
    );
    expect(success).toHaveBeenCalledWith("Предупреждение выдано");
    expect(wrapper.emitted("success")).toBeTruthy();
    expect(shut(wrapper)).toEqual([[false]]);
  });

  it("keeps quiet where the opener shows the warning itself", async () => {
    const wrapper = render({ hideToast: true });
    await issue(wrapper);

    expect(createWarning).toHaveBeenCalled();
    expect(success).not.toHaveBeenCalled();
    expect(wrapper.emitted("success")).toBeTruthy();
  });

  it("says a refusal in the window even when the plate is off", async () => {
    createWarning.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "t" },
    });

    const wrapper = render({ hideToast: true });
    await issue(wrapper);

    expect(wrapper.get(".form-refusal").text()).toBe(
      "Не удалось выдать предупреждение",
    );
    expect(wrapper.emitted("success")).toBeUndefined();
    expect(success).not.toHaveBeenCalled();
    expect(errorToast).not.toHaveBeenCalled();
    expect(shut(wrapper)).toEqual([]);
    expect(wrapper.get<HTMLTextAreaElement>(".reason-stub").element.value).toBe(
      "Оскорбление в теме",
    );
  });

  it("shuts the editor while the request is on the wire", async () => {
    let answer: (value: unknown) => void = () => {};
    createWarning.mockReturnValue(new Promise((resolve) => (answer = resolve)));

    const wrapper = render();
    await issue(wrapper);

    expect(wrapper.get(".reason-stub").attributes("disabled")).toBeDefined();

    answer({ data: null, error: { status: 404, title: "", type: "" } });
    await flushPromises();

    expect(wrapper.get(".reason-stub").attributes("disabled")).toBeUndefined();
  });

  it("names the violator as a link and counts his points as the profile does", async () => {
    const wrapper = render();
    await flushPromises();

    const lines = wrapper.findAll(".warn-person .stat-line");
    expect(lines.map((line) => line.text().replace(/\s+/g, " "))).toEqual([
      "Нарушитель: Нарушитель",
      "Нарушения: 2/6",
    ]);
    expect(lines[0].find("a").text()).toBe("Нарушитель");
    expect(lines[1].get(".value").classes()).toContain("negative");
  });

  it("writes no points in red where the person carries none", async () => {
    getWarnings.mockResolvedValue({ data: { totalPoints: 0 } });
    const wrapper = render();
    await flushPromises();

    const points = wrapper.findAll(".warn-person .stat-line")[1];
    expect(points.text().replace(/\s+/g, " ")).toBe("Нарушения: 0/6");
    expect(points.get(".value").classes()).not.toContain("negative");
  });

  it("quotes the message whole and links its number to its place", async () => {
    const link =
      "http://localhost:5173/forum/newbies/3?sortOrder=desc#comment-c1";
    const wrapper = render({
      entityId: "c1",
      entityType: "Comment",
      entityLink: link,
      evidence: "<p>Грубость в адрес <strong>мастера</strong></p>",
      number: 12,
    });
    await flushPromises();

    // The text of a comment, as the comment draws it, with the quote frame inside.
    const quote = wrapper.get(
      ".warn-evidence .comment-text.bbcode-content > .quote",
    );
    expect(quote.text()).toBe("Грубость в адрес мастера");
    expect(quote.get("strong").text()).toBe("мастера");

    const number = wrapper.get(".warn-evidence .warn-number");
    expect(number.text()).toBe("12");
    expect(number.attributes("href")).toBe(link);
    expect(number.attributes("target")).toBe("_blank");
    expect(wrapper.text()).not.toContain("http");
    expect(getQuotedText).not.toHaveBeenCalled();
  });

  it("asks for the text of a topic or a chat message it is not handed", async () => {
    getQuotedText.mockResolvedValue("Реклама стороннего сайта");
    const wrapper = render({
      entityId: "t1",
      entityType: "Topic",
      entityLink: "http://localhost:5173/forum/newbies/3",
    });
    await flushPromises();

    expect(getQuotedText).toHaveBeenCalledWith("Topic", "t1");
    expect(wrapper.get(".warn-evidence .quote").text()).toBe(
      "Реклама стороннего сайта",
    );
    expect(wrapper.find(".warn-number").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("http");
  });

  it("holds the fields back until the text it asked for is in", async () => {
    let answer: (text: string | null) => void = () => {};
    getQuotedText.mockReturnValue(new Promise((resolve) => (answer = resolve)));
    const wrapper = render({ entityId: "m1", entityType: "Message" });
    await flushPromises();

    expect(wrapper.find(".warn-person").exists()).toBe(true);
    expect(wrapper.find(".reason-stub").exists()).toBe(false);
    expect(wrapper.find("#warning-points").exists()).toBe(false);

    answer("Реклама стороннего сайта");
    await flushPromises();

    expect(wrapper.get(".warn-evidence .quote").text()).toBe(
      "Реклама стороннего сайта",
    );
    expect(wrapper.find(".reason-stub").exists()).toBe(true);
    expect(wrapper.find("#warning-points").exists()).toBe(true);
  });

  it("gives the fields where the text it asked for did not come", async () => {
    getQuotedText.mockResolvedValue(null);
    const wrapper = render({ entityId: "m1", entityType: "Message" });
    await flushPromises();

    expect(wrapper.find(".warn-evidence").exists()).toBe(false);
    expect(wrapper.find(".reason-stub").exists()).toBe(true);
  });

  it("quotes nothing where there is nothing to quote", async () => {
    const wrapper = render({ entityType: "Profile" });
    await flushPromises();

    expect(wrapper.find(".warn-evidence").exists()).toBe(false);
  });

  it("weighs the warning on one scale from a verbal one to six points", async () => {
    const wrapper = render();
    const options = wrapper.findAll("#warning-points option");

    expect(options.map((option) => option.text())).toEqual([
      "0 (устное предупреждение)",
      "1",
      "2",
      "3",
      "4",
      "5",
      "6",
    ]);

    await wrapper.get("#warning-points").setValue("0");
    await issue(wrapper);

    expect(createWarning).toHaveBeenCalledWith(
      expect.objectContaining({ points: 0, reason: "Оскорбление в теме" }),
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });
});
