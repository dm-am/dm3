// How the API snapshot fills the path parameters of the GET routes: the ids
// are looked up through the API's own lists once per run, so a route is asked
// about something that exists in the seed without naming a GUID the seeder
// may draw differently tomorrow. Where the seed has nothing of a kind (an
// empty notepad), the route is asked about an id that does not exist, and
// its refusal is what the snapshot keeps.

import { findPlayedGame } from "./played-game.mjs";

/** An id no seeded row has. */
const NONE = "00000000-0000-4000-8000-000000000000";

/** The seeded accounts and games the lookups start from (DataSeeder.*.cs). */
const MEMBER = "TestUser";
const SUBJECT = "SolohinLex";
const REVIEWED_GAME = "aaaaa";
const BIG_POST_GAME = "aaabp";

async function firstId(
  session,
  path,
  pick = (body) => body?.resources?.[0]?.id,
) {
  const response = await session.request("GET", path);
  if (!response.ok) return NONE;
  return pick(await response.json()) ?? NONE;
}

/**
 * Looks the ids up as the member (whose own notepads, chats and subscriptions
 * they are) and as the administrator (who sees every moderation list; the
 * moderator of the snapshot is refused some of them, and that refusal is part
 * of what the snapshot keeps).
 */
export async function discover(member, admin) {
  const blog = await member.json(`/v1/blogs/owner/${MEMBER}`);
  const blogId = blog.resource.publicId;
  const boards = await member.json("/v1/boards");
  const boardId = boards.resources[0].id;
  const topics = await member.json(`/v1/boards/${boardId}/topics?take=1`);
  const topic = topics.resources[0];

  const played = await findPlayedGame((path) => member.json(path), MEMBER);
  const { characterId, roomId } = played;

  const bigRooms = await member.json(`/v1/games/${BIG_POST_GAME}/rooms`);
  const bigRoomId = bigRooms.resources.find((r) => r.roomNumber === 1).id;
  const postId = await firstId(member, `/v1/rooms/${bigRoomId}/posts?take=1`);

  const chatId = await firstId(member, "/v1/chats");
  const publicationId = await firstId(
    member,
    `/v1/blogs/${blogId}/publications`,
    (b) => b?.resources?.find((p) => p.commentCount > 0)?.id,
  );
  const publicationCommentId = await firstId(
    member,
    `/v1/publications/${publicationId}/comments?take=1`,
  );
  const tagGroupId = await firstId(admin, "/v1/moderation/tags/groups");

  return {
    member: MEMBER,
    subject: SUBJECT,
    game: played.game,
    reviewedGame: REVIEWED_GAME,
    blogId,
    boardId,
    topicId: topic.id,
    forumAlias: boards.resources[0].alias,
    topicNumber: topic.topicNumber,
    characterId,
    roomId,
    postId,
    chatId,
    publicationId,
    tagGroupId,
    schemaTemplateId: await firstId(member, "/v1/attribute-schema-templates"),
    blogNotepadEntryId: await firstId(member, `/v1/blogs/${blogId}/notepad`),
    // A comment of a publication is what the blog comment routes address.
    blogCommentId: publicationCommentId,
    publicationCommentId,
    forumCommentId: await firstId(
      member,
      `/v1/topics/${topic.id}/comments?take=1`,
    ),
    characterNotepadEntryId: await firstId(
      member,
      `/v1/characters/${characterId}/notepad`,
    ),
    masterNotepadEntryId: await firstId(
      member,
      `/v1/characters/${characterId}/master-notepad`,
    ),
    chatRoomId: played.chatRoomId,
    gameCommentId: await firstId(
      member,
      `/v1/games/${played.game}/comments?take=1`,
    ),
    gameNotepadEntryId: await firstId(
      member,
      `/v1/games/${played.game}/notepad`,
    ),
    gameReviewId: await firstId(
      member,
      `/v1/games/${REVIEWED_GAME}/reviews?take=1`,
    ),
    postReviewId: await firstId(member, `/v1/posts/${postId}/reviews?take=1`),
    endorsementId: await firstId(
      member,
      `/v1/users/${SUBJECT}/endorsements?take=1`,
    ),
    pollId: await firstId(member, "/v1/polls?take=1"),
    testimonialId: await firstId(member, "/v1/testimonials?take=1"),
    globalMessageId: await firstId(
      member,
      "/v1/global-chat/messages?limit=1",
      (b) => b?.resources?.[0]?.id ?? b?.messages?.[0]?.id,
    ),
    globalEventId: await firstId(member, "/v1/global-chat/events"),
    messageId: await firstId(
      member,
      `/v1/chats/${chatId}/messages?limit=1`,
      (b) => b?.resources?.[0]?.id ?? b?.messages?.[0]?.id,
    ),
    contestSeriesId: await firstId(admin, "/v1/moderation/contest-series"),
    moderationNoteId: await firstId(
      admin,
      `/v1/moderation/users/${SUBJECT}/notes`,
    ),
    tagId: await firstId(
      admin,
      `/v1/moderation/tags/groups/${tagGroupId}/tags`,
    ),
    ticketId: await firstId(admin, "/v1/moderation/tickets?take=1"),
    usernameChangeId: await firstId(admin, "/v1/moderation/username-changes"),
    uploadId: await firstId(admin, "/v1/uploads?allUsers=true&take=1"),
    subscriptionId: await firstId(member, "/v1/users/me/subscriptions"),
    personalNotepadEntryId: await firstId(member, "/v1/users/me/notepad"),
  };
}

