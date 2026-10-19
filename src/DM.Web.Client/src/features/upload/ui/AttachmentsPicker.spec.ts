/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach, vi } from "vitest";
import { enableAutoUnmount, mount, type VueWrapper } from "@vue/test-utils";
import { privateTooltipOver } from "@/shared/lib/testing/privateTooltip";
import AttachmentsPicker from "./AttachmentsPicker.vue";
import {
  MAX_POST_ATTACHMENTS,
  MAX_POST_ATTACHMENT_BYTES,
} from "../model/postAttachments";
import {
  everyReader,
  type PendingAttachment,
} from "../model/attachmentAudience";

// A panel left open would keep listening on the document for the next test.
enableAutoUnmount(afterEach);
afterEach(() => vi.useRealTimers());

function file(name: string, type = "image/png"): File {
  return new File(["x"], name, { type });
}

const CANDIDATES = [
  { id: "c-vasya", name: "Вася" },
  { id: "c-petya", name: "Петя" },
];

function mountPicker(files: File[] = []) {
  const wrapper: VueWrapper = mount(AttachmentsPicker, {
    attachTo: document.body,
    props: {
      modelValue: files.map((one) => ({ file: one, audience: everyReader() })),
      "onUpdate:modelValue": (value: PendingAttachment[]) =>
        wrapper.setProps({ modelValue: value }),
      error: null,
      "onUpdate:error": (value: string | null) =>
        wrapper.setProps({ error: value }),
      disabled: false,
      candidates: CANDIDATES,
    },
  });
  return wrapper;
}

const held = (wrapper: VueWrapper) =>
  (wrapper.props() as { modelValue: PendingAttachment[] }).modelValue;

async function choose(wrapper: VueWrapper, files: File[]) {
  const input = wrapper.get(".attachment-input");
  Object.defineProperty(input.element, "files", {
    value: files,
    configurable: true,
  });
  await input.trigger("change");
}

const chips = (wrapper: VueWrapper) =>
  wrapper.findAll(".attachment-name").map((name) => name.text());

/** The word of who sees the file of the line, and the panel it opens. */
const who = (wrapper: VueWrapper, index = 0) =>
  wrapper.findAll(".attachment-audience")[index];
const panel = (wrapper: VueWrapper) => wrapper.find("[role=dialog]");
const press = (wrapper: VueWrapper, label: string) =>
  panel(wrapper)
    .findAll("button")
    .find((b) => b.text() === label)!
    .trigger("click");

describe("AttachmentsPicker", () => {
  it("takes files up to the limit, says so, and offers no more", async () => {
    const wrapper = mountPicker();

    await choose(wrapper, [
      file("a.png"),
      file("b.png"),
      file("c.png"),
      file("d.png"),
    ]);

    expect(chips(wrapper)).toEqual(["a.png", "b.png", "c.png"]);
    expect(wrapper.get(".action-result").text()).toBe("Не больше 3 файлов");
    expect(
      wrapper.get(".attachment-input").attributes("disabled"),
    ).toBeDefined();
  });

  it("leaves out a file the server would refuse, naming it and why", async () => {
    const wrapper = mountPicker();

    await choose(wrapper, [
      file("notes.pdf", "application/pdf"),
      file("a.png"),
    ]);

    expect(chips(wrapper)).toEqual(["a.png"]);
    expect(wrapper.get(".action-result").text()).toBe(
      "notes.pdf: Допустимы только JPEG, PNG, WebP и GIF",
    );
  });

  it("empties the field after a choice, so the same file can be chosen again", async () => {
    const wrapper = mountPicker();
    const input = wrapper.get(".attachment-input").element;
    const written: string[] = [];
    Object.defineProperty(input, "value", {
      configurable: true,
      get: () => "",
      set: (value: string) => written.push(value),
    });

    await choose(wrapper, [file("a.png")]);

    expect(written).toEqual([""]);
    expect(chips(wrapper)).toEqual(["a.png"]);
  });

  it("captions the block as the form's other fields and says the bounds the server keeps", () => {
    const wrapper = mountPicker();

    expect(wrapper.get(".attachments-title").text()).toBe("Вложения:");
    expect(wrapper.get(".attachment-hint").text()).toBe(
      `Не больше ${MAX_POST_ATTACHMENTS} картинок, до ${MAX_POST_ATTACHMENT_BYTES / 1024 / 1024} МБ каждая`,
    );
  });

  it("takes a file back by its word, and the refusal with it", async () => {
    const wrapper = mountPicker([file("a.png"), file("b.png")]);
    await wrapper.setProps({ error: "c.png: Файл больше 5 МБ" });

    const remove = wrapper.findAll(".attachment-remove")[0];
    expect(remove.text()).toBe("убрать");
    await remove.trigger("click");

    expect(chips(wrapper)).toEqual(["b.png"]);
    expect(wrapper.find(".action-result").exists()).toBe(false);
  });
});

