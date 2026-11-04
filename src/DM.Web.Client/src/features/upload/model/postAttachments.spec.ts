/**
 * @vitest-environment node
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
  pickAttachments,
  uploadPostAttachments,
} from "./postAttachments";
import { everyReader, type PendingAttachment } from "./attachmentAudience";

function file(name: string, type: string, size: number): File {
  // A File of the real size would allocate it; the picker reads only .size.
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

describe("pickAttachments", () => {
  const names = (files: File[]) => files.map((f) => f.name);
  const png = (name: string) => file(name, "image/png", 10);

  it("ends the pick at the limit, with the limit as its refusal", () => {
    const picked = pickAttachments(
      ["a.png", "b.png", "c.png", "d.png", "e.png"].map(png),
      0,
    );

    expect(names(picked.accepted)).toEqual(["a.png", "b.png", "c.png"]);
    expect(picked.error).toBe("Не больше 3 файлов");
  });

  it("leaves out a file of the wrong kind and tries the next one", () => {
    const picked = pickAttachments(
      [file("rules.pdf", "application/pdf", 10), png("map.png")],
      0,
    );

    expect(names(picked.accepted)).toEqual(["map.png"]);
    expect(picked.error).toBe(
      "rules.pdf: Допустимы только JPEG, PNG, WebP и GIF",
    );
  });

  it("leaves the last refusal of the pick standing", () => {
    const big = file("big.png", "image/png", MAX_POST_ATTACHMENT_BYTES + 1);
    const picked = pickAttachments(
      [file("rules.pdf", "application/pdf", 10), big, png("map.png")],
      0,
    );

    expect(names(picked.accepted)).toEqual(["map.png"]);
    expect(picked.error).toBe(`big.png: ${describeAttachmentProblem(big)}`);
  });

  it("counts the files already held toward the limit", () => {
    const fits = pickAttachments([png("a.png"), png("b.png")], 1);
    expect(names(fits.accepted)).toEqual(["a.png", "b.png"]);
    expect(fits.error).toBeNull();

    const over = pickAttachments([png("a.png"), png("b.png")], 2);
    expect(names(over.accepted)).toEqual(["a.png"]);
    expect(over.error).toBe("Не больше 3 файлов");

    const none = pickAttachments([png("a.png")], MAX_POST_ATTACHMENTS);
    expect(none.accepted).toEqual([]);
    expect(none.error).toBe("Не больше 3 файлов");
  });
});

/** A file chosen in a form, every reader's unless marked. */
const chosen = (one: File, audience = everyReader()): PendingAttachment => ({
  file: one,
  audience,
});

describe("uploadPostAttachments", () => {
  beforeEach(() => vi.mocked(uploadApi.directUpload).mockReset());

  it("names the post as the target of every file", async () => {
    vi.mocked(uploadApi.directUpload).mockResolvedValue({
      data: null,
      error: null,
    } as never);

    await uploadPostAttachments("post-1", [
      chosen(file("a.png", "image/png", 10)),
      chosen(file("b.png", "image/png", 10)),
    ]);

    expect(uploadApi.directUpload).toHaveBeenCalledTimes(2);
    expect(vi.mocked(uploadApi.directUpload).mock.calls[0][1]).toBe(
      "PostAttachment",
    );
    expect(vi.mocked(uploadApi.directUpload).mock.calls[0][2]).toEqual({
      targetId: "post-1",
      audience: everyReader(),
    });
  });

  // Stored with the file: no moment when it is seen wider than chosen.
  it("sends each file with the mark chosen for it", async () => {
    vi.mocked(uploadApi.directUpload).mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const map = file("map.png", "image/png", 10);
    const art = file("art.png", "image/png", 10);
    const vasya = { visibleTo: "Selected" as const, characterIds: ["c-vasya"] };

    await uploadPostAttachments("post-1", [chosen(map, vasya), chosen(art)]);

    const calls = vi.mocked(uploadApi.directUpload).mock.calls;
    expect(calls[0][0]).toBe(map);
    expect(calls[0][2]?.audience).toEqual(vasya);
    expect(calls[1][0]).toBe(art);
    expect(calls[1][2]?.audience).toEqual(everyReader());
  });

  it("reports which files did not make it and counts the ones that did", async () => {
    vi.mocked(uploadApi.directUpload)
      .mockResolvedValueOnce({ data: { id: "u1" }, error: null } as never)
      .mockResolvedValueOnce({
        data: null,
        error: { title: "нет" },
      } as never);

    const result = await uploadPostAttachments("post-1", [
      chosen(file("ok.png", "image/png", 10)),
      chosen(file("broken.png", "image/png", 10)),
    ]);

    expect(result.uploadedCount).toBe(1);
    expect(result.failedNames).toEqual(["broken.png"]);
  });

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
        chosen(file(`${i}.png`, "image/png", 10)),
      ),
    );

    expect(overlapped).toBe(false);
  });
});
