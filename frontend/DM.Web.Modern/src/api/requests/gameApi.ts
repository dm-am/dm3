import type { Envelope, ListEnvelope, PagingQuery } from "@/api/models/common";
import type {
  Game,
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
} from "@/api/models/game";
import type { Comment } from "@/api/models/forum";
import type { User } from "@/api/models/community";
import Api from "@/api";

export default new (class {
  public getOwnGames() {
    return Api.get<ListEnvelope<Game>>("games/owned");
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

  public getGamesByMaster(login: string) {
    return Api.get<ListEnvelope<Game>>("games", { masterLogin: login });
  }

  public getGamesByPlayer(login: string) {
    return Api.get<ListEnvelope<Game>>("games", { playerLogin: login });
  }

  public async getPopularGames() {
    return Api.get<ListEnvelope<Game>>("games/popular");
  }

  public getFeaturedPosts() {
    return Api.get<FeaturedPostsEnvelope>("posts/featured");
  }

  public getGame(id: string) {
    return Api.get<Envelope<Game>>(`games/${id}/details`);
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
    return Api.get<Envelope<Post>>(`posts/${postId}`);
  }

  public createPost(roomId: string, post: Partial<Post>) {
    return Api.post<Envelope<Post>>(`rooms/${roomId}/posts`, post);
  }

  public updatePost(postId: string, post: Partial<Post>) {
    return Api.patch<Envelope<Post>>(`posts/${postId}`, post);
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
    return Api.post<Envelope<Comment>>(`games/${gameId}/comments`, comment);
  }

  public updateGameComment(commentId: string, comment: { text: string }) {
    return Api.patch<Envelope<Comment>>(`games/comments/${commentId}`, comment);
  }

  public deleteGameComment(commentId: string) {
    return Api.delete(`games/comments/${commentId}`);
  }

  public getReaders(gameId: string) {
    return Api.get<ListEnvelope<User>>(`games/${gameId}/readers`);
  }

  public getSchemas() {
    return Api.get<ListEnvelope<AttributeSchema>>("schemas");
  }

  public getTags() {
    return Api.get<ListEnvelope<Tag>>("games/tags");
  }

  public createSchema(schema: AttributeSchema) {
    return Api.post<Envelope<AttributeSchema>>("schemas", schema);
  }

  public createGame(game: Game) {
    return Api.post<Envelope<Game>>("games", game);
  }

  public createCharacter(id: string, character: Character) {
    return Api.post<Envelope<Character>>(`games/${id}/characters`, character);
  }

  public subscribe(id: string) {
    return Api.post<Envelope<User>>(`games/${id}/readers`);
  }
  public unsubscribe(id: string) {
    return Api.delete(`games/${id}/readers`);
  }

  // Invitations
  public getGameInvitations(gameId: string) {
    return Api.get<ListEnvelope<Invitation>>(`games/${gameId}/invitations`);
  }

  public invitePlayer(gameId: string, login: string) {
    return Api.post<Envelope<Invitation>>(`games/${gameId}/invitations/players`, { login });
  }

  public inviteReader(gameId: string, login: string) {
    return Api.post<Envelope<Invitation>>(`games/${gameId}/invitations/readers`, { login });
  }

  public cancelInvitation(gameId: string, tokenId: string) {
    return Api.delete(`games/${gameId}/invitations/${tokenId}`);
  }

  // First unread content navigation
  public getFirstUnreadPost(gameId: string) {
    return Api.get<Envelope<FirstUnreadPostResult>>(`games/${gameId}/posts/first-unread`);
  }

  public getFirstUnreadComment(gameId: string) {
    return Api.get<Envelope<FirstUnreadCommentResult>>(`games/${gameId}/comments/first-unread`);
  }

  // Mark as read
  public markRoomAsRead(roomId: string) {
    return Api.delete(`rooms/${roomId}/posts/unread`);
  }

  public markCommentsAsRead(gameId: string) {
    return Api.delete(`games/${gameId}/comments/unread`);
  }
})();
