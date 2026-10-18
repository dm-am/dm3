// The game the member plays in, looked up instead of named: which accounts
// play which game is drawn by the seed's generator, and its draws depend on
// the epoch the seed is laid out around. On a stand this tool rebuilt (pinned
// epoch) the member plays Сага о Викингах; on the running stand, seeded from
// the day it was seeded, he plays other games.

/** The game of the pinned seed (DataSeeder.Games.cs), tried first. */
const PREFERRED = "aaaav";

/**
 * The first game, by public id with the preferred one ahead, in which the
 * member has an active character, is not the master, has a post in the first
 * room and sees a chat room: what the member's game states open. Public ids
 * and not the list's own order, which follows activity and moves with it.
 *
 * @param {(path: string) => Promise<any>} get a GET as the member
 * @param {string} member the member's username
 */
export async function findPlayedGame(get, member) {
  const games = [];
  for (let skip = 0; ; skip += 100) {
    const page = await get(`/v1/games?take=100&skip=${skip}`);
    games.push(...page.resources);
    if (page.resources.length < 100) break;
  }
  const ids = games
    .filter((game) => game.master?.username !== member)
    .map((game) => game.publicId)
    .sort();
  const candidates = [PREFERRED, ...ids.filter((id) => id !== PREFERRED)];
  for (const game of candidates) {
    const characters = await get(`/v1/games/${game}/characters`);
    const character = characters.resources.find(
      (c) => c.author?.username === member && c.status === "Active",
    );
    if (!character) continue;
    const rooms = await get(`/v1/games/${game}/rooms`);
    const room = rooms.resources.find((r) => r.roomNumber === 1);
    if (!room) continue;
    const posts = await get(
      `/v1/rooms/${room.id}/posts?take=100&sortOrder=desc`,
    );
    const own = posts.resources.find((p) => p.author?.username === member);
    if (!own) continue;
    const chatRooms = await get(`/v1/games/${game}/chat-rooms`);
    const chatRoom = chatRooms.resources[0];
    if (!chatRoom) continue;
    return {
      game,
      characterId: character.id,
      roomId: room.id,
      ownPostCreatedUtc: own.createdUtc,
      chatRoomId: chatRoom.id,
      chatRoomNumber: chatRoom.orderNumber,
    };
  }
  throw new Error(
    `${member} plays no game with an active character, a post in the first room and a chat room`,
  );
}
