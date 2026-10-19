/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore, RoomAccessType, RoomType } from "@/entities/game";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { ruleMap, scopedComponentCss } from "@/shared/lib/testing/styles";
import NewRoomForm from "./NewRoomForm.vue";

/** The form, in front of the master of a game. */
function mountForm() {
  const pinia = createPinia();
  setActivePinia(pinia);
  return mount(NewRoomForm, { global: { plugins: [pinia] } });
}

describe("NewRoomForm", () => {
  it("starts a room closed, as the old site does, and sends what the form holds", async () => {
    const wrapper = mountForm();
    const create = vi
      .spyOn(useGameDetailsStore(), "createRoom")
      .mockResolvedValue(null);

    expect(
      (
        wrapper.get("input[name='new-room-access'][value='Private']")
          .element as HTMLInputElement
      ).checked,
    ).toBe(true);

    await wrapper.get("#new-room-title").setValue("  Склеп  ");
    await wrapper
      .get("input[name='new-room-type'][value='Chat']")
      .setValue(true);
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(create).toHaveBeenCalledWith({
      title: "  Склеп  ",
      type: RoomType.Chat,
      accessType: RoomAccessType.Private,
      viewPrivateText: false,
      viewDiceResults: false,
      diceEnabled: false,
    });
    expect(
      (wrapper.get("#new-room-title").element as HTMLInputElement).value,
    ).toBe("");
  });

  // RoomFieldLimits.TitleMaxLength: a longer title the server refuses.
  it("takes a title from one character up to the hundred the server keeps", async () => {
    const wrapper = mountForm();
    const create = vi
      .spyOn(useGameDetailsStore(), "createRoom")
      .mockResolvedValue(null);
    const field = wrapper.get("#new-room-title");

    expect((field.element as HTMLInputElement).maxLength).toBe(100);

    await field.setValue("Я");
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(create).toHaveBeenCalledWith(
      expect.objectContaining({ title: "Я" }),
    );
  });
});

// Read off the stylesheet as the build scopes it: jsdom lays nothing out.
describe("NewRoomForm look", () => {
  const HERE = dirname(fileURLToPath(import.meta.url));
  const captionOf = (file: string, scope: string) =>
    [...ruleMap(scopedComponentCss(join(HERE, file), scope))].find(
      ([selector]) => selector.endsWith(`.row[${scope}] > .form-field-label`),
    )?.[1];

  it("stands a caption level with its field, as the settings of the game do", () => {
    const rooms = captionOf("NewRoomForm.vue", "data-v-rooms");
    expect(rooms, "no rule reaches the caption of a row").toBeDefined();
    expect(rooms).toEqual(captionOf("../GameInfoSection.vue", "data-v-info"));
    expect(rooms).toHaveProperty("min-height");
  });
});
