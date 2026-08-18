/**
 * @vitest-environment node
 */

/**
 * The composer's half of the two-phase attach.
 *
 * A file cannot be uploaded before the post it belongs to exists, so the post is
 * published first and the files follow. That makes a partial outcome real — a
 * published post with a file missing — and what these assert is that the partial
 * outcome is reported rather than swallowed, and that the uploads are sequential
 * so the server's count of a post's attachments is not read by three requests at
 * once.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("@/shared/api", () => ({
  uploadApi: { directUpload: vi.fn() },
}));

import { uploadApi } from "@/shared/api";
import {
  MAX_POST_ATTACHMENTS,
  MAX_POST_ATTACHMENT_BYTES,
  describeAttachmentProblem,
  uploadPostAttachments,
} from "./postAttachments";

function file(name: string, type: string, size: number): File {
  // Constructing a File with the real size would allocate it; the picker only
  // reads .size, so the property is defined directly.
  const f = new File([""], name, { type });
  Object.defineProperty(f, "size", { value: size });
  return f;
}

describe("describeAttachmentProblem", () => {
  it("accepts every format the server accepts", () => {
    for (const type of ["image/jpeg", "image/png", "image/webp", "image/gif"]) {
      expect(describeAttachmentProblem(file("a", type, 1024))).toBeNull();
    }
  });

  it("refuses the document formats the specification used to name", () => {
    for (const type of [
      "application/pdf",
      "text/plain",
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    ]) {
      expect(describeAttachmentProblem(file("a", type, 1024))).not.toBeNull();
    }
  });

  it("refuses a file over the five-megabyte ceiling", () => {
    expect(
      describeAttachmentProblem(
        file("map.png", "image/png", MAX_POST_ATTACHMENT_BYTES + 1),
      ),
    ).not.toBeNull();
    expect(
      describeAttachmentProblem(
        file("map.png", "image/png", MAX_POST_ATTACHMENT_BYTES),
      ),
    ).toBeNull();
  });
});

describe("uploadPostAttachments", () => {
  beforeEach(() => vi.mocked(uploadApi.directUpload).mockReset());

  it("names the post as the target of every file", async () => {
    vi.mocked(uploadApi.directUpload).mockResolvedValue({
      data: null,
      error: null,
    } as never);

    await uploadPostAttachments("post-1", [
      file("a.png", "image/png", 10),
      file("b.png", "image/png", 10),
    ]);

    expect(uploadApi.directUpload).toHaveBeenCalledTimes(2);
    expect(vi.mocked(uploadApi.directUpload).mock.calls[0][1]).toBe(
      "PostAttachment",
    );
    expect(vi.mocked(uploadApi.directUpload).mock.calls[0][2]).toEqual({
      targetId: "post-1",
    });
  });

  /**
   * The whole point of reporting rather than throwing: the post is already
   * published, so "it failed" is wrong and silence is worse.
   */
  it("reports which files did not make it and counts the ones that did", async () => {
    vi.mocked(uploadApi.directUpload)
      .mockResolvedValueOnce({ data: { id: "u1" }, error: null } as never)
      .mockResolvedValueOnce({
        data: null,
        error: { title: "нет" },
      } as never);

    const result = await uploadPostAttachments("post-1", [
      file("ok.png", "image/png", 10),
      file("broken.png", "image/png", 10),
    ]);

    expect(result.uploadedCount).toBe(1);
    expect(result.failedNames).toEqual(["broken.png"]);
  });

  /**
   * One at a time. The server counts a post's attachments to enforce the limit,
   * and requests racing each other read the same count.
   */
  it("uploads one file at a time", async () => {
    let inFlight = 0;
    let overlapped = false;
    vi.mocked(uploadApi.directUpload).mockImplementation(async () => {
      inFlight += 1;
      if (inFlight > 1) overlapped = true;
      await Promise.resolve();
      inFlight -= 1;
      return { data: null, error: null } as never;
    });

    await uploadPostAttachments(
      "post-1",
      Array.from({ length: MAX_POST_ATTACHMENTS }, (_, i) =>
        file(`${i}.png`, "image/png", 10),
      ),
    );

    expect(overlapped).toBe(false);
  });
});
