/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { submitEdit } from "./CommentItem.doubles";
import {
  LINE,
  action,
  comment,
  freshComment,
  mountComponent,
  plates,
  refused,
} from "./CommentItem.fixtures";

vi.mock("vue-router", async () =>
  (await import("./CommentItem.doubles")).vueRouter(),
);

vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

enableAutoUnmount(afterEach);

beforeEach(freshComment);

describe("CommentItem edit seeding", () => {
  it("seeds the editor from the fetched raw BBCode source, not the displayed HTML", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(fetch).toHaveBeenCalledWith("c-1");
    const editor = wrapper.find(".editor-stub");
    expect(editor.exists()).toBe(true);
    expect((editor.element as HTMLTextAreaElement).value).toBe(
      "[b]Правила[/b]",
    );
  });

  it("does not open the editor when the source fetch fails", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: null,
      error: { message: "network" },
    });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  it("prints the refusal under the footer instead of raising a plate", async () => {
    const wrapper = mountComponent(vi.fn().mockResolvedValue(refused(404)));

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    const line = wrapper.find(LINE);
    expect(line.exists()).toBe(true);
    expect(line.text()).toBe("Не удалось загрузить текст комментария");
    expect(line.attributes("role")).toBe("alert");
    expect(plates()).toEqual([]);
  });

  it("prints what the server said when it said something", async () => {
    const wrapper = mountComponent(
      vi.fn().mockResolvedValue(refused(404, "Комментарий не найден")),
    );

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).text()).toBe("Комментарий не найден");
  });

  it("says a failure the interceptor used to plate under the footer too", async () => {
    const wrapper = mountComponent(vi.fn().mockResolvedValue(refused(503)));

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).text()).toBe(
      "Не удалось загрузить текст комментария",
    );
    expect(plates()).toEqual([]);
  });

  it("leaves a dead connection to its plate", async () => {
    const wrapper = mountComponent(vi.fn().mockResolvedValue(refused(0)));

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).exists()).toBe(false);
  });

  it("clears the refusal on the next press", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(refused(404))
      .mockResolvedValueOnce({
        data: { resource: comment({ text: "[b]Правила[/b]" }) },
        error: undefined,
      });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();
    expect(wrapper.find(LINE).exists()).toBe(true);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).exists()).toBe(false);
    expect(wrapper.find(".editor-stub").exists()).toBe(true);
  });

  it("hands the edited raw BBCode to the save function", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();
    await wrapper
      .findAll("button")
      .find((button) => button.text() === "Сохранить")!
      .trigger("click");
    await flushPromises();

    expect(submitEdit).toHaveBeenCalledWith("c-1", "[b]Правила[/b]");
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  it("keeps the editor open and names the refusal when the save fails", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    submitEdit.mockResolvedValue({
      error: { type: "", title: "", status: 404, traceId: "t" },
    });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();
    await wrapper
      .findAll("button")
      .find((button) => button.text() === "Сохранить")!
      .trigger("click");
    await flushPromises();

    expect(wrapper.find(".editor-stub").exists()).toBe(true);
    expect(wrapper.find(".edit-container " + LINE).text()).toBe(
      "Не удалось сохранить комментарий",
    );
    expect(plates()).toEqual([]);
  });
});
