/**
 * @vitest-environment jsdom
 */

// A mark changed by the author: the file keeps what the server answered with.
import { afterEach, describe, expect, it, vi } from "vitest";
import type { PostAttachment } from "@/entities/game";
import { uploadApi } from "@/shared/api";
import { usePostAttachments } from "./usePostAttachments";

const VASYA = { id: "c-vasya", name: "Вася" };

const scheme: PostAttachment = {
  id: "a-3",
  fileName: "Схема.png",
  contentType: "image/png",
  sizeBytes: 2048,
  url: "/v1/uploads/a-3/content",
  createdUtc: "2026-11-04T08:00:00",
  recipients: "DungeonMaster, Assistant, Вася, Ушедший",
  addressees: [VASYA, { id: "c-gone", name: "Ушедший" }],
};

describe("usePostAttachments", () => {
  afterEach(() => vi.restoreAllMocks());

  // Unchecked, the one gone from the room leaves the mark, and his box with it.
  it("keeps the mark the server answered a change with", async () => {
    vi.spyOn(uploadApi, "updateAudience").mockResolvedValue({
      data: {
        resource: {
          recipients: "DungeonMaster, Assistant, Вася",
          addressees: [VASYA],
        },
      },
      error: null,
    } as never);
    const attachments = usePostAttachments(
      () => "p-1",
      () => [scheme],
    );

    await attachments.mark(scheme, {
      visibleTo: "Selected",
      characterIds: ["c-vasya"],
    });

    expect(attachments.list).toEqual([
      {
        ...scheme,
        recipients: "DungeonMaster, Assistant, Вася",
        addressees: [VASYA],
      },
    ]);
  });
});
