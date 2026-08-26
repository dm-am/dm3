/**
 * @vitest-environment jsdom
 */

/**
 * Deleting a file is one flow, and it used to be two copies of one.
 *
 * The moderation list and the owner's list each carried it, and the last
 * difference between the copies — which client sent the DELETE — had already
 * been settled on the shared one. What matters to both is here: nothing is
 * asked twice while a request is in flight, the list is re-read only after the
 * server agreed, and a refusal leaves the row selected so the reader can see
 * what was refused and try again.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { Upload } from "@/shared/api/models/common/upload";
import { useUploadDelete } from "./useUploadDelete";

const deleteUpload = vi.hoisted(() => vi.fn());
const success = vi.hoisted(() => vi.fn());
const notifyFailure = vi.hoisted(() => vi.fn());

vi.mock("@/shared/api/uploadApi", () => ({
  default: { deleteUpload },
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ success, error: vi.fn() }),
}));

vi.mock("@/shared/lib/errors", () => ({ notifyFailure }));

const file = { id: "u-1", originalFileName: "portrait.png" } as Upload;

beforeEach(() => {
  deleteUpload.mockReset();
  success.mockReset();
  notifyFailure.mockReset();
});

describe("useUploadDelete", () => {
  it("deletes the selected file and re-reads the page", async () => {
    deleteUpload.mockResolvedValue({ error: null });
    const reload = vi.fn();
    const { deleteTarget, deleting, confirmDelete } = useUploadDelete(reload);

    deleteTarget.value = file;
    await confirmDelete();

    expect(deleteUpload).toHaveBeenCalledWith("u-1");
    expect(success).toHaveBeenCalledWith("Файл удален");
    expect(reload).toHaveBeenCalled();
    expect(deleteTarget.value).toBeNull();
    expect(deleting.value).toBe(false);
  });

  it("does nothing when no file is selected", async () => {
    const reload = vi.fn();
    const { confirmDelete } = useUploadDelete(reload);

    await confirmDelete();

    expect(deleteUpload).not.toHaveBeenCalled();
    expect(reload).not.toHaveBeenCalled();
  });

  it("keeps the row and says what the server said on a refusal", async () => {
    const refusal = { status: 403, title: "Нельзя" };
    deleteUpload.mockResolvedValue({ error: refusal });
    const reload = vi.fn();
    const { deleteTarget, confirmDelete } = useUploadDelete(reload);

    deleteTarget.value = file;
    await confirmDelete();

    expect(notifyFailure).toHaveBeenCalledWith(
      refusal,
      "Не удалось удалить файл",
    );
    expect(reload).not.toHaveBeenCalled();
    // Still selected: ref() hands back a reactive view of the same row.
    expect(deleteTarget.value).toEqual(file);
  });

  it("sends one request however often the button is pressed", async () => {
    let release: (value: { error: null }) => void = () => {};
    deleteUpload.mockReturnValue(
      new Promise<{ error: null }>((resolve) => {
        release = resolve;
      }),
    );
    const { deleteTarget, deleting, confirmDelete } = useUploadDelete(vi.fn());

    deleteTarget.value = file;
    const first = confirmDelete();
    expect(deleting.value).toBe(true);
    await confirmDelete();

    expect(deleteUpload).toHaveBeenCalledTimes(1);

    release({ error: null });
    await first;
  });
});
