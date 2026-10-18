/**
 * @vitest-environment node
 */

import { describe, it, expect, vi } from "vitest";
import type { Comment } from "@/shared/api/models/common";
import {
  createCommentSection,
  type CommentSectionApi,
} from "./createCommentSection";

const refusal = (title: string) => ({
  type: "",
  title,
  status: 409,
  traceId: "t",
});

const comment = (likes: Array<{ username: string }> = []) =>
  ({ id: "c-1", text: "", likes }) as unknown as Comment;

/** The adapter a details store binds, with the like half under test. */
const adapter = (over: Partial<CommentSectionApi> = {}): CommentSectionApi => ({
  getComments: vi.fn(),
  updateComment: vi.fn(),
  deleteComment: vi.fn(),
  likeComment: vi.fn(),
  unlikeComment: vi.fn(),
  currentUsername: () => "Reader",
  ...over,
});

/**
 * The game, the blog and the publication run their discussions through this
 * slice, and the item that was pressed is what says a refused like. So the
 * answer has to come back out of it, and a refusal must not move the heart.
 */
describe("createCommentSection likes", () => {
  it("adds the liker and answers with no error", async () => {
    const section = createCommentSection(
      adapter({
        likeComment: vi.fn().mockResolvedValue({
          data: { resource: { username: "Reader" } },
          error: null,
        }),
      }),
    );
    section.comments.value = [comment()];

    const { error } = await section.likeComment("c-1");

    expect(error).toBeNull();
    expect(section.comments.value[0].likes).toEqual([{ username: "Reader" }]);
  });

  it("hands a refused like back and adds nobody", async () => {
    const answer = refusal("Вы уже поставили лайк");
    const section = createCommentSection(
      adapter({
        likeComment: vi.fn().mockResolvedValue({ data: null, error: answer }),
      }),
    );
    section.comments.value = [comment()];

    const { error } = await section.likeComment("c-1");

    expect(error).toBe(answer);
    expect(section.comments.value[0].likes).toEqual([]);
  });

  it("takes the reader's like out and answers with no error", async () => {
    const section = createCommentSection(
      adapter({
        unlikeComment: vi.fn().mockResolvedValue({ data: null, error: null }),
      }),
    );
    section.comments.value = [
      comment([{ username: "Reader" }, { username: "Other" }]),
    ];

    const { error } = await section.unlikeComment("c-1");

    expect(error).toBeNull();
    expect(section.comments.value[0].likes).toEqual([{ username: "Other" }]);
  });

  it("hands a refused unlike back and keeps the like", async () => {
    const answer = refusal("Вы еще не ставили лайк");
    const section = createCommentSection(
      adapter({
        unlikeComment: vi.fn().mockResolvedValue({ data: null, error: answer }),
      }),
    );
    section.comments.value = [comment([{ username: "Reader" }])];

    const { error } = await section.unlikeComment("c-1");

    expect(error).toBe(answer);
    expect(section.comments.value[0].likes).toEqual([{ username: "Reader" }]);
  });
});
