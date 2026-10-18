/**
 * @vitest-environment jsdom
 */

/**
 * Who the warning dialog has to speak to, and when it has to keep quiet.
 *
 * A warning is a record in the violator's profile. Three of the four places
 * that open this dialog — a forum topic, a global-chat message, a comment
 * list — have nowhere to put such a record: the moderator presses "Выдать
 * предупреждение", the dialog closes and the page behind it is exactly what it
 * was. There the toast is the only evidence the warning exists, and it stays.
 *
 * The fourth is the moderation block of the profile, where the warning lands on
 * the very screen it was issued from: the counter and the active-points bracket
 * are refetched and the open list of warnings takes the new row. A toast on top
 * of that says a second time what the reader is already looking at, so that
 * opener asks for silence with `hideToast` (UI_STANDARDS, "Всплывающие
 * сообщения").
 *
 * Silence is asked for by the opener and never assumed: nothing here lets the
 * dialog go quiet on its own, and a refusal is said in both modes. It is said
 * in the window, which stays open over what the moderator wrote: the request
 * is the window's (Dialog's `action`), so the site's own window, form and
 * fields are mounted, and only the library tier, the lists and the editor are
 * reduced to what this dialog decides.
 */
import { afterEach, beforeEach, describe, it, expect, vi } from "vitest";
import { defineComponent, h } from "vue";
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
const getUserWarnings = vi.hoisted(() => vi.fn());
const getQuotedText = vi.hoisted(() => vi.fn());

vi.mock("../api", () => ({
  default: { createWarning, getUserWarnings, getQuotedText },
}));

// The library tier is a box that holds the card: every closing the window
// makes is its own decision, and that decision is what is read below.
vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    name: "VueFinalModal",
    setup:
      (_props, { slots }) =>
      () =>
        h("div", { class: "vfm-content" }, slots.default?.()),
  }),
}));

function render(props: Record<string, unknown> = {}) {
  return mount(WarningDialog, {
    attachTo: document.body,
    props: { username: "Нарушитель", ...props },
    global: {
      // The quote reads the reader's switch "Перенос слов".
      plugins: [createPinia()],
      stubs: {
        RouterLink: { template: "<a><slot /></a>" },
        "router-link": { template: "<a><slot /></a>" },
        Select: {
          props: ["modelValue", "options", "id"],
          emits: ["update:modelValue"],
          // Compiled at runtime, so the template is plain JS: a TypeScript
          // cast on the event target is a syntax error here.
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

/** Fills in the one required field and presses the one action. */
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
    getUserWarnings.mockReset();
    getQuotedText.mockReset();
    getUserWarnings.mockResolvedValue({ data: { totalPoints: 2 } });
    getQuotedText.mockResolvedValue(null);
    createWarning.mockResolvedValue(issued);
  });

  /**
   * The default, which is the topic, the chat and the comment list: the page
   * behind the dialog cannot show a warning, so without this line the moderator
   * presses the button and nothing at all happens on screen.
   */
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

  /**
   * The profile moderation block, which counts the warning on the screen the
   * moderator is looking at. The plate would repeat that count in the corner.
   */
  it("keeps quiet where the opener shows the warning itself", async () => {
    const wrapper = render({ hideToast: true });
    await issue(wrapper);

    expect(createWarning).toHaveBeenCalled();
    expect(success).not.toHaveBeenCalled();
    expect(wrapper.emitted("success")).toBeTruthy();
  });

  /**
   * Silence is about the confirmation only. A refusal is never visible on the
   * screen behind, whoever opened the dialog, so it is said in both modes, in
   * the window that stays open over the reason — and the warning is not
   * reported as issued.
   */
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

  /**
   * Whom the warning is for is said as the profile says it, the name a link
   * and the points he carries now in the profile's line: the moderator weighs
   * the warning against what the person already carries.
   */
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
    getUserWarnings.mockResolvedValue({ data: { totalPoints: 0 } });
    const wrapper = render();
    await flushPromises();

    const points = wrapper.findAll(".warn-person .stat-line")[1];
    expect(points.text().replace(/\s+/g, " ")).toBe("Нарушения: 0/6");
    expect(points.get(".value").classes()).not.toContain("negative");
  });

  /**
   * What for is the message itself, as a [quote] of the site, with its number
   * under it linking to its place: never the address written out.
   */
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

    const quote = wrapper.get(".warn-evidence .bbcode-content > .quote");
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
    // A topic has no number in its discussion, so nothing stands for one.
    expect(wrapper.find(".warn-number").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("http");
  });

  /**
   * The quote stands above the fields. Where the window asks for the text
   * itself, the fields wait for it: drawn first, they would move down under
   * the moderator's hand when the text lands.
   */
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

  /**
   * One field for the weight, as in the window that changes the points: the
   * whole scale, zero the verbal warning.
   */
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
