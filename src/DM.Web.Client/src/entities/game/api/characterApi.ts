import type { Envelope, ListEnvelope } from "@/shared/api/models/common";
import type {
  Character,
  CharacterInput,
  CharacterStatusTransition,
} from "../model/characterTypes";
import { Api } from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api/audience";
import type { RequestOptions } from "@/shared/api/interceptor";

const characterApi = {
  getCharacters(gameId: string) {
    return Api.get<ListEnvelope<Character>>(`games/${gameId}/characters`);
  },

  /** For reading: BBCode values arrive as rendered HTML in valueBbText. */
  getCharacter(characterId: string, options?: RequestOptions) {
    return Api.get<Envelope<Character>>(
      `characters/${characterId}`,
      undefined,
      undefined,
      options,
    );
  },

  createCharacter(
    id: string,
    character: CharacterInput,
    options?: RequestOptions,
  ) {
    return Api.post<Envelope<Character>>(
      `games/${id}/characters`,
      character,
      options,
    );
  },

  updateCharacter(
    characterId: string,
    character: CharacterInput,
    options?: RequestOptions,
  ) {
    return Api.patch<Envelope<Character>>(
      `characters/${characterId}`,
      character,
      options,
    );
  },

  /** For the editor: BBCode values arrive as raw source in valueBbText. */
  getCharacterForEdit(characterId: string) {
    return Api.get<Envelope<Character>>(
      `characters/${characterId}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  },

  // A transition, not a target status: Retired is reached three ways, each another person's right.
  changeCharacterStatus(
    characterId: string,
    transition: CharacterStatusTransition,
    options?: RequestOptions,
  ) {
    return Api.post<Envelope<Character>>(
      `characters/${characterId}/status`,
      { transition },
      options,
    );
  },

  deleteCharacter(characterId: string, options?: RequestOptions) {
    return Api.delete(`characters/${characterId}`, undefined, options);
  },
};

export default characterApi;
