/**
 * @vitest-environment jsdom
 */

/**
 * The avatar overlay used to change nothing the reader could see. Both actions
 * ended in `fetchUser()`, which writes the session user behind the header, and
 * the big picture on the profile is drawn from `communityStore.selectedUser` —
 * an object filled in one place only, on a change of route. So the new picture
 * arrived at the next reload, and after a reset the "Сбросить" button stayed
 * where it was, offering to remove a picture that was already gone.
 *
 * And when a change did not happen, the overlay said so in a toast: in the
 * corner of the screen, for a press made on the avatar, until dismissed by
 * hand. Now a refused upload says so in a line under the picture, and the
 * second half of this file holds that line where it is: outside the hover
 * shade, so it stands on a touch screen; down with the next attempt; and still
 * a drop target, because a file dropped on a line with no handler is opened by
 * the browser in place of the page. A refused reset is said in the window that
 * asked before it, which stays open over the picture.
 *
 * Read from the rendered page and not from the store, because the store was
 * never the complaint: what was wrong is what the reader was looking at.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { useCommunityStore } from "@/entities/user";
import type { User } from "@/entities/user";
import ProfilePictureUpload from "./ProfilePictureUpload.vue";

const { directUpload, updateMyProfile, removeMyAvatar, success, error } =
  vi.hoisted(() => ({
    directUpload: vi.fn(),
    updateMyProfile: vi.fn(),
    removeMyAvatar: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
  }));

vi.mock("@/shared/api", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  uploadApi: { directUpload },
}));

vi.mock("@/entities/user/api", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  personalApi: { updateMyProfile, removeMyAvatar },
}));

// Canvas is not in jsdom, and the resize is not what this file is about.
vi.mock("@/shared/lib/utils/imageCompression", () => ({
  compressImage: (file: File) => Promise.resolve(file),
}));

// Nothing in the overlay imports this any more. The mock stays so that a toast
// put back by any route - useToast or notifyFailure - lands on these spies.
vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ success, error }),
}));

const READER_ID = "00000000-0000-0000-0000-000000000001";
const OTHER_ID = "00000000-0000-0000-0000-000000000002";

/** A problem document the interceptor keeps quiet about, so the screen speaks. */
const refused = { type: "", title: "", status: 400, traceId: "" };

function profileWith(id: string, originalUrl?: string): User {
  return {
    id,
    username: "reader",
    lastActivityUtc: "2026-09-01T00:00:00Z",
    role: "RegularUser",
    isNewbie: false,
    usernameHistory: [],
    rating: null,
    picture: originalUrl ? { originalUrl } : {},
  } as unknown as User;
}

/** The file the picker hands over on the next click. */
let pendingFile: File | null = null;

const UploadInputStub = defineComponent({
  name: "UploadInput",
  emits: ["uploading"],
  render() {
    return h("button", {
      class: "pick-file",
      onClick: () => {
        const form = new FormData();
        if (pendingFile) form.append("file", pendingFile);
        this.$emit("uploading", form);
      },
    });
  },
});

/**
 * The profile as the reader sees it: the large picture and, over it, the
 * editing overlay. Both read the same store object, which is the whole point.
 */
const ProfileView = defineComponent({
  setup() {
    const store = useCommunityStore();
    return () =>
      h("div", { class: "avatar-wrapper" }, [
        h(AvatarImg, {
          picture: store.selectedUser?.picture,
          alt: "reader",
          size: 220,
          preferOriginal: true,
        }),
        h(ProfilePictureUpload, { username: "reader" }),
      ]);
  },
});

function render() {
  return mount(ProfileView, {
    global: {
      stubs: {
        UploadInput: UploadInputStub,
        // The confirmation of the reset is the real window, drawn in place.
        Teleport: true,
      },
    },
  });
}

/** The address of the picture actually drawn, or null for the silhouette. */
function shownPicture(wrapper: ReturnType<typeof render>): string | null {
  const img = wrapper.find("img");
  if (!img.exists()) return null;
  return img.attributes("src") ?? null;
}

/** The line under the picture, as a reader of the page finds it. */
const refusalLine = (wrapper: ReturnType<typeof render>) =>
  wrapper.find('[role="alert"]');

const aPng = () => new File(["x"], "face.png", { type: "image/png" });

async function pick(wrapper: ReturnType<typeof render>, file: File) {
  pendingFile = file;
  await wrapper.find(".pick-file").trigger("click");
  await flushPromises();
  await nextTick();
}

async function confirmReset(wrapper: ReturnType<typeof render>) {
  await wrapper.find(".reset-btn").trigger("click");
  await nextTick();
  await wrapper.find(".dialog-btn-submit").trigger("click");
  await flushPromises();
  await nextTick();
}

beforeEach(() => {
  setActivePinia(createPinia());
  directUpload.mockReset();
  updateMyProfile.mockReset();
  removeMyAvatar.mockReset();
  success.mockReset();
  error.mockReset();
  pendingFile = null;

  directUpload.mockResolvedValue({ data: { id: "upload-1" }, error: null });
  updateMyProfile.mockResolvedValue({
    data: profileWith(READER_ID, "/avatars/new.png"),
    error: null,
  });
  removeMyAvatar.mockResolvedValue({ data: null, error: null });
});

