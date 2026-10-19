/**
 * Who edits and deletes a message: the author within the time limit, and a moderator only where
 * moderation reaches the chat. The global chat is moderated; a conversation and the chat of a
 * game room are not, so a moderator has no pencil and no trash on a message of somebody else there.
 */
import { describe, it, expect } from "vitest";
import { ref } from "vue";
import { UserRole } from "@/shared/api/models/common";
import type { Message } from "@/shared/api/models/common/message";
import { useMessagePermissions } from "./useMessagePermissions";

const message = (author: string, minutesAgo = 600): Message =>
  ({
    id: "m1",
    createdUtc: new Date(Date.now() - minutesAgo * 60_000).toISOString(),
    modifiedUtc: null,
    author: { username: author },
    text: "Текст",
    isRemoved: false,
    deletedBy: null,
    deletedUtc: null,
    likes: [],
    edits: [],
  }) as unknown as Message;

const moderator = ref({ username: "Модератор", role: UserRole.Moderator });

describe("useMessagePermissions", () => {
  it("lets a moderator edit and delete a message of somebody else in a moderated chat", () => {
    const { canEdit, canDelete } = useMessagePermissions(moderator, {
      moderation: {},
    });

    expect(canEdit(message("Петя"))).toBe(true);
    expect(canDelete(message("Петя"))).toBe(true);
  });

  it("gives a moderator nothing on a message of somebody else where the chat is not moderated", () => {
    const { canEdit, canDelete } = useMessagePermissions(moderator, {});

    expect(canEdit(message("Петя"))).toBe(false);
    expect(canDelete(message("Петя"))).toBe(false);
  });

  // Rooms and private correspondence are not moderated, so a removed message there stays shut to him.
  it("opens a removed message to a moderator only in a moderated chat", () => {
    const regular = ref({ username: "Петя", role: UserRole.RegularUser });

    expect(
      useMessagePermissions(moderator, { moderation: {} }).revealsRemoved.value,
    ).toBe(true);
    expect(useMessagePermissions(moderator, {}).revealsRemoved.value).toBe(
      false,
    );
    expect(
      useMessagePermissions(regular, { moderation: {} }).revealsRemoved.value,
    ).toBe(false);
  });

  it("lets the author edit his own message within the time limit, in either chat", () => {
    const author = ref({ username: "Петя", role: UserRole.RegularUser });

    for (const feed of [{}, { moderation: {} }]) {
      const { canEdit } = useMessagePermissions(author, feed);
      expect(canEdit(message("Петя", 1))).toBe(true);
      expect(canEdit(message("Петя"))).toBe(false);
    }
  });
});
