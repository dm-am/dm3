/**
 * @vitest-environment jsdom
 */

/**
 * What a reader is told when they change the picture on their own profile.
 *
 * The picture is the one thing on this screen that answers for itself: it is
 * replaced where the reader is looking, and the "Сбросить" button beside it
 * goes away when there is nothing left to reset. So a successful change says
 * nothing at all, and every sentence this composable can produce is about a
 * change that did not happen.
 *
 * Those sentences are not toasts. A refused upload goes into `refusal`, for
 * the caller to print under the picture, and a refused reset goes back to the
 * window that asked before it, which prints it in itself. The toast store is
 * read here on every case to prove that nothing reached it: the reader pressed
 * on the avatar and looks at the avatar for the answer, and a toast in the
 * corner that stays until dismissed is the shape this file exists to keep out.
 *
 * Three of those sentences matter here. A file that never arrived and a file
 * that arrived and was refused by the profile are different outcomes and read
 * differently - the second one may be retried as it stands. And the throw is
 * not a dead path: the API client answers failures with `{ data, error }` and
 * never throws, but `fetchUser()` hands the viewer to the auth store, which
 * writes it to localStorage, and setItem raises on a store that is full or
 * blocked.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, ref } from "vue";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { uploadApi } from "@/shared/api";
import { useToast } from "@/shared/lib/composables/useToast";
import { useAuthStore } from "@/shared/stores";
import { personalApi } from "../api";
import { useCommunityStore } from "./communityStore";
import { useAvatarUpload } from "./useAvatarUpload";
import type { User } from "./types";

/** The profile the page is drawing, picture and all. */
const READER = {
  id: "9f1c7d5e-0000-4000-8000-000000000001",
  username: "reader",
  picture: { mediumUrl: "/uploads/old.webp" },
} as unknown as User;

/**
 * A file small enough that compressImage hands it back untouched: under the
 * 200 KB threshold it never reaches a canvas, which jsdom does not draw on.
 */
const picked = () =>
  new File(["a tiny png"], "avatar.png", { type: "image/png" });

/** A problem document the interceptor keeps quiet about, so the call site speaks. */
const refusal = { type: "", title: "", status: 400, traceId: "" };

/** A failure the interceptor has already said out loud. */
const serverDown = { type: "", title: "", status: 500, traceId: "" };

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

/** The composable inside a component, so its paste listener has an owner. */
function render() {
  const pinia = createPinia();
  setActivePinia(pinia);

  const community = useCommunityStore();
  community.selectedUser = { ...READER };

  let avatar!: ReturnType<typeof useAvatarUpload>;
  mount(
    defineComponent({
      setup() {
        avatar = useAvatarUpload(ref({ ...READER }));
        return () => null;
      },
    }),
    { global: { plugins: [pinia] } },
  );

  return { avatar, community };
}

/** What the profile picture of the reader is right now, as the page reads it. */
const pictureOnScreen = (community: ReturnType<typeof useCommunityStore>) =>
  community.selectedUser?.picture;

/** The upload and the profile write both answering yes. */
function letTheUploadThrough() {
  vi.spyOn(uploadApi, "directUpload").mockResolvedValue({
    data: { id: "upload-1" },
    error: null,
  } as never);
  vi.spyOn(personalApi, "updateMyProfile").mockResolvedValue({
    data: { picture: { mediumUrl: "/uploads/new.webp" } },
    error: null,
  } as never);
}

/**
 * Signs the reader in and then takes the browser store away, which is the one
 * thing inside either try block that throws.
 */
function breakBrowserStorage() {
  useAuthStore().updateUser({ ...READER });
  vi.spyOn(personalApi, "getMyProfile").mockResolvedValue({
    data: { ...READER },
    error: null,
  } as never);
  vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
    throw new DOMException("quota", "QuotaExceededError");
  });
}

