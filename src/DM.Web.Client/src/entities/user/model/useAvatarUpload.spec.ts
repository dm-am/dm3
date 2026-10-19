/**
 * @vitest-environment jsdom
 */

// The upload of the edit mode: the file goes up and is handed over, the
// profile is not written. No sentence here is a toast.
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { defineComponent } from "vue";
import { mount } from "@vue/test-utils";
import { uploadApi } from "@/shared/api";
import { useToast } from "@/shared/lib/composables/useToast";
import { personalApi } from "../api";
import { useAvatarUpload, type UploadedAvatar } from "./useAvatarUpload";

const READER_ID = "9f1c7d5e-0000-4000-8000-000000000001";

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

const answered = (data: unknown) => ({ data, error: null }) as never;
const refused = (error: object) => ({ data: null, error }) as never;

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

/** The composable inside a component, so its paste listener has an owner. */
function render() {
  const handed: UploadedAvatar[] = [];
  let avatar!: ReturnType<typeof useAvatarUpload>;
  const wrapper = mount(
    defineComponent({
      setup() {
        avatar = useAvatarUpload(
          () => READER_ID,
          (one) => handed.push(one),
        );
        return () => null;
      },
    }),
  );
  return { avatar, handed, wrapper };
}

beforeEach(() => {
  vi.restoreAllMocks();
  [...toast.toasts.value].forEach((one) => toast.dismiss(one.id));
});

afterEach(() => {
  expect(saidOutLoud()).toEqual([]);
  vi.restoreAllMocks();
});

describe("sending a picture up in the edit mode", () => {
  it("hands the upload over and writes nothing to the profile", async () => {
    const upload = vi
      .spyOn(uploadApi, "directUpload")
      .mockResolvedValue(answered({ id: "upload-1" }));
    const update = vi.spyOn(personalApi, "updateMyProfile");
    const { avatar, handed } = render();
    const file = picked();

    await avatar.uploadFile(file);

    expect(upload.mock.calls[0][1]).toBe("UserAvatar");
    expect(upload.mock.calls[0][2]).toMatchObject({ targetId: READER_ID });
    expect(handed).toEqual([{ uploadId: "upload-1", file }]);
    expect(update).not.toHaveBeenCalled();
    expect(avatar.refusal.value).toBeNull();
  });

  // A drop and a paste never pass the accept filter of the picker.
  it("refuses a format the server would not take, before anything goes up", async () => {
    const upload = vi.spyOn(uploadApi, "directUpload");
    const { avatar, handed } = render();

    await avatar.uploadFile(
      new File(["gif"], "avatar.gif", { type: "image/gif" }),
    );

    expect(avatar.refusal.value).toBe(
      "Для аватара допустимы только JPEG, PNG или WebP",
    );
    expect(upload).not.toHaveBeenCalled();
    expect(handed).toEqual([]);
  });

  it("says under the picture that the file did not arrive", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue(refused(refusal));
    const { avatar, handed } = render();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBe("Не удалось загрузить аватар");
    expect(handed).toEqual([]);
  });

  // A 413 arrives with no document at all, and the status is the sentence.
  it("prints the sentence of the server when the answer carries one", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue(
      refused({ ...refusal, status: 413 }),
    );
    const { avatar } = render();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBe("Файл слишком большой");
  });

  // A 5xx is said in the line under the picture and the plate is taken down (refusalHere).
  it("says a failure the interceptor used to plate under the picture", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue(refused(serverDown));
    const { avatar } = render();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBe("Не удалось загрузить аватар");
  });

  // A dead connection is about the reader's network: its plate stays, the line is blank.
  it("leaves the line blank for a request that never arrived", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue(
      refused({ ...serverDown, status: 0 }),
    );
    const { avatar } = render();

    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBeNull();
  });

  it("takes the line down as soon as the reader tries again", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValueOnce(refused(refusal));
    const { avatar, handed } = render();
    await avatar.uploadFile(picked());
    expect(avatar.refusal.value).toBe("Не удалось загрузить аватар");

    vi.spyOn(uploadApi, "directUpload").mockResolvedValue(
      answered({ id: "upload-2" }),
    );
    await avatar.uploadFile(picked());

    expect(avatar.refusal.value).toBeNull();
    expect(handed.map((one) => one.uploadId)).toEqual(["upload-2"]);
  });

  // Linked to nothing, a file that lands after the overlay is gone would wait for nobody.
  it("deletes an upload that lands after the edit mode is gone", async () => {
    let land: (value: unknown) => void = () => undefined;
    vi.spyOn(uploadApi, "directUpload").mockReturnValue(
      new Promise((resolve) => {
        land = resolve;
      }) as never,
    );
    const remove = vi
      .spyOn(uploadApi, "deleteUpload")
      .mockResolvedValue(answered(null));
    const { avatar, handed, wrapper } = render();

    const sending = avatar.uploadFile(picked());
    await vi.waitFor(() => expect(uploadApi.directUpload).toHaveBeenCalled());
    wrapper.unmount();
    land(answered({ id: "upload-late" }));
    await sending;

    expect(handed).toEqual([]);
    expect(remove).toHaveBeenCalledWith("upload-late", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
  });
});
