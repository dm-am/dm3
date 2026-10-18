/**
 * @vitest-environment jsdom
 */

/**
 * The edit mode of a profile, and the one sentence it puts on the page when a
 * save comes back refused.
 *
 * That sentence has to be the only one about the refusal. The response
 * interceptor already raises a plate for a 403, a 429, a 5xx, an expired
 * session and a request that never left, and on a 5xx the plate carries the
 * API's own title - which is exactly what describeFailure hands this line. The
 * reader pressed "Сохранить" once and read the same words twice: under the
 * form and over it.
 *
 * The line is the half that goes, because it takes nothing with it. The fields
 * stay filled, the edit mode stays open and the save still reports failure to
 * the page, so nothing about the screen claims the profile was stored.
 *
 * Everything else - a rejected field, a conflict, a profile that is gone - is
 * this line's alone: the interceptor says nothing about those, and the server
 * named the field and the rule for the line to read out.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { ref } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { personalApi } from "../api";
import { useProfileEdit } from "./useProfileEdit";

/** An edit mode with one pending change in it, ready for a save. */
function editing() {
  const edit = useProfileEdit(ref("reader"));
  edit.toggleEditMode();
  edit.setField("status", "новый статус");
  return edit;
}

/** Makes the next save come back refused with this status. */
function refusedWith(status: number, title = "Ошибка сервера") {
  return vi
    .spyOn(personalApi, "updateMyProfile")
    .mockResolvedValue({ data: null, error: { status, title } } as never);
}

describe("saving a profile from its edit mode", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  // The refusals the interceptor used to plate stand under the form now, and
  // the plate is taken down (refusalHere; the owner's rule of 26 September
  // 2026). An expired session and a dead connection keep the plate.
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
    [401, null],
    [0, null],
  ])("answers a %i under the form", async (status, line) => {
    refusedWith(status);
    const edit = editing();

    const stored = await edit.saveChanges();

    expect(edit.saveError.value).toBe(line);
    // Refused all the same: the change is still pending and the form is open.
    expect(stored).toBe(false);
    expect(edit.hasChanges.value).toBe(true);
    expect(edit.isEditMode.value).toBe(true);
  });

  it("reads out a refusal the interceptor keeps quiet about", async () => {
    refusedWith(400, "Статус длиннее допустимого");
    const edit = editing();

    const stored = await edit.saveChanges();

    expect(edit.saveError.value).toBe("Статус длиннее допустимого");
    expect(stored).toBe(false);
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    refusedWith(409, "");
    const edit = editing();

    await edit.saveChanges();

    expect(edit.saveError.value).toBe("Не удалось сохранить профиль");
  });

  it("clears the line and the edit mode once a save goes through", async () => {
    refusedWith(400, "Статус длиннее допустимого");
    const edit = editing();
    await edit.saveChanges();

    vi.spyOn(personalApi, "updateMyProfile").mockResolvedValue({
      data: {},
      error: null,
    } as never);
    edit.setField("status", "другой статус");
    const stored = await edit.saveChanges();

    expect(stored).toBe(true);
    expect(edit.saveError.value).toBeNull();
    expect(edit.isEditMode.value).toBe(false);
  });
});