describe("the avatar overlay on a profile", () => {
  it("draws the uploaded picture without waiting for a reload", async () => {
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();
    expect(shownPicture(wrapper)).toBe("/avatars/old.png");

    await pick(wrapper, aPng());

    expect(shownPicture(wrapper)).toBe("/avatars/new.png");
  });

  it("says nothing on top of a picture that changed in front of the reader", async () => {
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();

    await pick(wrapper, aPng());

    expect(success).not.toHaveBeenCalled();
    expect(refusalLine(wrapper).exists()).toBe(false);
  });

  it("keeps the picture of the profile the reader moved to", async () => {
    // An upload can outlive the profile it started on. The answer belongs to
    // the profile that was left, and it must not repaint the one on screen.
    let answer: (value: unknown) => void = () => undefined;
    updateMyProfile.mockReturnValue(
      new Promise((resolve) => {
        answer = resolve;
      }),
    );

    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();

    pendingFile = aPng();
    await wrapper.find(".pick-file").trigger("click");
    await flushPromises();

    store.selectedUser = profileWith(OTHER_ID, "/avatars/other.png");
    await nextTick();

    answer({ data: profileWith(READER_ID, "/avatars/new.png"), error: null });
    await flushPromises();
    await nextTick();

    expect(shownPicture(wrapper)).toBe("/avatars/other.png");
  });

  it("drops the picture and the reset button as soon as the reset lands", async () => {
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();
    expect(wrapper.find(".reset-btn").exists()).toBe(true);

    await confirmReset(wrapper);

    // The silhouette in place of the picture, and nothing left to reset.
    expect(shownPicture(wrapper)).toBeNull();
    expect(wrapper.find(".default-avatar").exists()).toBe(true);
    expect(wrapper.find(".reset-btn").exists()).toBe(false);
  });

  it("says nothing on top of an avatar that disappeared in front of the reader", async () => {
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();

    await confirmReset(wrapper);

    expect(success).not.toHaveBeenCalled();
    expect(refusalLine(wrapper).exists()).toBe(false);
  });
});

describe("the line under the picture when a change did not happen", () => {
  it("prints a refused upload under the picture, and not in a toast", async () => {
    directUpload.mockResolvedValue({ data: null, error: refused });
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();

    await pick(wrapper, aPng());

    expect(refusalLine(wrapper).text()).toBe("Не удалось загрузить аватар");
    expect(error).not.toHaveBeenCalled();
    expect(shownPicture(wrapper)).toBe("/avatars/old.png");
  });

  it("prints a format the server would not take, before anything goes up", async () => {
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();

    await pick(wrapper, new File(["x"], "face.gif", { type: "image/gif" }));

    expect(refusalLine(wrapper).text()).toBe(
      "Для аватара допустимы только JPEG, PNG или WebP",
    );
    expect(directUpload).not.toHaveBeenCalled();
    expect(error).not.toHaveBeenCalled();
  });

  // The shade is drawn on hover alone. A line inside it is gone on a touch
  // screen, after a paste made with the pointer elsewhere, and after the
  // reset dialog closes with the pointer where the dialog was.
  it("stands in the flow under the picture, outside the hover shade", async () => {
    directUpload.mockResolvedValue({ data: null, error: refused });
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();

    await pick(wrapper, aPng());

    const line = refusalLine(wrapper);
    expect(line.exists()).toBe(true);
    expect(line.element.closest(".profile-picture-upload")).toBeNull();
    // After the picture, inside the same box the picture is in.
    expect(line.element.parentElement).toBe(
      wrapper.find(".avatar-wrapper").element,
    );
    expect(
      wrapper.find("img").element.compareDocumentPosition(line.element) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("takes the line down on the next attempt", async () => {
    directUpload.mockResolvedValueOnce({ data: null, error: refused });
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();

    await pick(wrapper, aPng());
    expect(refusalLine(wrapper).exists()).toBe(true);

    await pick(wrapper, aPng());

    expect(refusalLine(wrapper).exists()).toBe(false);
    expect(shownPicture(wrapper)).toBe("/avatars/new.png");
  });

  // The line sits inside the drop target. Without its own handlers, a file
  // let go over it is not an upload but a navigation: the browser opens the
  // file in place of the profile.
  it("still takes a file dropped on the line itself", async () => {
    directUpload.mockResolvedValueOnce({ data: null, error: refused });
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();
    await pick(wrapper, aPng());
    expect(directUpload).toHaveBeenCalledTimes(1);

    const dropped = new File(["y"], "other.png", { type: "image/png" });
    await refusalLine(wrapper).trigger("drop", {
      dataTransfer: { files: [dropped] },
    });
    await flushPromises();
    await nextTick();

    expect(directUpload).toHaveBeenCalledTimes(2);
    expect(directUpload.mock.calls[1][0]).toBe(dropped);
    expect(shownPicture(wrapper)).toBe("/avatars/new.png");
  });

  it("says a refused reset in the window, which stays open over the picture", async () => {
    removeMyAvatar.mockResolvedValue({ data: null, error: refused });
    const store = useCommunityStore();
    store.selectedUser = profileWith(READER_ID, "/avatars/old.png");
    const wrapper = render();

    await confirmReset(wrapper);

    // The window prints a refusal in itself, so the interceptor keeps quiet.
    expect(removeMyAvatar).toHaveBeenCalledWith({
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось сбросить аватар",
    );
    // Said once: not a second time under the picture, nor in a toast.
    expect(wrapper.find(".avatar-refusal").exists()).toBe(false);
    expect(error).not.toHaveBeenCalled();
    // The picture and the button to reset it are both still there.
    expect(shownPicture(wrapper)).toBe("/avatars/old.png");
    expect(wrapper.find(".reset-btn").exists()).toBe(true);
  });
});