describe("who sees a file of the form", () => {
  // One line, as a file in the strip of a post: name, size, who sees it, "убрать".
  it("is every reader for a file just chosen", async () => {
    const wrapper = mountPicker();
    await choose(wrapper, [file("map.png")]);

    expect(wrapper.get(".attachment-line").text()).toBe(
      "map.png 1 Б | видят все | убрать",
    );
    expect(who(wrapper).attributes("aria-expanded")).toBe("false");
    expect(panel(wrapper).exists()).toBe(false);
    expect(held(wrapper)[0].audience).toEqual(everyReader());
  });

  // The boxes say whom, the line says it as a hidden block's tooltip would.
  it("opens the characters to check and names who will see the file", async () => {
    const wrapper = mountPicker([file("map.png")]);

    await who(wrapper).trigger("click");
    expect(panel(wrapper).attributes("aria-label")).toBe("Кто видит map.png");
    expect(panel(wrapper).get(".audience-caption").text()).toBe(
      "Кто видит map.png",
    );
    expect(
      panel(wrapper)
        .findAll("label")
        .map((l) => l.text()),
    ).toEqual(["Вася", "Петя"]);
    // None checked is every reader: no line naming the leads alone.
    expect(wrapper.find(".audience-recipients").exists()).toBe(false);

    await panel(wrapper).findAll("input")[0].setValue(true);
    expect(wrapper.get(".audience-recipients").text()).toBe(
      "Получатели: DungeonMaster, Assistant, Вася",
    );
    // A check is the panel's until "Готово".
    expect(held(wrapper)[0].audience).toEqual(everyReader());
  });

  it("marks the file by Готово: green, the names in the line and the readers in the tooltip", async () => {
    const wrapper = mountPicker([file("map.png")]);

    await who(wrapper).trigger("click");
    await panel(wrapper).findAll("input")[0].setValue(true);
    await press(wrapper, "Готово");

    expect(held(wrapper)[0].audience).toEqual({
      visibleTo: "Selected",
      characterIds: ["c-vasya"],
    });
    expect(panel(wrapper).exists()).toBe(false);
    expect(who(wrapper).text()).toBe("видят: Вася");
    const name = wrapper.get(".attachment-name");
    expect(name.classes()).toContain("private-message");
    expect(name.attributes("data-recipients")).toBe(
      "DungeonMaster, Assistant, Вася",
    );
  });

  // The hidden block's own tooltip over the name, once the mark is made.
  it("names the readers of a marked file on hover, as a hidden block does", async () => {
    vi.useFakeTimers();
    const wrapper = mountPicker([file("map.png")]);
    await who(wrapper).trigger("click");
    await panel(wrapper).findAll("input")[0].setValue(true);
    await press(wrapper, "Готово");

    expect(
      await privateTooltipOver(wrapper.get(".attachment-name").element),
    ).toBe("Получатели: DungeonMaster, Assistant, Вася");
  });

  it("gives the file back to every reader when its last box is unchecked", async () => {
    const wrapper = mountPicker([file("map.png")]);
    await who(wrapper).trigger("click");
    await panel(wrapper).findAll("input")[1].setValue(true);
    await press(wrapper, "Готово");
    expect(who(wrapper).text()).toBe("видят: Петя");

    await who(wrapper).trigger("click");
    expect(
      panel(wrapper)
        .findAll<HTMLInputElement>("input")
        .map((box) => box.element.checked),
    ).toEqual([false, true]);
    await panel(wrapper).findAll("input")[1].setValue(false);
    expect(wrapper.find(".audience-recipients").exists()).toBe(false);
    await press(wrapper, "Готово");

    expect(held(wrapper)[0].audience).toEqual(everyReader());
    expect(who(wrapper).text()).toBe("видят все");
    expect(wrapper.get(".attachment-name").classes()).not.toContain(
      "private-message",
    );
  });

  it("closes on Отмена and on Escape and keeps the mark as it was", async () => {
    const wrapper = mountPicker([file("map.png")]);

    await who(wrapper).trigger("click");
    await panel(wrapper).findAll("input")[0].setValue(true);
    await press(wrapper, "Отмена");
    expect(panel(wrapper).exists()).toBe(false);

    await who(wrapper).trigger("click");
    expect(
      panel(wrapper)
        .findAll<HTMLInputElement>("input")
        .map((box) => box.element.checked),
    ).toEqual([false, false]);
    await panel(wrapper).trigger("keydown", { key: "Escape" });
    expect(panel(wrapper).exists()).toBe(false);

    expect(held(wrapper)[0].audience).toEqual(everyReader());
    expect(who(wrapper).text()).toBe("видят все");
  });

  it("marks each file apart", async () => {
    const wrapper = mountPicker([file("map.png"), file("art.png")]);

    await who(wrapper, 1).trigger("click");
    await panel(wrapper).findAll("input")[0].setValue(true);
    await press(wrapper, "Готово");

    expect(held(wrapper).map((one) => one.audience.visibleTo)).toEqual([
      "All",
      "Selected",
    ]);
  });

  // Nobody to show it to: the line says who sees it and opens nothing.
  it("offers no choice in a room without other characters", async () => {
    const wrapper = mountPicker([file("map.png")]);
    await wrapper.setProps({ candidates: [] });

    expect(wrapper.find(".attachment-audience").exists()).toBe(false);
    expect(wrapper.get(".attachment-line").text()).toBe(
      "map.png 1 Б | видят все | убрать",
    );
  });
});
