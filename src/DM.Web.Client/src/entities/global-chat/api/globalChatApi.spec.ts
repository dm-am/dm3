/**
 * @vitest-environment node
 */

/**
 * A single reply of the global chat is addressed under the global chat.
 *
 * These seven requests used to go to `messages/{id}` — the endpoint of private
 * correspondence, which reads a message only for a participant of its chat.
 * The global chat has no participants, because the right to read it belongs to
 * everyone signed in, so all seven answered 404, including on the reader's own
 * line: editing, liking, deleting, quoting and following a link to a message
 * were dead in the chat.
 *
 * The URL is the whole of what is asserted here, because the URL was the whole
 * of the defect.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", () => ({
  Api: {
    get: vi.fn().mockResolvedValue({ data: null, error: null }),
    post: vi.fn().mockResolvedValue({ data: null, error: null }),
    patch: vi.fn().mockResolvedValue({ data: null, error: null }),
    delete: vi.fn().mockResolvedValue({ data: null, error: null }),
  },
  RENDER_AUDIENCE: { AuthorEdit: "author_edit", Display: "display" },
}));

import { Api, RENDER_AUDIENCE } from "@/shared/api";
import globalChatApi from "./globalChatApi";

const messageId = "1f6f0b3c-0000-0000-0000-00000000abcd";

describe("globalChatApi single message operations", () => {
  beforeEach(() => {
    vi.mocked(Api.get).mockClear();
    vi.mocked(Api.post).mockClear();
    vi.mocked(Api.patch).mockClear();
    vi.mocked(Api.delete).mockClear();
  });

  it("reads one reply from the global chat", async () => {
    await globalChatApi.getMessage(messageId);

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith(
      `global-chat/messages/${messageId}`,
    );
  });

  it("asks for the author's own markup with the author audience", async () => {
    await globalChatApi.getMessageForEdit(messageId);

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith(
      `global-chat/messages/${messageId}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  });

  it("edits a reply of the global chat", async () => {
    await globalChatApi.updateMessage(messageId, "Новый текст");

    expect(vi.mocked(Api.patch)).toHaveBeenCalledWith(
      `global-chat/messages/${messageId}`,
      { text: "Новый текст" },
    );
  });

  it("deletes a reply of the global chat", async () => {
    await globalChatApi.deleteMessage(messageId);

    expect(vi.mocked(Api.delete)).toHaveBeenCalledWith(
      `global-chat/messages/${messageId}`,
    );
  });

  it("likes a reply of the global chat", async () => {
    await globalChatApi.likeMessage(messageId);

    expect(vi.mocked(Api.post)).toHaveBeenCalledWith(
      `global-chat/messages/${messageId}/likes`,
    );
  });

  it("takes the like back", async () => {
    await globalChatApi.unlikeMessage(messageId);

    expect(vi.mocked(Api.delete)).toHaveBeenCalledWith(
      `global-chat/messages/${messageId}/likes`,
    );
  });

  it("asks for the markup of a quotation of a reply", async () => {
    await globalChatApi.getMessageQuote(messageId);

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith(
      `global-chat/messages/${messageId}/quote`,
    );
  });
});
