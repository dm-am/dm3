import { request, type APIRequestContext } from "@playwright/test";
import { API_BASE_URL, test, expect } from "../../fixtures/auth";

/**
 * A file of a post kept to the characters chosen ("видят: Вася" in its line): a
 * reader the file is not for gets the post without it and the file's address
 * as a file that is not there, the author gets it with its recipients. The unit tests hold the client's half and
 * the server's own tests the rule; this is the two together on a real stand,
 * the mark sent the way the form sends it, with the file in one request.
 */

/** A picture of one pixel: a post attachment has no floor of size. */
const PNG_1X1 = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
  "base64",
);

interface Attachment {
  id: string;
  recipients?: string;
}

interface PostOfRoom {
  id: string;
  attachments?: Attachment[];
}

/**
 * An open room of posts in a game the signed-in account leads, which a guest
 * reads: the lead writes a post in it as the master, and the guest is the
 * reader the file is kept from. Resolved against the stand, not pinned.
 */
async function resolveOpenRoom(
  api: APIRequestContext,
  guest: APIRequestContext,
): Promise<string> {
  const gamesResponse = await api.get(`${API_BASE_URL}/v1/games?take=50`);
  expect(gamesResponse.ok()).toBeTruthy();
  const { resources: games } = await gamesResponse.json();

  for (const game of games ?? []) {
    if (!(game.participation ?? []).includes("Authority")) continue;
    const roomsResponse = await api.get(
      `${API_BASE_URL}/v1/games/${game.id}/rooms`,
    );
    if (!roomsResponse.ok()) continue;
    const { resources: rooms } = await roomsResponse.json();
    for (const room of rooms ?? []) {
      if (room.type !== "Default" || room.access !== "Open" || room.isArchived)
        continue;
      const probe = await guest.get(
        `${API_BASE_URL}/v1/rooms/${room.id}/posts?take=1`,
      );
      if (probe.ok()) return room.id;
    }
  }

  throw new Error(
    "No game on this stand that the signed-in account leads has an open room of posts a guest reads. " +
      "Is the database seeded (powershell -File .\\scripts\\dm.ps1 seed)?",
  );
}

/** The post as the given session reads it. */
async function postAs(
  reader: APIRequestContext,
  postId: string,
): Promise<PostOfRoom> {
  const response = await reader.get(`${API_BASE_URL}/v1/posts/${postId}`);
  expect(response.ok()).toBeTruthy();
  return (await response.json()).resource;
}

test.describe("who sees a file of a post", () => {
  let guest: APIRequestContext;

  test.beforeAll(async () => {
    guest = await request.newContext();
  });

  test.afterAll(async () => {
    await guest?.dispose();
  });

  test("keeps a file kept to chosen characters from a reader and shows it to its author", async ({
    authContext: api,
  }) => {
    const roomId = await resolveOpenRoom(api, guest);

    const created = await api.post(`${API_BASE_URL}/v1/rooms/${roomId}/posts`, {
      data: { gameText: "Пост с картой для одних ведущих" },
    });
    expect(created.status()).toBe(201);
    const postId: string = (await created.json()).resource.id;

    try {
      // No characters checked: the author and the leads, as a bare hidden block.
      const uploaded = await api.post(
        `${API_BASE_URL}/v1/uploads?type=PostAttachment&targetId=${postId}`,
        {
          multipart: {
            file: { name: "map.png", mimeType: "image/png", buffer: PNG_1X1 },
            visibleTo: "Selected",
          },
        },
      );
      expect(uploaded.status()).toBe(201);
      const uploadId: string = (await uploaded.json()).id;

      const asAuthor = await postAs(api, postId);
      expect(asAuthor.attachments?.map((a) => a.id)).toEqual([uploadId]);
      expect(asAuthor.attachments?.[0].recipients).toBe(
        "DungeonMaster, Assistant",
      );

      const asGuest = await postAs(guest, postId);
      expect(asGuest.attachments ?? []).toEqual([]);

      const content = `${API_BASE_URL}/v1/uploads/${uploadId}/content`;
      expect((await api.get(content)).status()).toBe(200);
      expect((await guest.get(content)).status()).toBe(404);
    } finally {
      await api.delete(`${API_BASE_URL}/v1/posts/${postId}`);
    }
  });
});
