import type { Rating, UserPicture, UserRef } from "@/shared/api/models/common";
import type { Served } from "@/shared/api/models";

export interface ActiveCharacterInfo {
  name: string;
  ownerUsername: string;
}

/** Retired is refined by isDead, isPlayerLeft and isPlayerExiled. */
export type ApiCharacterStatus =
  "UnderReview" | "Declined" | "Active" | "Retired";

export type CharacterStatusTransition =
  "Accept" | "Decline" | "Kill" | "Exile" | "Leave" | "Resurrect" | "Readmit";

/** The character of the player the playerUsername filter names. */
export interface PlayerCharacterInfo {
  name: string;
  status: ApiCharacterStatus;
  isDead: boolean;
  isPlayerLeft: boolean;
  isPlayerExiled: boolean;
}

export type CharacterPrivacySettings = {
  isNpc: boolean;
  editByMaster: boolean;
  editPostByMaster: boolean;
};

type CharacterAttributeId = Served<string>;
export type CharacterAttribute = {
  id: Served<CharacterAttributeId>;
  title: Served<string>;
  /** Null for a BBCode specification, whose value is valueBbText. */
  value: string | null;
  /** Rendered HTML, bound with ContentText; as typed for the author_edit audience. */
  valueBbText?: Served<string>;
  modifier: Served<string>;
  inconsistent: Served<string>;
};

/** The server derives the title, the type and the modifier from the specification. */
interface CharacterAttributeInput {
  /** The specification's id. */
  id: string;
  value: string;
}

export interface CharacterInput {
  name: string;
  privacy: CharacterPrivacySettings;
  attributes: CharacterAttributeInput[];
}

type CharacterId = Served<string>;
export type Character = {
  id: Served<CharacterId>;
  /** Absent for the master's characters. */
  author?: Served<UserRef>;
  status: ApiCharacterStatus;
  isDead: Served<boolean>;
  isPlayerLeft: Served<boolean>;
  isPlayerExiled: Served<boolean>;
  name: string;
  /** Null addresses without an uploaded avatar. */
  picture: Served<UserPicture>;
  isNpc: Served<boolean>;
  /** Read one character at a time; the roster leaves it out. */
  privacy?: CharacterPrivacySettings;
  /** Read one character at a time; the roster leaves it out. */
  attributes?: Served<CharacterAttribute[]>;
  totalPostsCount: Served<number>;
  lastPostUtc?: Served<string | null>;
  /** Null when the schema has no descriptor or the character no value for it. */
  descriptor?: Served<string | null>;
  /** Null without an author or when the author hides the rating. */
  authorRating?: Served<Rating | null>;
};
