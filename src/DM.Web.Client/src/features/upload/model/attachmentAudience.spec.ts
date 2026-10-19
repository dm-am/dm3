/**
 * @vitest-environment node
 */

// Whom a file of a post can be shown to, and how its mark reads back.
import { describe, expect, it } from "vitest";
import type { RoomAccess } from "@/entities/game";
import {
  attachmentCandidates,
  audienceCandidates,
  audienceLabel,
  audienceOf,
  audienceRecipients,
  everyReader,
} from "./attachmentAudience";

const access = (
  id: string,
  name: string,
  over: { owner?: string; isNpc?: boolean } = {},
) =>
  ({
    id: `access-${id}`,
    character: {
      id,
      name,
      isNpc: over.isNpc ?? false,
      author: over.owner ? { username: over.owner } : undefined,
    },
  }) as unknown as RoomAccess;

const reader = {
  id: "access-reader",
  user: { username: "Гость" },
} as RoomAccess;

describe("audienceCandidates", () => {
  // The boxes a hidden block's names would be: the other players' characters.
  it("offers the characters let in, not the author's own nor the NPCs", () => {
    const accesses = [
      access("c-vasya", "Вася", { owner: "vasya" }),
      access("c-mine", "Мой", { owner: "author" }),
      access("c-npc", "Трактирщик", { isNpc: true }),
      reader,
      access("c-petya", "Петя", { owner: "petya" }),
    ];

    expect(audienceCandidates(accesses, "author")).toEqual([
      { id: "c-vasya", name: "Вася" },
      { id: "c-petya", name: "Петя" },
    ]);
  });

  it("offers a character once, however many accesses name it", () => {
    const vasya = access("c-vasya", "Вася", { owner: "vasya" });

    expect(audienceCandidates([vasya, vasya], "author")).toHaveLength(1);
  });

  it("offers nothing without a room", () => {
    expect(audienceCandidates(undefined, "author")).toEqual([]);
  });
});

describe("audienceOf", () => {
  it("reads a file without addressees as every reader's", () => {
    expect(audienceOf({})).toEqual(everyReader());
  });

  // No boxes checked is the author and the leads, as a bare hidden block.
  it("reads chosen characters, none among them, as Selected", () => {
    expect(
      audienceOf({ addressees: [{ id: "c-vasya", name: "Вася" }] }),
    ).toEqual({
      visibleTo: "Selected",
      characterIds: ["c-vasya"],
    });
    expect(audienceOf({ addressees: [] })).toEqual({
      visibleTo: "Selected",
      characterIds: [],
    });
  });
});

describe("attachmentCandidates", () => {
  const room = [
    { id: "c-vasya", name: "Вася" },
    { id: "c-petya", name: "Петя" },
  ];

  // One still in the room keeps his box and his name of today.
  it("adds the characters of the mark gone from the room after the room's", () => {
    expect(
      attachmentCandidates(room, {
        addressees: [
          { id: "c-gone", name: "Ушедший" },
          { id: "c-vasya", name: "Василий" },
        ],
      }),
    ).toEqual([...room, { id: "c-gone", name: "Ушедший" }]);
  });

  it("offers the room alone for a file every reader sees", () => {
    expect(attachmentCandidates(room, {})).toEqual(room);
  });
});

describe("audienceRecipients", () => {
  const candidates = [
    { id: "c-vasya", name: "Вася" },
    { id: "c-petya", name: "Петя" },
  ];

  it("names the leads first, then the checked ones in the order of the boxes", () => {
    expect(audienceRecipients(candidates, [])).toBe("DungeonMaster, Assistant");
    expect(audienceRecipients(candidates, ["c-petya", "c-vasya"])).toBe(
      "DungeonMaster, Assistant, Вася, Петя",
    );
  });
});

describe("audienceLabel", () => {
  const candidates = [
    { id: "c-vasya", name: "Вася" },
    { id: "c-petya", name: "Петя" },
  ];

  it("says every reader, or the checked ones in the order of the boxes", () => {
    expect(audienceLabel(candidates, everyReader())).toBe("видят все");
    expect(
      audienceLabel(candidates, {
        visibleTo: "Selected",
        characterIds: ["c-petya", "c-vasya"],
      }),
    ).toBe("видят: Вася, Петя");
  });

  // A mark naming no character is a bare hidden block's readers.
  it("names the leads for a mark without a character", () => {
    expect(
      audienceLabel(candidates, { visibleTo: "Selected", characterIds: [] }),
    ).toBe("видят: DungeonMaster, Assistant");
  });
});
