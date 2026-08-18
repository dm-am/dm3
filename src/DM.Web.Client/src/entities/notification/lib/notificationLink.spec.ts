/**
 * Where a notification takes the reader.
 *
 * Three of them are about one publication — it appeared, somebody liked it,
 * somebody commented under it — and all three used to open the blog: the
 * publication had no page, so the reader was handed the feed and left to find
 * the text he had been told about. The publication has a page now, and the way
 * in is the resolver route: the payload names the blog in the readable-guid
 * form, and the blog is looked up by public id or guid, so the address built
 * straight out of the payload would 404. The publication endpoint takes that
 * form, which is what the resolver asks.
 *
 * The pairing with the letter about the same event is held from the server
 * side (NotificationLinkVocabularyShould); what is held here is the client's
 * own answer, including the one it gives for a payload with nothing in it.
 */
import { describe, it, expect } from "vitest";
import {
  NotificationType,
  type UserNotification,
} from "@/shared/api/models/notifications";
import { notificationLink } from "./notificationLink";

const notification = (
  eventType: NotificationType,
  payload: Record<string, unknown> | null,
) => ({ id: "n-1", eventType, payload }) as unknown as UserNotification;

const PAYLOAD = {
  publicationId: "polet~cHVi",
  blogId: "zapiski~Ymxv",
};

describe("notificationLink", () => {
  it.each([
    NotificationType.NewPublication,
    NotificationType.LikedPublication,
    NotificationType.NewPublicationComment,
  ])("takes %s to the publication itself", (eventType) => {
    expect(notificationLink(notification(eventType, PAYLOAD))).toBe(
      "/publication/polet~cHVi",
    );
  });

  it.each([
    NotificationType.NewBlogComment,
    NotificationType.LikedBlogComment,
    NotificationType.NewBlogFromSubscribedAuthor,
  ])("keeps %s on the blog", (eventType) => {
    expect(notificationLink(notification(eventType, PAYLOAD))).toBe(
      "/blogs/zapiski~Ymxv",
    );
  });

  it("links nothing when the payload names no publication", () => {
    expect(
      notificationLink(
        notification(NotificationType.NewPublication, { blogId: "zapiski" }),
      ),
    ).toBeNull();
    expect(
      notificationLink(notification(NotificationType.NewPublication, null)),
    ).toBeNull();
  });
});
