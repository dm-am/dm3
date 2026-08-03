/**
 * The builder makes one promise: two composers that are not the same composer
 * do not get the same key. The cross product below is that promise written
 * out, and the three records keep it honest — a subject or a purpose added to
 * the unions and not to them does not compile.
 */
import { describe, it, expect } from "vitest";
import {
  composerDraftKey,
  type DraftEntity,
  type DraftPlace,
  type DraftPurpose,
} from "./draftKey";

const EVERY_ENTITY: Record<DraftEntity, true> = {
  topic: true,
  blog: true,
  game: true,
  room: true,
  chat: true,
};

const EVERY_PLACE: Record<DraftPlace, true> = {
  "global-chat": true,
  support: true,
  complaint: true,
};

const EVERY_PURPOSE: Record<DraftPurpose, true> = {
  post: true,
  metagame: true,
  comment: true,
  message: true,
  publication: true,
  ticket: true,
};

const entities = Object.keys(EVERY_ENTITY) as DraftEntity[];
const places = Object.keys(EVERY_PLACE) as DraftPlace[];
const purposes = Object.keys(EVERY_PURPOSE) as DraftPurpose[];

describe("composerDraftKey", () => {
  it("gives no two composers the same key", () => {
    const keys = [
      ...entities.flatMap((subject) =>
        purposes.flatMap((purpose) =>
          ["7f1a", "b204"].map((id) => composerDraftKey(subject, purpose, id)),
        ),
      ),
      ...places.flatMap((subject) =>
        purposes.map((purpose) => composerDraftKey(subject, purpose)),
      ),
    ];
    expect(new Set(keys).size).toBe(keys.length);
  });

  it("separates two entities of one kind", () => {
    expect(composerDraftKey("game", "comment", "7f1a")).not.toBe(
      composerDraftKey("game", "comment", "b204"),
    );
  });

  it("separates two kinds that share an identifier", () => {
    expect(composerDraftKey("game", "comment", "7f1a")).not.toBe(
      composerDraftKey("blog", "comment", "7f1a"),
    );
  });

  it("separates two composers of one entity", () => {
    // A game room writes the post and its metagame half in two boxes.
    expect(composerDraftKey("room", "post", "7f1a")).not.toBe(
      composerDraftKey("room", "metagame", "7f1a"),
    );
  });

  it("names the purpose where there is no entity", () => {
    expect(composerDraftKey("global-chat", "message")).toBe(
      "global-chat:-:message",
    );
    expect(composerDraftKey("support", "ticket")).not.toBe(
      composerDraftKey("complaint", "ticket"),
    );
  });

  it("repeats itself for the same composer", () => {
    expect(composerDraftKey("topic", "comment", "7f1a")).toBe(
      composerDraftKey("topic", "comment", "7f1a"),
    );
  });

  it("gives no key while the entity is unknown", () => {
    // Not a placeholder key: nothing is saved until the composer knows whose
    // text it is holding.
    expect(composerDraftKey("game", "comment", undefined)).toBe("");
    expect(composerDraftKey("game", "comment", null)).toBe("");
    expect(composerDraftKey("game", "comment", "")).toBe("");
  });
});
