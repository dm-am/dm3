import { request } from "@playwright/test";
import { join } from "path";
import { findPlayedGame } from "./played-game.mjs";

/**
 * The few things a state needs that the seed names by a GUID rather than by a
 * public id or a name: they are looked up once per run through the API, so a
 * state reads "the member's character in the game he plays" instead of a
 * GUID that moves whenever the seeder draws one value more.
 *
 * Everything else the states open is addressed the way a reader addresses it
 * (a game's public id, a board's alias, a username), which the pinned seed
 * keeps in place.
 */
export interface Fixture {
  /** The public id of a game the member plays in (played-game.mjs). */
  playedGame: string;
  /** The member's character in that game. */
  characterId: string;
  /** When the member's last post in that game's first room was written. */
  ownPostCreatedUtc: string;
  /** The order number of a chat room of that game the member sees. */
  chatRoomNumber: number;
  /** The member's private conversation. */
  chatId: string;
  /** A ticket a moderator can open. */
  ticketId: string;
  /** The member's blog. */
  blogId: string;
}

/** The account the member is. */
const MEMBER = "TestUser";

async function as(api: string, directory: string, role: string) {
  const context = await request.newContext({
    storageState: join(directory, `${role}.json`),
  });
  return {
    async get<T>(path: string): Promise<T> {
      const response = await context.get(`${api}${path}`);
      if (!response.ok())
        throw new Error(`GET ${path} as ${role}: ${response.status()}`);
      return (await response.json()) as T;
    },
    dispose: () => context.dispose(),
  };
}

interface List<T> {
  resources: T[];
}

export async function resolveFixture(
  api: string,
  directory: string,
): Promise<Fixture> {
  const member = await as(api, directory, "user");
  const moderator = await as(api, directory, "moderator");
  try {
    const played = await findPlayedGame(member.get, MEMBER);

    const chats = await member.get<List<{ id: string }>>("/v1/chats");
    const tickets = await moderator.get<List<{ id: string }>>(
      "/v1/moderation/tickets?take=1",
    );
    const blog = await member.get<{ resource: { publicId: string } }>(
      `/v1/blogs/owner/${MEMBER}`,
    );

    return {
      playedGame: played.game,
      characterId: played.characterId,
      ownPostCreatedUtc: played.ownPostCreatedUtc,
      chatRoomNumber: played.chatRoomNumber,
      chatId: chats.resources[0].id,
      ticketId: tickets.resources[0].id,
      blogId: blog.resource.publicId,
    };
  } finally {
    await member.dispose();
    await moderator.dispose();
  }
}
