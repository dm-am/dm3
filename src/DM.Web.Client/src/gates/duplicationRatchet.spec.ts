/**
 * @vitest-environment node
 */

/**
 * Repeated code may not grow. Each of the six groups (client code, client
 * specs, e2e, the TypeScript and JavaScript of scripts/, server code, server
 * tests) has a ceiling of redundant lines, counted by
 * shared/lib/testing/duplicates.ts the way the clean-up baseline counted them,
 * and a group past its ceiling fails with its largest repeated pieces and their
 * places, so the reader sees what to cut.
 *
 * A copy is what the next reader has to find twice and the next fix has to make
 * twice; the ones that drifted apart are where this codebase's bugs have come
 * from. Below the ceiling passes, so that a wave which removes copies does not
 * have to edit this file in the middle of its work: the ceilings are lowered to
 * the measured numbers at the end of each step, and never raised. Raising one
 * widens what is allowed, which takes the owner's word.
 *
 * Twins that stay by rule are in TWINS, each with its reason: a piece whose
 * places all lie in the files of one entry, two of them at least, is not
 * counted. An entry that no longer excuses any piece fails, so the register
 * shrinks with the code.
 */
import { beforeAll, describe, it, expect } from "vitest";
import { existsSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

import {
  GROUPS,
  measureDuplicates,
  redundantLines,
  type Group,
  type Piece,
} from "@/shared/lib/testing/duplicates";

/** The repository root: this spec sits in src/DM.Web.Client/src/gates. */
const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "../../../..");

const CEILING: Record<Group, number> = {
  client: 1998,
  spec: 2123,
  e2e: 150,
  scripts: 16,
  server: 1459,
  srvtest: 1736,
};

const API_DTO =
  "the API maps domain models to its own DTOs (PATTERNS.md), so the wire shape repeats the read model it is mapped from";
const COMMENT_GLUE =
  "glue over a separate module contract each: merging them would couple the modules the split keeps apart";

/** Files whose repeats stay by rule, joined by " + ", with the reason. */
const TWINS: Record<string, string> = {
  "src/DM.Domain.Community/Features/Statistics/LiveStats.cs + src/DM.Web.API/Features/Community/Statistics/StatisticsDtos.cs":
    API_DTO,
  "src/DM.Domain.Community/Features/Statistics/Leaderboards.cs + src/DM.Web.API/Features/Community/Statistics/StatisticsDtos.cs":
    API_DTO,
  "src/DM.Domain.Core/Subscriptions/Subscription.cs + src/DM.Web.API/Features/Personal/Subscriptions/SubscriptionDtos.cs":
    API_DTO,
  "src/DM.Domain.Game/Features/Games/GamesQuery.cs + src/DM.Web.API/Features/Game/Games/GamesQuery.cs": `${API_DTO}; the API query carries its own wire names (HostUsernames for the domain's OwnerUsernames)`,
  "src/DM.Domain.Community/Features/Polls/PollsQuery.cs + src/DM.Web.API/Features/Community/Polls/PollDtos.cs":
    API_DTO,
  "src/DM.Domain.Forum/Features/Topics/TopicsQuery.cs + src/DM.Web.API/Features/Forum/Topics/TopicsQuery.cs":
    API_DTO,
  "src/DM.Web.API/Features/Blog/Comments/BlogCommentApiService.cs + src/DM.Web.API/Features/Blog/PublicationComments/PublicationCommentApiService.cs + src/DM.Web.API/Features/Forum/Comments/TopicCommentApiService.cs + src/DM.Web.API/Features/Game/Comments/GameCommentApiService.cs":
    COMMENT_GLUE,
  "src/DM.Domain.Blog/Features/Comments/BlogCommentService.cs + src/DM.Domain.Blog/Features/PublicationComments/PublicationCommentService.cs + src/DM.Domain.Forum/Features/Comments/TopicCommentService.cs + src/DM.Domain.Game/Features/Comments/GameCommentService.cs":
    COMMENT_GLUE,
};

const twinFiles = Object.keys(TWINS).map((key) => new Set(key.split(" + ")));

/** The twin entry that excuses a piece, if any; a repeat inside one file is not a twin. */
const excusedBy = (piece: Piece) =>
  twinFiles.findIndex(
    (files) =>
      piece.places.every((place) => files.has(place.path)) &&
      new Set(piece.places.map((place) => place.path)).size > 1,
  );

const HOW_TO_CUT =
  "cut by meaning: one shared part named after what it does, never by size alone";

const shown = (piece: Piece) =>
  `  ${piece.cost} lines: ${piece.places.map((at) => `${at.path}:${at.from}-${at.to}`).join(" | ")}`;

describe("duplication ratchet", () => {
  let measured: Record<Group, Piece[]>;
  beforeAll(() => {
    measured = measureDuplicates(ROOT);
  }, 120_000);

  it("keeps the redundant lines of every group at or under its ceiling", () => {
    const over = GROUPS.flatMap((group) => {
      const counted = measured[group].filter((p) => excusedBy(p) < 0);
      const lines = redundantLines(counted);
      if (lines <= CEILING[group]) return [];
      return [
        `${group}: ${lines} redundant lines, ceiling ${CEILING[group]}: remove the new copy rather than raise the ceiling; ${HOW_TO_CUT}. Largest pieces:`,
        ...counted.slice(0, 5).map(shown),
      ];
    });
    expect(over).toEqual([]);
  });

  it("keeps every twin pair alive and every file of it in the tree", () => {
    const pieces = GROUPS.flatMap((group) => measured[group]);
    const alive = new Set(pieces.map(excusedBy));
    const stale = Object.keys(TWINS).flatMap((key, at) => [
      ...key
        .split(" + ")
        .filter((path) => !existsSync(join(ROOT, path)))
        .map((path) => `${key}: ${path} is gone: drop or fix the entry`),
      ...(alive.has(at)
        ? []
        : [`${key}: no repeat left between these files: drop the entry`]),
    ]);
    expect(stale).toEqual([]);
  });
});
