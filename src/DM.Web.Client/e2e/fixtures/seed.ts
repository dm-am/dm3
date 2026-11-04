/**
 * What the development seed puts on the site and the specs find there by
 * name: a spec that needs a seeded thing takes its name from here rather than
 * writing it down again. A page whose address the seed does not fix is found
 * through the API, as the session given to it sees the site.
 */

import { API_BASE_URL, expect } from "./auth";
import type { APIRequestContext } from "@playwright/test";

/**
 * The seeded game of SolohinLex, the primary account: its sheet, both rosters,
 * the players' and the master's, and a first ordinary room a guest reads, with
 * posts.
 */
export const MASTERED_GAME = "Хроники Забытых Королевств";

/** A seeded game TestUser plays in, with a room he writes posts in. */
const PLAYED_GAME = "Наследие Драконов";

/** The code of the first seeded game: the primary account masters it, two of its rooms closed. */
export const PRIMARY_GAME = "aaaaa";

/** The code of the seeded game TestUser, the secondary account, masters. */
export const SECONDARY_GAME = "aaaam";

/** The code of a seeded blog TestUser runs. */
export const SECONDARY_BLOG = "aaaar";

/** The token of the seeded guest ticket, the one with a known address. */
export const SEEDED_GUEST_TICKET = "5eedc0de5eedc0de5eedc0de5eedc0de";

/**
 * The id of the seeded complaint about a moderator's decision, the ticket
 * whose conversation holds a question and an answer.
 */
export async function seededTicketId(api: APIRequestContext): Promise<string> {
  const tickets = await api.get(
    `${API_BASE_URL}/v1/moderation/tickets?subtype=ModeratorDecisionComplaint`,
  );
  expect(
    tickets.ok(),
    `GET /v1/moderation/tickets answered ${tickets.status()}`,
  ).toBe(true);
  const ticket = ((await tickets.json()).resources ?? [])[0];
  expect(ticket, "the seed has no complaint about a decision").toBeTruthy();
  return ticket.id;
}

/** That ticket by the address a moderator opens it at. */
export async function seededTicket(api: APIRequestContext): Promise<string> {
  return `/moderation/tickets/${await seededTicketId(api)}`;
}

/** A seeded game by its title. */
async function gameByTitle(api: APIRequestContext, title: string) {
  const games = await api.get(`${API_BASE_URL}/v1/games?take=100`);
  expect(games.ok(), `GET /v1/games answered ${games.status()}`).toBe(true);
  const game = ((await games.json()).resources ?? []).find(
    (g: { title: string }) => g.title === title,
  );
  expect(game, `the seed has no game "${title}"`).toBeTruthy();
  return game;
}

/** The longest login of the seed, twenty letters (DataSeeder.Users.cs). */
const LONGEST_LOGIN = "LongestLoginPossible";

/** The details of a seeded game the account with the longest login masters. */
export async function longestLoginGame(
  api: APIRequestContext,
): Promise<string> {
  const games = await api.get(`${API_BASE_URL}/v1/games?take=100`);
  expect(games.ok(), `GET /v1/games answered ${games.status()}`).toBe(true);
  const game = ((await games.json()).resources ?? []).find(
    (g: { master?: { username: string } }) =>
      g.master?.username === LONGEST_LOGIN,
  );
  expect(game, `the seed has no game ${LONGEST_LOGIN} masters`).toBeTruthy();
  return `/game/${game.publicId ?? game.id}`;
}

/** The settings of the game the administrator of the seed is the master of. */
export async function masteredGameSettings(
  api: APIRequestContext,
): Promise<string> {
  const game = await gameByTitle(api, MASTERED_GAME);
  return `/game/${game.publicId ?? game.id}/settings`;
}

/** The first ordinary room of the game the administrator masters, by its address. */
export async function masteredGameRoom(
  api: APIRequestContext,
): Promise<string> {
  const game = await gameByTitle(api, MASTERED_GAME);
  const rooms = await api.get(`${API_BASE_URL}/v1/games/${game.id}/rooms`);
  expect(rooms.ok(), `GET rooms answered ${rooms.status()}`).toBe(true);
  const room = ((await rooms.json()).resources ?? []).find(
    (r: { type: string }) => r.type === "Default",
  );
  expect(room, `"${MASTERED_GAME}" has no ordinary room`).toBeTruthy();
  return `/game/${game.publicId ?? game.id}/rooms/${room.roomNumber}`;
}

/** The first template of a sheet in the seed, by the address it is edited at. */
export async function seededSchemaTemplate(
  api: APIRequestContext,
): Promise<string> {
  const templates = await api.get(
    `${API_BASE_URL}/v1/attribute-schema-templates`,
  );
  expect(
    templates.ok(),
    `GET /v1/attribute-schema-templates answered ${templates.status()}`,
  ).toBe(true);
  const template = ((await templates.json()).resources ?? [])[0];
  expect(template, "the seed has no template of a sheet").toBeTruthy();
  return `/moderation/schema-templates/${template.id}`;
}

/** The first room of the game TestUser plays in, by its address. */
export async function playedRoom(api: APIRequestContext): Promise<string> {
  const game = await gameByTitle(api, PLAYED_GAME);
  return `/game/${game.publicId ?? game.id}/rooms/1`;
}
