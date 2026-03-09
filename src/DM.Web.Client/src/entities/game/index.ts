// Game entity - public API
// Types
export type {
  Game,
  GameId,
  GameStatus,
  GameRole,
  GameUser,
  GamePrivacySettings,
  GameRecruitment,
  GamesQuery,
  Tag,
  TagId,
  CommentariesAccessMode,
  Invitation,
  InvitationType,
  AttributeSchema,
  AttributeSchemaType,
  AttributeSpecification,
  AttributeSpecificationType,
  AttributeValueSpecification,
  Character,
  CharacterId,
  CharacterStatus,
  CharacterAttribute,
  CharacterAttributeId,
  CharacterPrivacySettings,
  Alignment,
  Room,
  RoomId,
  RoomType,
  RoomAccessType,
  RoomClaim,
  RoomSettings,
  PendingPost,
  Post,
  PostId,
  PostBbText,
  DiceRoll,
  FeaturedPost,
  FeaturedPostId,
  FeaturedPostsEnvelope,
  PostReview,
  ReviewSign,
  FirstUnreadPostResult,
  FirstUnreadCommentResult,
} from "./model/types";

// Re-export enums as values (not just types)
export {
  GameStatus,
  GameRole,
  CommentariesAccessMode,
  AttributeSchemaType,
  AttributeSpecificationType,
  CharacterStatus,
  Alignment,
  RoomType,
  RoomAccessType,
  ReviewSign,
} from "./model/types";

// Store
export { useGamesStore, useGameDetailsStore } from "./model/store";
export { useFeaturedPostsStore } from "./model/featuredPostsStore";

// API
export { gameApi } from "./api";
export { default as gameApiDefault } from "./api";
