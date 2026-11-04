/**
 * @vitest-environment jsdom
 */

/**
 * The avatar overlay of the edit mode, read from the rendered page: a picture
 * sent up and a reset are handed to the page, which holds them until
 * "Сохранить"; nothing here writes the profile. A refused upload is said in a
 * line under the picture that stays a drop target.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import ProfilePictureUpload from "./ProfilePictureUpload.vue";

const { directUpload, updateMyProfile, removeMyAvatar } = vi.hoisted(() => ({
  directUpload: vi.fn(),
  updateMyProfile: vi.fn(),
  removeMyAvatar: vi.fn(),
}));

vi.mock("@/shared/api", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  uploadApi: { directUpload },
}));

vi.mock("@/entities/user/api", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  personalApi: { updateMyProfile, removeMyAvatar },
}));

// No canvas in jsdom.
vi.mock("@/shared/lib/utils/imageCompression", () => ({
  compressImage: (file: File) => Promise.resolve(file),
}));

const READER_ID = "00000000-0000-0000-0000-000000000001";

/** A problem document the interceptor keeps quiet about, so the screen speaks. */
const refused = { type: "", title: "", status: 400, traceId: "" };

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

function render(hasAvatar = true) {
  return mount(ProfilePictureUpload, {
    props: { userId: READER_ID, hasAvatar },
    global: { stubs: { UploadInput: UploadInputStub } },
  });
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

beforeEach(() => {
  directUpload.mockReset();
  updateMyProfile.mockReset();
  removeMyAvatar.mockReset();
  pendingFile = null;

  directUpload.mockResolvedValue({ data: { id: "upload-1" }, error: null });
});

describe("the avatar overlay of the edit mode", () => {
  it("hands the page the picture sent up, and writes no profile", async () => {
    const wrapper = render();
    const file = aPng();

    await pick(wrapper, file);

    expect(wrapper.emitted("uploaded")).toEqual([
      [{ uploadId: "upload-1", file }],
    ]);
    expect(updateMyProfile).not.toHaveBeenCalled();
    expect(refusalLine(wrapper).exists()).toBe(false);
  });

  // The reset waits for "Сохранить" and "Отмена" takes it back: nothing to ask first.
  it("hands the page a reset at once, asking nothing and sending nothing", async () => {
    const wrapper = render();

    await wrapper.get(".reset-btn").trigger("click");

    expect(wrapper.emitted("removed")).toHaveLength(1);
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
    expect(removeMyAvatar).not.toHaveBeenCalled();
  });

  it("offers no reset where no picture is drawn", () => {
    const wrapper = render(false);

    expect(wrapper.find(".reset-btn").exists()).toBe(false);
  });
});

describe("the line under the picture when an upload did not happen", () => {
  it("prints a refused upload under the picture, and hands nothing over", async () => {
    directUpload.mockResolvedValue({ data: null, error: refused });
    const wrapper = render();

    await pick(wrapper, aPng());

    expect(refusalLine(wrapper).text()).toBe("Не удалось загрузить аватар");
    expect(wrapper.emitted("uploaded")).toBeUndefined();
  });

  it("prints a format the server would not take, before anything goes up", async () => {
    const wrapper = render();

    await pick(wrapper, new File(["x"], "face.gif", { type: "image/gif" }));

    expect(refusalLine(wrapper).text()).toBe(
      "Для аватара допустимы только JPEG, PNG или WebP",
    );
    expect(directUpload).not.toHaveBeenCalled();
  });

  it("takes the line down on the next attempt", async () => {
    directUpload.mockResolvedValueOnce({ data: null, error: refused });
    const wrapper = render();

    await pick(wrapper, aPng());
    expect(refusalLine(wrapper).exists()).toBe(true);

    await pick(wrapper, aPng());

    expect(refusalLine(wrapper).exists()).toBe(false);
    expect(wrapper.emitted("uploaded")).toHaveLength(1);
  });

  // Without its own handlers a file dropped on the line would open in place of the profile.
  it("still takes a file dropped on the line itself", async () => {
    directUpload.mockResolvedValueOnce({ data: null, error: refused });
    const wrapper = render();
    await pick(wrapper, aPng());
    expect(directUpload).toHaveBeenCalledTimes(1);

    const dropped = new File(["y"], "other.png", { type: "image/png" });
    await refusalLine(wrapper).trigger("drop", {
      dataTransfer: { files: [dropped] },
    });
    await flushPromises();

    expect(directUpload).toHaveBeenCalledTimes(2);
    expect(directUpload.mock.calls[1][0]).toBe(dropped);
    expect(wrapper.emitted("uploaded")?.[0][0]).toMatchObject({
      file: dropped,
    });
  });
});

describe("the shade over the picture", () => {
  const rules = ruleMap(
    componentCss(
      join(dirname(fileURLToPath(import.meta.url)), "ProfilePictureUpload.vue"),
    ),
  );

  it("comes in and fills its strip at the tempo of the site's transitions", () => {
    expect(rules.get(".profile-picture-upload")?.transition).toBe(
      "opacity 0.15s ease",
    );
    expect(rules.get(".upload-progress")?.transition).toBe("width 0.15s ease");
  });
});