beforeEach(() => {
  vi.restoreAllMocks();
  // The auth store reads the viewer out of localStorage the moment it is
  // created, so a viewer left there by an earlier case would make the next
  // fresh store think somebody is signed in and send fetchUser to the network.
  localStorage.clear();
  [...toast.toasts.value].forEach((one) => toast.dismiss(one.id));
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("changing the picture on a profile", () => {
  // The reason there is no "Аватар обновлен" and no "success with no body"
  // line: the answer is the picture itself.
  it("puts the new picture on the profile and says nothing", async () => {
    letTheUploadThrough();
    const { avatar, community } = render();

    await avatar.uploadFile(picked());

    expect(pictureOnScreen(community)?.mediumUrl).toBe("/uploads/new.webp");
    expect(avatar.refusal.value).toBeNull();
    expect(saidOutLoud()).toEqual([]);
  });

  // A drop and a paste never pass the accept filter of the picker, so the
  // format is decided here, before a byte goes up.
  it("refuses a format the server would not take, before anything goes up", async () => {
    const upload = vi.spyOn(uploadApi, "directUpload");
    const { avatar, community } = render();

    await avatar.uploadFile(
      new File(["gif"], "avatar.gif", { type: "image/gif" }),
    );

    expect(avatar.refusal.value).toBe(
      "Для аватара допустимы только JPEG, PNG или WebP",
    );
    expect(upload).not.toHaveBeenCalled();
    expect(pictureOnScreen(community)?.mediumUrl).toBe("/uploads/old.webp");
    expect(saidOutLoud()).toEqual([]);
  });

  it("says under the picture that the file did not arrive when the upload is refused", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue({
      data: null,
      error: refusal,
    } as never);
    const update = vi.spyOn(personalApi, "updateMyProfile");
    const { avatar, community } = render();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBe("Не удалось загрузить аватар");
    expect(update).not.toHaveBeenCalled();
    expect(pictureOnScreen(community)?.mediumUrl).toBe("/uploads/old.webp");
    expect(saidOutLoud()).toEqual([]);
  });

  // The outcome that used to answer about a profile the reader was not
  // editing. It is also the one the reader may press again unchanged, so it
  // must not read like the refused upload above.
  it("names the avatar, not the profile, when the profile refuses the file", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue({
      data: { id: "upload-1" },
      error: null,
    } as never);
    vi.spyOn(personalApi, "updateMyProfile").mockResolvedValue({
      data: null,
      error: refusal,
    } as never);
    const { avatar, community } = render();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBe("Не удалось сохранить аватар");
    expect(pictureOnScreen(community)?.mediumUrl).toBe("/uploads/old.webp");
    expect(saidOutLoud()).toEqual([]);
  });

  // The line is read through describeFailure and not written from the
  // fallback alone: a 413 arrives with no document at all, and the status is
  // the sentence.
  it("prints the sentence of the server when the answer carries one", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue({
      data: null,
      error: { ...refusal, status: 413 },
    } as never);
    const { avatar } = render();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBe("Файл слишком большой");
    expect(saidOutLoud()).toEqual([]);
  });

  // One carrier per refusal, and it is the line under the picture: a 5xx the
  // interceptor used to plate is said here, and the plate is taken down
  // (refusalHere). The server named nothing, so the line falls back on its
  // own sentence.
  it("says a failure the interceptor used to plate under the picture", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue({
      data: null,
      error: serverDown,
    } as never);
    const { avatar } = render();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBe("Не удалось загрузить аватар");
    expect(saidOutLoud()).toEqual([]);
  });

  // A dead connection is about the reader's network: its plate stays, and the
  // line is blank.
  it("leaves the line blank for a request that never arrived", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue({
      data: null,
      error: { ...serverDown, status: 0 },
    } as never);
    const { avatar } = render();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBeNull();
  });

  it("takes the line down as soon as the reader tries again", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue({
      data: null,
      error: refusal,
    } as never);
    const { avatar, community } = render();
    await avatar.uploadFile(picked());
    expect(avatar.refusal.value).toBe("Не удалось загрузить аватар");

    vi.restoreAllMocks();
    letTheUploadThrough();
    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBeNull();
    expect(pictureOnScreen(community)?.mediumUrl).toBe("/uploads/new.webp");
  });

  // Proof that the catch is not the dead branch it looks like.
  it("answers the reader when the browser store refuses the refreshed session", async () => {
    letTheUploadThrough();
    const { avatar, community } = render();
    breakBrowserStorage();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBe("Не удалось загрузить аватар");
    // The sentence and the screen agree: the picture was never applied.
    expect(pictureOnScreen(community)?.mediumUrl).toBe("/uploads/old.webp");
    expect(avatar.uploading.value).toBe(false);
    expect(saidOutLoud()).toEqual([]);
  });
});

describe("resetting the picture on a profile", () => {
  it("takes the picture off the profile and says nothing", async () => {
    vi.spyOn(personalApi, "removeMyAvatar").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const { avatar, community } = render();

    await avatar.resetAvatar();

    expect(pictureOnScreen(community)).toEqual({});
    expect(avatar.refusal.value).toBeNull();
    expect(saidOutLoud()).toEqual([]);
  });

  /**
   * A reset is asked for in a window, and the window stays open over a
   * refusal and prints it in itself. A line under the picture as well would
   * be the one refusal said twice, so the answer goes back to the window.
   */
  it("answers a refused reset to the window that asked, and leaves the picture where it was", async () => {
    vi.spyOn(personalApi, "removeMyAvatar").mockResolvedValue({
      data: null,
      error: refusal,
    } as never);
    const { avatar, community } = render();

    const answer = await avatar.resetAvatar();

    expect(answer.error).toEqual(refusal);
    expect(avatar.refusal.value).toBeNull();
    expect(pictureOnScreen(community)?.mediumUrl).toBe("/uploads/old.webp");
    expect(saidOutLoud()).toEqual([]);
  });

  it("carries the flags of the window that shows the refusal itself", async () => {
    const remove = vi
      .spyOn(personalApi, "removeMyAvatar")
      .mockResolvedValue({ data: null, error: serverDown } as never);
    const { avatar } = render();
    const flags = { ownsRefusal: true, ownsRateLimit: true };

    const answer = await avatar.resetAvatar(flags);

    expect(remove).toHaveBeenCalledWith(flags);
    expect(answer.error).toEqual(serverDown);
    expect(avatar.refusal.value).toBeNull();
  });

  it("takes the line of a refused upload down when the reader resets instead", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue({
      data: null,
      error: refusal,
    } as never);
    vi.spyOn(personalApi, "removeMyAvatar").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const { avatar } = render();
    await avatar.uploadFile(picked());
    expect(avatar.refusal.value).toBe("Не удалось загрузить аватар");

    await avatar.resetAvatar();

    expect(avatar.refusal.value).toBeNull();
  });

  // Same single source as the catch of the upload. The window that asked
  // counts a throw as a failure and says so in itself.
  it("hands the window a throw when the browser store refuses the refreshed session", async () => {
    vi.spyOn(personalApi, "removeMyAvatar").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const { avatar, community } = render();
    breakBrowserStorage();

    await expect(avatar.resetAvatar()).rejects.toThrow();

    expect(pictureOnScreen(community)?.mediumUrl).toBe("/uploads/old.webp");
    expect(avatar.resetting.value).toBe(false);
    expect(saidOutLoud()).toEqual([]);
  });
});
