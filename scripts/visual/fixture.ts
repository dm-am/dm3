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
  /** The nameless figure of the member's game. */
  npcId: string;
  /** The first active character with a player in the administrator's game. */
  uiCharacterId: string;
  /** The page of a topic the member has a comment in. */
  ownCommentTopicUrl: string;
  /** When the member's first comment on that page was written. */
  ownCommentCreatedUtc: string;
  /** An account that plays in a game and masters none. */
  playerOnlyUsername: string;
  /** When the administrator endorsed the senior moderator. */
  endorsementCreatedUtc: string;
}

/** The account the member is. */
const MEMBER = "TestUser";

/** The account the administrator is. */
const ADMIN = "SolohinLex";

/** The game the administrator masters, and the one the member does (DataSeeder.Games.cs). */
const ADMIN_GAME = "aaaaa";
const MEMBER_GAME = "aaaam";

/** The FAQ of the newbies' board, the topic of the seed with the most comments. */
const FAQ = { alias: "newbies", number: 3 };

async function asRole(api: string, directory: string, role: string) {
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

type Get = <T>(path: string) => Promise<T>;

interface List<T> {
  resources: T[];
}

interface Character {
  id: string;
  status: string;
  isNpc: boolean;
  author?: { username: string };
}

interface Topic {
  id: string;
  topicNumber: number;
}

/** The first item that answers the question, or a refusal naming what was looked for. */
function first<T>(items: T[], matches: (item: T) => boolean, what: string): T {
  const found = items.find(matches);
  if (found === undefined) throw new Error(`the seed has no ${what}`);
  return found;
}

/** The characters of the two games the states open by public id. */
async function characters(admin: Get, member: Get) {
  const adminGame = await admin<List<Character>>(
    `/v1/games/${ADMIN_GAME}/characters`,
  );
  const memberGame = await member<List<Character>>(
    `/v1/games/${MEMBER_GAME}/characters`,
  );
  return {
    uiCharacterId: first(
      adminGame.resources,
      (c) => c.status === "Active" && !!c.author,
      `active character with a player in ${ADMIN_GAME}`,
    ).id,
    npcId: first(
      memberGame.resources,
      (c) => c.isNpc,
      `nameless figure in ${MEMBER_GAME}`,
    ).id,
  };
}

/**
 * The member's first comment in a topic: the FAQ first, then every topic of
 * every board, since who writes a comment is a draw of the seed.
 */
async function ownComment(member: Get) {
  const faq = await member<{ resource: Topic }>(
    `/v1/forum/${FAQ.alias}/${FAQ.number}`,
  );
  const candidates = [{ alias: FAQ.alias, topic: faq.resource }];
  const boards =
    await member<List<{ id: string; alias: string }>>("/v1/boards");
  for (const board of boards.resources) {
    const topics = await member<List<Topic>>(
      `/v1/boards/${board.id}/topics?take=100`,
    );
    for (const topic of topics.resources)
      if (topic.id !== faq.resource.id)
        candidates.push({ alias: board.alias, topic });
  }
  for (const { alias, topic } of candidates) {
    const comments = await member<
      List<{ createdUtc: string; author?: { username: string } }>
    >(`/v1/topics/${topic.id}/comments?take=100`);
    const own = comments.resources.find((c) => c.author?.username === MEMBER);
    if (own)
      return {
        ownCommentTopicUrl: `/forum/${alias}/${topic.topicNumber}`,
        ownCommentCreatedUtc: own.createdUtc,
      };
  }
  throw new Error(`the seed has no comment of ${MEMBER} in a topic`);
}

/** The first account, of any activity, that plays in a game and masters none. */
async function playerOnly(member: Get): Promise<string> {
  type User = { username: string; gamesHosting: number; gamesPlaying: number };
  for (let skip = 0; ; skip += 100) {
    const page = await member<List<User>>(
      `/v1/users?take=100&skip=${skip}&activity=All`,
    );
    const found = page.resources.find(
      (u) => u.gamesHosting === 0 && u.gamesPlaying > 0,
    );
    if (found) return found.username;
    if (page.resources.length < 100)
      throw new Error("the seed has no account that plays and masters none");
  }
}

/** When the administrator endorsed the senior moderator (DataSeeder.UserProfile.cs). */
async function endorsementCreatedUtc(admin: Get): Promise<string> {
  const endorsements = await admin<
    List<{ createdUtc: string; author?: { username: string } }>
  >("/v1/users/TestSeniorMod/endorsements");
  return first(
    endorsements.resources,
    (e) => e.author?.username === ADMIN,
    `endorsement of TestSeniorMod by ${ADMIN}`,
  ).createdUtc;
}

export async function resolveFixture(
  api: string,
  directory: string,
): Promise<Fixture> {
  const member = await asRole(api, directory, "user");
  const moderator = await asRole(api, directory, "moderator");
  const admin = await asRole(api, directory, "admin");
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
      ...(await characters(admin.get, member.get)),
      ...(await ownComment(member.get)),
      playerOnlyUsername: await playerOnly(member.get),
      endorsementCreatedUtc: await endorsementCreatedUtc(admin.get),
    };
  } finally {
    await member.dispose();
    await moderator.dispose();
    await admin.dispose();
  }
}
