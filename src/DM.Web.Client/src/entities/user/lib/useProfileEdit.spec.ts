/**
 * @vitest-environment jsdom
 */

// The refusal is said once, under the form; the fields and the edit mode stay.
import { describe, expect, it, vi, beforeEach } from "vitest";
import { ref } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { uploadApi } from "@/shared/api";
import { personalApi, userApi } from "../api";
import { useCommunityStore } from "../model/communityStore";
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

/** The profile the page reads back after a save, as the server now has it. */
function readBackAs(profile: object) {
  return vi
    .spyOn(userApi, "getUserProfile")
    .mockResolvedValue({ data: { resource: profile }, error: null } as never);
}

describe("saving a profile from its edit mode", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    readBackAs({ id: "reader-id", username: "reader" });
  });

  // An expired session and a dead connection keep the plate.
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

describe("who may edit a profile", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  // The address of a profile may spell the name in another case than the session.
  it("lets the owner edit at an address in another case", () => {
    useAuthStore().user = { username: "Reader" } as never;

    expect(useProfileEdit(ref("reader")).canEdit.value).toBe(true);
    expect(useProfileEdit(ref("Other")).canEdit.value).toBe(false);
  });

  it("lets a guest edit nothing", () => {
    expect(useProfileEdit(ref("reader")).canEdit.value).toBe(false);
  });
});

/** The answers of a save that goes through, and the deletion of an upload left behind. */
function letTheSaveThrough() {
  return {
    update: vi
      .spyOn(personalApi, "updateMyProfile")
      .mockResolvedValue({ data: {}, error: null } as never),
    reset: vi
      .spyOn(personalApi, "removeMyAvatar")
      .mockResolvedValue({ data: null, error: null } as never),
    dropUpload: vi
      .spyOn(uploadApi, "deleteUpload")
      .mockResolvedValue({ data: null, error: null } as never),
  };
}

const aPicture = () => new File(["x"], "face.png", { type: "image/png" });

describe("the avatar in the edit mode of a profile", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    readBackAs({ id: "reader-id", username: "reader" });
    // jsdom has no object URLs.
    URL.createObjectURL = vi.fn(() => "blob:picked");
    URL.revokeObjectURL = vi.fn();
  });

  it("shows the picked picture and sends it with Сохранить, not before", async () => {
    const { update } = letTheSaveThrough();
    const edit = useProfileEdit(ref("reader"));
    edit.toggleEditMode();

    edit.setAvatar({ uploadId: "upload-1", file: aPicture() });

    expect(edit.avatarPreview.value).toBe("blob:picked");
    expect(edit.hasChanges.value).toBe(true);
    expect(update).not.toHaveBeenCalled();

    await edit.saveChanges();

    expect(update).toHaveBeenCalledWith({ avatarUploadId: "upload-1" });
    expect(edit.avatarPreview.value).toBeNull();
  });

  // The PATCH takes no null for the avatar: the reset has its endpoint.
  it("resets the stored picture with Сохранить, on its own endpoint", async () => {
    const { update, reset } = letTheSaveThrough();
    const edit = useProfileEdit(ref("reader"));
    edit.toggleEditMode();
    edit.setField("status", "новый статус");

    edit.removeAvatar(true);
    expect(reset).not.toHaveBeenCalled();
    await edit.saveChanges();

    expect(update).toHaveBeenCalledWith({ status: "новый статус" });
    expect(reset).toHaveBeenCalledTimes(1);
    expect(edit.isEditMode.value).toBe(false);
  });

  it("keeps the reset to send again when it is refused after the fields went", async () => {
    const { reset } = letTheSaveThrough();
    reset.mockResolvedValue({
      data: null,
      error: { status: 400, title: "" },
    } as never);
    const edit = useProfileEdit(ref("reader"));
    edit.toggleEditMode();
    edit.setField("status", "новый статус");
    edit.removeAvatar(true);

    expect(await edit.saveChanges()).toBe(false);

    expect(edit.saveError.value).toBe("Не удалось сбросить аватар");
    expect(edit.pendingChanges.value).toEqual({ avatarUploadId: null });
  });

  it("takes back the picked picture on Отмена and deletes its upload", () => {
    const { update, dropUpload } = letTheSaveThrough();
    const edit = useProfileEdit(ref("reader"));
    edit.toggleEditMode();
    edit.setAvatar({ uploadId: "upload-1", file: aPicture() });

    edit.cancelEdit();

    expect(edit.avatarPreview.value).toBeNull();
    expect(edit.hasChanges.value).toBe(false);
    expect(dropUpload).toHaveBeenCalledWith("upload-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(update).not.toHaveBeenCalled();
  });

  it("deletes a picked upload that another one replaces", () => {
    const { dropUpload } = letTheSaveThrough();
    const edit = useProfileEdit(ref("reader"));
    edit.toggleEditMode();
    edit.setAvatar({ uploadId: "upload-1", file: aPicture() });

    edit.setAvatar({ uploadId: "upload-2", file: aPicture() });

    expect(dropUpload).toHaveBeenCalledWith("upload-1", expect.anything());
    expect(edit.pendingChanges.value).toEqual({ avatarUploadId: "upload-2" });
  });

  it("leaves nothing to save when a picture picked over none is reset", () => {
    letTheSaveThrough();
    const edit = useProfileEdit(ref("reader"));
    edit.toggleEditMode();
    edit.setAvatar({ uploadId: "upload-1", file: aPicture() });

    edit.removeAvatar(false);

    expect(edit.hasChanges.value).toBe(false);
    expect(edit.avatarPreview.value).toBeNull();
  });
});

// The page draws the public profile, which the PATCH does not touch.
describe("the profile after a save", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  it("reads the profile back, so the page shows what was stored", async () => {
    letTheSaveThrough();
    const community = useCommunityStore();
    community.selectedUser = { id: "reader-id", status: "старый" } as never;
    readBackAs({ id: "reader-id", status: "новый статус" });
    const edit = useProfileEdit(ref("reader"));
    edit.toggleEditMode();
    edit.setField("status", "новый статус");

    await edit.saveChanges();

    expect(community.selectedUser).toMatchObject({ status: "новый статус" });
  });

  it("does not put the saved profile over another one opened meanwhile", async () => {
    letTheSaveThrough();
    const community = useCommunityStore();
    community.selectedUser = { id: "other-id", status: "чужой" } as never;
    readBackAs({ id: "reader-id", status: "новый статус" });
    const edit = useProfileEdit(ref("reader"));
    edit.toggleEditMode();
    edit.setField("status", "новый статус");

    await edit.saveChanges();

    expect(community.selectedUser).toMatchObject({ status: "чужой" });
  });
});
