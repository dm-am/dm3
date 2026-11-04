/**
 * @vitest-environment jsdom
 */

/**
 * The personal note on somebody else's profile, mounted through the page that
 * holds it. A deletion always asks, and saving the note empty is a deletion.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { userApi } from "@/entities/user";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import {
  askedForNote,
  button,
  confirmation,
  dismissToasts,
  FLAGS,
  ok,
  refused,
  render,
  saidOutLoud,
} from "./ProfilePage.fixtures";

vi.mock("vue-router", async (importOriginal) =>
  (await import("./ProfilePage.doubles")).vueRouter(importOriginal),
);

beforeEach(dismissToasts);
afterEach(() => vi.restoreAllMocks());

describe("ProfilePage personal note", () => {
  // The note comes with the profile; the notes endpoint answers 404 for most readers.
  it("takes the note from the profile and asks the notes endpoint nothing", async () => {
    const wrapper = await render({ note: "Хороший мастер" });

    expect(wrapper.get(".note-text").text()).toBe("Хороший мастер");
    expect(askedForNote(wrapper.noteRequest)).toBe(false);
  });

  it("says there is no note when the profile carries none", async () => {
    const wrapper = await render({});

    expect(wrapper.find(".note-empty").exists()).toBe(true);
    expect(askedForNote(wrapper.noteRequest)).toBe(false);
  });

  it("asks before the note is deleted", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Удалить").trigger("click");

    expect(confirmation(wrapper).find(".dialog-title").text()).toBe(
      "Удалить заметку?",
    );
    expect(remove).not.toHaveBeenCalled();

    await confirmation(wrapper).get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(remove).toHaveBeenCalledWith("Other", FLAGS);
    expect(confirmation(wrapper).exists()).toBe(false);
    expect(wrapper.find(".note-empty").exists()).toBe(true);
  });

  it("keeps the note when the question is turned down", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Удалить").trigger("click");
    await confirmation(wrapper).get(".dialog-btn-cancel").trigger("click");

    expect(remove).not.toHaveBeenCalled();
    expect(confirmation(wrapper).exists()).toBe(false);
    expect(wrapper.get(".note-text").text()).toBe("Хороший мастер");
  });

  it("prints a refused delete in the window and keeps the note", async () => {
    vi.spyOn(userApi, "deleteUserProfileNote").mockResolvedValue(refused());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Удалить").trigger("click");
    await confirmation(wrapper).get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(confirmation(wrapper).get(".dialog-refusal").text()).toBe(
      "Не удалось удалить заметку",
    );
    expect(wrapper.get(".note-text").text()).toBe("Хороший мастер");
    expect(saidOutLoud()).toEqual([]);
  });

  // A refused save keeps the editor open with the text and says why under its buttons.
  it("keeps the editor open with the text when a save is refused, and says why under it", async () => {
    vi.spyOn(userApi, "upsertUserProfileNote").mockResolvedValue(refused());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Редактировать").trigger("click");
    wrapper
      .findComponent(BBCodeEditor)
      .vm.$emit("update:modelValue", "Очень хороший мастер");
    await button(wrapper, "Сохранить").trigger("click");
    await flushPromises();

    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(true);
    expect(wrapper.get(".note-actions + .action-result").text()).toBe(
      "Не удалось сохранить заметку",
    );
    expect(saidOutLoud()).toEqual([]);
  });

  it("asks the same question when the note is saved emptied", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const upsert = vi
      .spyOn(userApi, "upsertUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Редактировать").trigger("click");
    wrapper.findComponent(BBCodeEditor).vm.$emit("update:modelValue", "  ");
    await button(wrapper, "Сохранить").trigger("click");

    expect(confirmation(wrapper).find(".dialog-title").text()).toBe(
      "Удалить заметку?",
    );
    expect(remove).not.toHaveBeenCalled();
    expect(upsert).not.toHaveBeenCalled();

    await confirmation(wrapper).get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(remove).toHaveBeenCalledWith("Other", FLAGS);
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
    expect(wrapper.find(".note-empty").exists()).toBe(true);
  });

  it("leaves the editor open with the emptied text when the question is turned down", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Редактировать").trigger("click");
    wrapper.findComponent(BBCodeEditor).vm.$emit("update:modelValue", "");
    await button(wrapper, "Сохранить").trigger("click");
    await confirmation(wrapper).get(".dialog-btn-cancel").trigger("click");

    expect(remove).not.toHaveBeenCalled();
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(true);
  });

  it("closes an empty editor quietly when there is no note to lose", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const upsert = vi
      .spyOn(userApi, "upsertUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: null });

    await button(wrapper, "Добавить").trigger("click");
    await button(wrapper, "Сохранить").trigger("click");
    await flushPromises();

    expect(confirmation(wrapper).exists()).toBe(false);
    expect(remove).not.toHaveBeenCalled();
    expect(upsert).not.toHaveBeenCalled();
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
  });

  // The page holds the note: the section goes with every tab switch, and the saved text must survive it.
  it("shows the saved note after a switch to another tab and back", async () => {
    vi.spyOn(userApi, "upsertUserProfileNote").mockResolvedValue(
      ok({
        id: "note-id",
        text: "Очень хороший мастер",
        createdUtc: "2026-09-01T00:00:00Z",
      }),
    );
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Редактировать").trigger("click");
    wrapper
      .findComponent(BBCodeEditor)
      .vm.$emit("update:modelValue", "Очень хороший мастер");
    await button(wrapper, "Сохранить").trigger("click");
    await flushPromises();
    await button(wrapper, "Топики").trigger("click");
    expect(wrapper.find(".profile-note").exists()).toBe(false);
    await button(wrapper, "О себе").trigger("click");

    expect(wrapper.get(".note-text").text()).toBe("Очень хороший мастер");
  });

  it("keeps the open editor and the text in it across a switch of tab", async () => {
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Редактировать").trigger("click");
    wrapper
      .findComponent(BBCodeEditor)
      .vm.$emit("update:modelValue", "Недописанная заметка");
    await button(wrapper, "Топики").trigger("click");
    expect(wrapper.find(".profile-note").exists()).toBe(false);
    await button(wrapper, "О себе").trigger("click");

    expect(wrapper.getComponent(BBCodeEditor).props("modelValue")).toBe(
      "Недописанная заметка",
    );
  });
});
