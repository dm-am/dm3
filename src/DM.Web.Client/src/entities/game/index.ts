// Game entity - public API
// Pure types (interfaces, type aliases)
export type {
  Game,
  GameRef,
  GameId,
  GameUser,
  GamePrivacySettings,
  GameRecruitment,
  GamesQuery,
  Tag,
  Invitation,
  InvitationType,
  AttributeSchema,
  AttributeSpecification,
  AttributeValueSpecification,
  Character,
  CharacterId,
  CharacterAttribute,
  CharacterAttributeId,
  CharacterPrivacySettings,
  Room,
  RoomId,
  RoomClaim,
  RoomSettings,
  PendingPost,
  Post,
  PostId,
  PostBbText,
  DiceRoll,
  PostReview,
  FirstUnreadPostResult,
  FirstUnreadCommentResult,
} from "./model/types";

// Enums (exported as values, can also be used as types)
export {
  GameStatus,
  GameRole,
  ClosedReason,
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
export { usePulseStore, getWeekStartUtc } from "./model/pulseStore";
export type { PulseSearchParams } from "./model/pulseStore";

// Composables
export { useGameDisplay } from "./model/useGameDisplay";

// API
export { gameApi } from "./api";
export { default as gameApiDefault } from "./api";

// UI Components
export { GameStatusBadge, UnreadCounters, PostRating, PostReviewItem } from "./ui";
