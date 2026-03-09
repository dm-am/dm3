// Game API
// Migrated from api/requests/gameApi.ts

import type { ListEnvelope, PagingQuery, Comment, User } from "@/shared/api/models/common";
import type {
  Game,
  GameUser,
  AttributeSchema,
  Tag,
  Character,
  Room,
  Post,
  Invitation,
  FeaturedPostsEnvelope,
  PostReview,
  FirstUnreadPostResult,
  FirstUnreadCommentResult,
} from "../model/types";
import { Api } from "@/shared/api";

class GameApi {
  public getOwnGames() {
    return Api.get<ListEnvelope<Game>>("games/owned");
  }

  public getSubscribedGames() {
    return Api.get<ListEnvelope<Game>>("games/subscribed");
  }

  public getModerationGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: "Draft" });
  }

  public getActiveGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: "Active" });
  }

  public getRecruitingGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: "Active", isRecruiting: true });
  }

  public getFinishedGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: "Closed", isFinished: true });
  }

  public getGamesByMaster(username: string) {
    return Api.get<ListEnvelope<Game>>("games", { masterUsername: username });
  }

  public getGamesByPlayer(username: string) {
    return Api.get<ListEnvelope<Game>>("games", { playerUsername: username });
  }

  public async getPopularGames() {
    return Api.get<ListEnvelope<Game>>("games/popular");
  }

  public getFeaturedPosts() {
    return Api.get<FeaturedPostsEnvelope>("posts/featured");
  }

  public getGame(id: string) {
    return Api.get<Game>(`games/${id}/details`);
  }

  public getCharacters(gameId: string) {
    return Api.get<ListEnvelope<Character>>(`games/${gameId}/characters`);
  }

  public getRooms(gameId: string) {
    return Api.get<ListEnvelope<Room>>(`games/${gameId}/rooms`);
  }

  public getPosts(roomId: string, paging?: PagingQuery) {
    return Api.get<ListEnvelope<Post>>(`rooms/${roomId}/posts`, paging);
  }

  public getPost(postId: string) {
    return Api.get<Post>(`posts/${postId}`);
  }

  public createPost(roomId: string, post: Partial<Post>) {
    return Api.post<Post>(`rooms/${roomId}/posts`, post);
  }

  public updatePost(postId: string, post: Partial<Post>) {
    return Api.patch<Post>(`posts/${postId}`, post);
  }

  public deletePost(postId: string) {
    return Api.delete(`posts/${postId}`);
  }

  public getPostReviews(postId: string, paging?: PagingQuery) {
    return Api.get<ListEnvelope<PostReview>>(`posts/${postId}/reviews`, paging);
  }

  // Game comments
  public getGameComments(gameId: string, paging?: PagingQuery) {
    return Api.get<ListEnvelope<Comment>>(`games/${gameId}/comments`, paging);
  }

  public createGameComment(gameId: string, comment: { text: string }) {
    return Api.post<Comment>(`games/${gameId}/comments`, comment);
  }

  public updateGameComment(commentId: string, comment: { text: string }) {
    return Api.patch<Comment>(`games/comments/${commentId}`, comment);
  }

  public deleteGameComment(commentId: string) {
    return Api.delete(`games/comments/${commentId}`);
  }

  // Game users
  public getUsers(gameId: string) {
    return Api.get<ListEnvelope<GameUser>>(`games/${gameId}/users`);
  }

  public getAssistants(gameId: string) {
    return Api.get<ListEnvelope<GameUser>>(`games/${gameId}/users/assistants`);
  }

  public getReaders(gameId: string) {
    return Api.get<ListEnvelope<User>>(`games/${gameId}/readers`);
  }

  public subscribe(id: string) {
    return Api.post<User>(`games/${id}/readers`);
  }

  public unsubscribe(id: string) {
    return Api.delete(`games/${id}/readers`);
  }

  public getSchemas() {
    return Api.get<ListEnvelope<AttributeSchema>>("schemas");
  }

  public getTags() {
    return Api.get<ListEnvelope<Tag>>("games/tags");
  }

  public createSchema(schema: AttributeSchema) {
    return Api.post<AttributeSchema>("schemas", schema);
  }

  public createGame(game: Game) {
    return Api.post<Game>("games", game);
  }

  public createCharacter(id: string, character: Character) {
    return Api.post<Character>(`games/${id}/characters`, character);
  }

  // Invitations
  public getGameInvitations(gameId: string) {
    return Api.get<ListEnvelope<Invitation>>(`games/${gameId}/invitations`);
  }

  public invitePlayer(gameId: string, username: string) {
    return Api.post<Invitation>(`games/${gameId}/invitations/players`, { username });
  }

  public inviteReader(gameId: string, username: string) {
    return Api.post<Invitation>(`games/${gameId}/invitations/readers`, { username });
  }

  public cancelInvitation(gameId: string, tokenId: string) {
    return Api.delete(`games/${gameId}/invitations/${tokenId}`);
  }

  // First unread content navigation
  public getFirstUnreadPost(gameId: string) {
    return Api.get<FirstUnreadPostResult>(`games/${gameId}/posts/first-unread`);
  }

  public getFirstUnreadComment(gameId: string) {
    return Api.get<FirstUnreadCommentResult>(`games/${gameId}/comments/first-unread`);
  }

  // Mark as read
  public markRoomAsRead(roomId: string) {
    return Api.delete(`rooms/${roomId}/posts/unread`);
  }

  public markCommentsAsRead(gameId: string) {
    return Api.delete(`games/${gameId}/comments/unread`);
  }
}

export default new GameApi();
