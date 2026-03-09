/**
 * Shared ID types for cross-entity usage
 * @module shared/api/models/common/ids
 *
 * These types are extracted to shared to allow notifications
 * and other shared modules to reference entity IDs without
 * creating shared→entities imports.
 *
 * FSD Rule: shared should NOT import from entities.
 */

import type { Id } from "../index";

// Forum entity IDs
export type TopicId = Id<string>;
export type BoardId = Id<string>;

// Game entity IDs
export type GameId = Id<string>;

// Blog entity IDs
export type BlogId = Id<string>;
export type PublicationId = Id<string>;
