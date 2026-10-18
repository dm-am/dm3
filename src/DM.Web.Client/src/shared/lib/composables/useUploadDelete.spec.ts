/**
 * @vitest-environment jsdom
 */

/**
 * Deleting a file is one flow, and it used to be two copies of one.
 *
 * The moderation list and the owner's list each carried it, and the last
 * difference between the copies — which client sent the DELETE — had already
 * been settled on the shared one. What matters to both is here: the request
 * carries the flags of a window that prints the refusal itself, the list is
 * re-read only after the server agreed, and a refusal is handed back to the
 * window with the row still selected, so the reader can see what was refused
 * and try again. One request per press and the refusal line are the window's
 * (ConfirmDialog.spec.ts).
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { Upload } from "@/shared/api/models/common/upload";
import { useToast } from "@/shared/lib/composables/useToast";
import { useUploadDelete } from "./useUploadDelete";

const deleteUpload = vi.hoisted(() => vi.fn());

vi.mock("@/shared/api/uploadApi", () => ({
  default: { deleteUpload },
}));

const file = { id: "u-1", originalFileName: "portrait.png" } as Upload;

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

beforeEach(() => {
  deleteUpload.mockReset();
  for (const shown of [...toast.toasts.value]) toast.dismiss(shown.id);
});

describe("useUploadDelete", () => {
  it("deletes the selected file and re-reads the page", async () => {
    deleteUpload.mockResolvedValue({ error: null });
    const reload = vi.fn();
    const { deleteTarget, deleteFile } = useUploadDelete(reload);

    deleteTarget.value = file;
    const answer = await deleteFile();

    expect(deleteUpload).toHaveBeenCalledWith("u-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(answer?.error).toBeNull();
    // Nothing is said on success: the window closes and the row is gone from
    // the reloaded table.
    expect(saidOutLoud()).toEqual([]);
    expect(reload).toHaveBeenCalled();
  });

  it("does nothing when no file is selected", async () => {
    const reload = vi.fn();
    const { deleteFile } = useUploadDelete(reload);

    await deleteFile();

    expect(deleteUpload).not.toHaveBeenCalled();
    expect(reload).not.toHaveBeenCalled();
  });

  it("hands a refusal back to the window and keeps the row", async () => {
    const refusal = { status: 403, title: "Нельзя" };
    deleteUpload.mockResolvedValue({ error: refusal });
    const reload = vi.fn();
    const { deleteTarget, deleteFile } = useUploadDelete(reload);

    deleteTarget.value = file;
    const answer = await deleteFile();

    expect(answer?.error).toEqual(refusal);
    expect(saidOutLoud()).toEqual([]);
    expect(reload).not.toHaveBeenCalled();
    // Still selected: ref() hands back a reactive view of the same row.
    expect(deleteTarget.value).toEqual(file);
  });
});
