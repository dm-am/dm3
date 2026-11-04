import type { Envelope, ListEnvelope, User } from "@/shared/api/models/common";
import type {
  Game,
  GameUser,
  CreateGameInput,
  UpdateGameInput,
  GameAttributeSchema,
  ReplaceGameAttributeSchemaRequest,
  Tag,
  Invitation,
  GameStatusTransition,
  GamePremoderationTransition,
} from "../model/gameTypes";
import { Api, RENDER_AUDIENCE } from "@/shared/api";
import { FRESH, type RequestOptions } from "@/shared/api/interceptor";

const gameApi = {
  getGame(id: string) {
    return Api.get<Envelope<Game>>(`games/${id}/details`);
  },

  /** The description as the master typed it; anybody else is given it rendered. */
  getGameForEdit(id: string) {
    return Api.get<Envelope<Game>>(
      `games/${id}/details`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  },

  getUsers(gameId: string) {
    return Api.get<ListEnvelope<GameUser>>(`games/${gameId}/users`);
  },

  subscribe(id: string) {
    return Api.post<User>(`games/${id}/readers`);
  },

  unsubscribe(id: string) {
    return Api.delete(`games/${id}/readers`);
  },

  // Cached for five minutes and written under moderation/tags, so a reload after an edit asks fresh.
  getTags(fresh = false) {
    return Api.get<ListEnvelope<Tag>>(
      "games/tags",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
  },

  // The whole set with the version it was read at: a sheet changed meanwhile is refused.
  replaceAttributeSchema(
    gameId: string,
    schema: ReplaceGameAttributeSchemaRequest,
    options?: RequestOptions,
  ) {
    return Api.put<Envelope<GameAttributeSchema>>(
      `games/${gameId}/attribute-schema`,
      schema,
      options,
    );
  },

  createGame(game: CreateGameInput) {
    return Api.post<Envelope<Game>>("games", game);
  },

  deleteGame(id: string, options?: RequestOptions) {
    return Api.delete(`games/${id}`, undefined, options);
  },

  getGameInvitations(gameId: string) {
    return Api.get<ListEnvelope<Invitation>>(`games/${gameId}/invitations`);
  },

  invitePlayer(gameId: string, username: string) {
    return Api.post<Invitation>(`games/${gameId}/invitations/players`, {
      username,
    });
  },

  inviteReader(gameId: string, username: string) {
    return Api.post<Invitation>(`games/${gameId}/invitations/readers`, {
      username,
    });
  },

  inviteAssistant(gameId: string, username: string) {
    return Api.post<Invitation>(`games/${gameId}/invitations/assistants`, {
      username,
    });
  },

  cancelInvitation(gameId: string, tokenId: string) {
    return Api.delete(`games/${gameId}/invitations/${tokenId}`);
  },

  getBlacklist(gameId: string) {
    return Api.get<ListEnvelope<User>>(`games/${gameId}/blacklist`);
  },

  addToBlacklist(gameId: string, username: string) {
    return Api.post<Envelope<User>>(`games/${gameId}/blacklist`, { username });
  },

  removeFromBlacklist(gameId: string, login: string) {
    return Api.delete(`games/${gameId}/blacklist/${login}`);
  },

  removeAssistant(gameId: string, username: string, options?: RequestOptions) {
    return Api.delete(
      `games/${gameId}/users/assistants/${username}`,
      undefined,
      options,
    );
  },

  updateGame(id: string, patch: UpdateGameInput) {
    return Api.patch<Envelope<Game>>(`games/${id}/details`, patch);
  },

  transitionStatus(
    id: string,
    transition: GameStatusTransition,
    options?: RequestOptions,
  ) {
    return Api.post<Envelope<Game>>(
      `games/${id}/status`,
      { transition },
      options,
    );
  },

  changePremoderation(
    id: string,
    transition: GamePremoderationTransition,
    options?: RequestOptions,
  ) {
    return Api.post<Envelope<Game>>(
      `games/${id}/premoderation`,
      { transition },
      options,
    );
  },

  resetRecruitment(id: string, options?: RequestOptions) {
    return Api.post<Envelope<Game>>(
      `games/${id}/reset-recruitment-date`,
      undefined,
      options,
    );
  },
};

export default gameApi;
