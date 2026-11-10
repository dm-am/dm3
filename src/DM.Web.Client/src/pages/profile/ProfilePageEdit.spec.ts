/**
 * @vitest-environment jsdom
 */

/**
 * The edit mode of the owner's profile opens on what is stored, leads to the
 * account for another name, and holds the avatar on the bar like every field.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { personalApi } from "@/entities/user";
import { uploadApi } from "@/shared/api";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import ProfilePersonalInfo from "./ProfilePersonalInfo.vue";
import ProfilePictureUpload from "./ProfilePictureUpload.vue";
import ProfileStats from "./ProfileStats.vue";
import ProfileUsername from "./ProfileUsername.vue";
import { button, dismissToasts, ok, render } from "./ProfilePage.fixtures";

beforeEach(dismissToasts);
afterEach(() => vi.restoreAllMocks());

async function edit(wrapper: VueWrapper) {
  await button(wrapper, "Редактировать").trigger("click");
  await flushPromises();
}

describe("ProfilePage edit mode, opened", () => {
  const OWN = {
    visibility: { showBirthday: true },
    birthday: { day: 14, month: 3, year: 1991 },
  };

  it("hands the fields the owner's own profile, which the public one leaves out", async () => {
    const wrapper = await render({ own: true, ownProfile: OWN });

    await edit(wrapper);

    expect(wrapper.ownRead).toHaveBeenCalledTimes(1);
    expect(
      wrapper.getComponent(ProfilePersonalInfo).props("own"),
    ).toMatchObject(OWN);
  });

  it("offers no opt-in of the rating among the counters", async () => {
    const wrapper = await render({ own: true });

    await edit(wrapper);

    expect(wrapper.getComponent(ProfileStats).props()).not.toHaveProperty(
      "editing",
    );
  });

  it("saves the participation in the rating with the profile, beside the year of the birthday", async () => {
    const update = vi
      .spyOn(personalApi, "updateMyProfile")
      .mockResolvedValue(ok({}));
    const wrapper = await render({ own: true, ownProfile: OWN });
    await edit(wrapper);

    const info = wrapper.getComponent(ProfilePersonalInfo);
    info.vm.$emit("updateField", "showBirthday", "false");
    info.vm.$emit("updateField", "showRating", "false");
    await flushPromises();
    await button(wrapper, "Сохранить").trigger("click");
    await flushPromises();

    expect(update).toHaveBeenCalledWith({
      visibility: { showBirthday: false, showRating: false },
    });
  });

  it("saves the contacts the editor hands over as they are", async () => {
    const update = vi
      .spyOn(personalApi, "updateMyProfile")
      .mockResolvedValue(ok({}));
    const wrapper = await render({ own: true, ownProfile: OWN });
    await edit(wrapper);
    const contacts = [{ contactType: "Telegram", value: "@reader" }];

    wrapper
      .getComponent(ProfilePersonalInfo)
      .vm.$emit("updateContacts", contacts);
    await flushPromises();
    await button(wrapper, "Сохранить").trigger("click");
    await flushPromises();

    expect(update).toHaveBeenCalledWith({ contacts });
  });

  it("asks whether a change of name waits only when the edit mode opens", async () => {
    const wrapper = await render({ own: true, nameChangePending: true });
    expect(wrapper.nameRequest).not.toHaveBeenCalled();

    await edit(wrapper);

    expect(
      wrapper.getComponent(ProfileUsername).props("nameChangePending"),
    ).toBe(true);
  });

  it("stays out of the edit mode when the own profile does not come", async () => {
    const wrapper = await render({ own: true });
    wrapper.ownRead.mockResolvedValue({
      data: null,
      error: { status: 400, title: "", type: "", traceId: "t" },
    } as never);

    await edit(wrapper);

    expect(wrapper.find(".save-bar").exists()).toBe(false);
    expect(wrapper.text()).toContain("Не удалось загрузить профиль");
  });
});

describe("ProfilePage avatar in the edit mode", () => {
  const STORED = { originalUrl: "/avatars/stored.png" };

  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => "blob:picked");
    URL.revokeObjectURL = vi.fn();
  });

  const drawn = (wrapper: VueWrapper) =>
    wrapper.getComponent(AvatarImg).props("picture");

  async function withStoredPicture() {
    const wrapper = await render({ own: true });
    const { useCommunityStore } = await import("@/entities/user");
    const community = useCommunityStore();
    community.selectedUser = { ...community.selectedUser!, picture: STORED };
    await flushPromises();
    await edit(wrapper);
    return wrapper;
  }

  it("draws a picture sent up at once, and Отмена brings the stored one back", async () => {
    const update = vi.spyOn(personalApi, "updateMyProfile");
    const dropUpload = vi
      .spyOn(uploadApi, "deleteUpload")
      .mockResolvedValue(ok());
    const wrapper = await withStoredPicture();

    wrapper
      .getComponent(ProfilePictureUpload)
      .vm.$emit("uploaded", { uploadId: "upload-1", file: new File([], "a") });
    await flushPromises();

    expect(drawn(wrapper)).toEqual({ originalUrl: "blob:picked" });
    expect(wrapper.get(".save-bar").text()).toContain(
      "Несохраненные изменения",
    );

    await button(wrapper, "Отмена").trigger("click");
    await flushPromises();

    expect(drawn(wrapper)).toEqual(STORED);
    expect(dropUpload).toHaveBeenCalledWith("upload-1", expect.anything());
    expect(update).not.toHaveBeenCalled();
  });

  it("draws no picture after a reset, and Сохранить sends the reset", async () => {
    const reset = vi
      .spyOn(personalApi, "removeMyAvatar")
      .mockResolvedValue(ok());
    const wrapper = await withStoredPicture();

    wrapper.getComponent(ProfilePictureUpload).vm.$emit("removed");
    await flushPromises();

    expect(drawn(wrapper)).toEqual({});
    expect(reset).not.toHaveBeenCalled();

    await button(wrapper, "Сохранить").trigger("click");
    await flushPromises();

    expect(reset).toHaveBeenCalledTimes(1);
  });
});
