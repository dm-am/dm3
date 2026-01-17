import type { Envelope, ListEnvelope } from "@/api/models/common";
import type {
  Game,
  AttributeSchema,
  Tag,
  Character,
  Room,
} from "@/api/models/gaming";
import type { User } from "@/api/models/community";
import Api from "@/api";

export default new (class {
  public getOwnGames() {
    return Api.get<ListEnvelope<Game>>("games/owned");
  }

  public getModerationGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: "Moderation" });
  }

  public getActiveGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: "Active" });
  }

  public getRequirementGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: "Requirement" });
  }

  public getFinishedGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: "Finished" });
  }

  public async getPopularGames() {
    return Api.get<ListEnvelope<Game>>("games/popular");
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
})();