/**
 * The path parameters of every GET route that has any, by route. A GET route
 * added to the API without an entry here is reported by the snapshot as not
 * asked, so this table cannot fall behind unnoticed.
 */
export const PARAMETERS = {
  "/v1/attribute-schema-templates/{id}": (f) => ({ id: f.schemaTemplateId }),
  "/v1/blogs/comments/{id}": (f) => ({ id: f.blogCommentId }),
  "/v1/blogs/owner/{login}": (f) => ({ login: f.member }),
  "/v1/blogs/{blogId}/notepad": (f) => ({ blogId: f.blogId }),
  "/v1/blogs/{blogId}/notepad/{entryId}": (f) => ({
    blogId: f.blogId,
    entryId: f.blogNotepadEntryId,
  }),
  "/v1/blogs/{blogId}/publications": (f) => ({ blogId: f.blogId }),
  "/v1/blogs/{id}": (f) => ({ id: f.blogId }),
  "/v1/blogs/{id}/blacklist": (f) => ({ id: f.blogId }),
  "/v1/blogs/{id}/comments": (f) => ({ id: f.blogId }),
  "/v1/blogs/{id}/invitations": (f) => ({ id: f.blogId }),
  "/v1/blogs/{id}/readers": (f) => ({ id: f.blogId }),
  "/v1/blogs/{id}/users": (f) => ({ id: f.blogId }),
  "/v1/blogs/{id}/users/assistants": (f) => ({ id: f.blogId }),
  "/v1/boards/{id}": (f) => ({ id: f.boardId }),
  "/v1/boards/{id}/moderators": (f) => ({ id: f.boardId }),
  "/v1/boards/{id}/topics": (f) => ({ id: f.boardId }),
  "/v1/characters/{id}": (f) => ({ id: f.characterId }),
  "/v1/characters/{id}/master-notepad": (f) => ({ id: f.characterId }),
  "/v1/characters/{id}/master-notepad/{entryId}": (f) => ({
    id: f.characterId,
    entryId: f.masterNotepadEntryId,
  }),
  "/v1/characters/{id}/notepad": (f) => ({ id: f.characterId }),
  "/v1/characters/{id}/notepad/{entryId}": (f) => ({
    id: f.characterId,
    entryId: f.characterNotepadEntryId,
  }),
  "/v1/chat-rooms/{id}": (f) => ({ id: f.chatRoomId }),
  "/v1/chat-rooms/{id}/messages": (f) => ({ id: f.chatRoomId }),
  "/v1/chats/{id}": (f) => ({ id: f.chatId }),
  "/v1/chats/{id}/messages": (f) => ({ id: f.chatId }),
  "/v1/endorsements/{id}": (f) => ({ id: f.endorsementId }),
  "/v1/forum/comments/{id}": (f) => ({ id: f.forumCommentId }),
  "/v1/forum/{alias}/{num}": (f) => ({
    alias: f.forumAlias,
    num: f.topicNumber,
  }),
  "/v1/forum/{alias}/{num}/comments/first-unread": (f) => ({
    alias: f.forumAlias,
    num: f.topicNumber,
  }),
  "/v1/games/comments/{id}": (f) => ({ id: f.gameCommentId }),
  "/v1/games/{id}": (f) => ({ id: f.game }),
  "/v1/games/{id}/blacklist": (f) => ({ id: f.game }),
  "/v1/games/{id}/characters": (f) => ({ id: f.game }),
  "/v1/games/{id}/chat-rooms": (f) => ({ id: f.game }),
  "/v1/games/{id}/comments": (f) => ({ id: f.game }),
  "/v1/games/{id}/comments/first-unread": (f) => ({ id: f.game }),
  "/v1/games/{id}/details": (f) => ({ id: f.game }),
  "/v1/games/{id}/invitations": (f) => ({ id: f.game }),
  "/v1/games/{id}/notepad": (f) => ({ id: f.game }),
  "/v1/games/{id}/notepad/{entryId}": (f) => ({
    id: f.game,
    entryId: f.gameNotepadEntryId,
  }),
  "/v1/games/{id}/posts/first-unread": (f) => ({ id: f.game }),
  "/v1/games/{id}/readers": (f) => ({ id: f.game }),
  "/v1/games/{id}/reviews": (f) => ({ id: f.reviewedGame }),
  "/v1/games/{id}/reviews/{reviewId}": (f) => ({
    id: f.reviewedGame,
    reviewId: f.gameReviewId,
  }),
  "/v1/games/{id}/rooms": (f) => ({ id: f.game }),
  "/v1/games/{id}/users": (f) => ({ id: f.game }),
  "/v1/games/{id}/users/assistants": (f) => ({ id: f.game }),
  "/v1/global-chat/events/{id}": (f) => ({ id: f.globalEventId }),
  "/v1/global-chat/events/{id}/participants": (f) => ({ id: f.globalEventId }),
  "/v1/global-chat/messages/{id}": (f) => ({ id: f.globalMessageId }),
  "/v1/leaderboards/{year}": () => ({ year: 2026 }),
  "/v1/leaderboards/{year}/{month}": () => ({ year: 2026, month: 6 }),
  "/v1/messages/{id}": (f) => ({ id: f.messageId }),
  "/v1/moderation/contest-series/{id}/awards": (f) => ({
    id: f.contestSeriesId,
  }),
  "/v1/moderation/notes/{id}": (f) => ({ id: f.moderationNoteId }),
  "/v1/moderation/tags/groups/{groupId}": (f) => ({ groupId: f.tagGroupId }),
  "/v1/moderation/tags/groups/{groupId}/tags": (f) => ({
    groupId: f.tagGroupId,
  }),
  "/v1/moderation/tags/{tagId}": (f) => ({ tagId: f.tagId }),
  "/v1/moderation/tickets/{ticketId}": (f) => ({ ticketId: f.ticketId }),
  "/v1/moderation/username-changes/{id}": (f) => ({ id: f.usernameChangeId }),
  "/v1/moderation/users/{username}/notes": (f) => ({ username: f.subject }),
  "/v1/moderation/users/{username}/profile": (f) => ({ username: f.subject }),
  "/v1/polls/{id}": (f) => ({ id: f.pollId }),
  "/v1/posts/{id}": (f) => ({ id: f.postId }),
  "/v1/posts/{postId}/reviews": (f) => ({ postId: f.postId }),
  "/v1/posts/{postId}/reviews/{reviewId}": (f) => ({
    postId: f.postId,
    reviewId: f.postReviewId,
  }),
  "/v1/publications/comments/{id}": (f) => ({ id: f.publicationCommentId }),
  "/v1/publications/{id}": (f) => ({ id: f.publicationId }),
  "/v1/publications/{id}/comments": (f) => ({ id: f.publicationId }),
  "/v1/rooms/{id}": (f) => ({ id: f.roomId }),
  "/v1/rooms/{id}/posts": (f) => ({ id: f.roomId }),
  "/v1/rooms/{id}/posts/first-unread": (f) => ({ id: f.roomId }),
  "/v1/testimonials/{id}": (f) => ({ id: f.testimonialId }),
  "/v1/topics/{id}": (f) => ({ id: f.topicId }),
  "/v1/topics/{id}/comments": (f) => ({ id: f.topicId }),
  "/v1/topics/{id}/discussion": (f) => ({ id: f.topicId }),
  "/v1/uploads/{id}": (f) => ({ id: f.uploadId }),
  "/v1/uploads/{id}/content": (f) => ({ id: f.uploadId }),
  "/v1/users/me/notepad/{id}": (f) => ({ id: f.personalNotepadEntryId }),
  "/v1/users/me/notes/{username}": (f) => ({ username: f.subject }),
  "/v1/users/me/subscriptions/{id}": (f) => ({ id: f.subscriptionId }),
  "/v1/users/{username}": (f) => ({ username: f.subject }),
  "/v1/users/{username}/achievements": (f) => ({ username: f.subject }),
  "/v1/users/{username}/awards": (f) => ({ username: f.subject }),
  "/v1/users/{username}/bans": (f) => ({ username: f.subject }),
  "/v1/users/{username}/bans/active": (f) => ({ username: f.subject }),
  "/v1/users/{username}/best-publication": (f) => ({ username: f.subject }),
  "/v1/users/{username}/best-topic": (f) => ({ username: f.subject }),
  "/v1/users/{username}/endorsements": (f) => ({ username: f.subject }),
  "/v1/users/{username}/endorsements/eligibility": (f) => ({
    username: f.subject,
  }),
  "/v1/users/{username}/game-reviews": (f) => ({ username: f.subject }),
  "/v1/users/{username}/login-history": (f) => ({ username: f.subject }),
  "/v1/users/{username}/profile": (f) => ({ username: f.subject }),
  "/v1/users/{username}/subscribers": (f) => ({ username: f.subject }),
  "/v1/users/{username}/subscribers/me": (f) => ({ username: f.subject }),
  "/v1/users/{username}/warnings": (f) => ({ username: f.subject }),
  "/v1/users/{username}/written-endorsements": (f) => ({ username: f.subject }),
  "/v1/users/{username}/written-game-reviews": (f) => ({ username: f.subject }),
};

/** The query a route cannot be asked without (the API answers 400 to none). */
export const QUERIES = {
  "/v1/search/forum": { search: "правила" },
  "/v1/search/messages": { search: "привет" },
};
