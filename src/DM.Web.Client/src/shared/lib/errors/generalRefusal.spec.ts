/**
 * @vitest-environment node
 */

// The general refusals: one sentence each, in the words the API titles them with.
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

import {
  LETTER_NOT_SENT,
  REQUEST_FAILED,
  SERVER_ERROR,
} from "./generalRefusal";
import { GENERAL_REASONS } from "./generalPlate";

const HERE = dirname(fileURLToPath(import.meta.url));
// shared/lib/errors -> shared -> src -> DM.Web.Client -> src -> repository root
const CLIENT_SRC = resolve(HERE, "..", "..", "..");
const REPO_ROOT = resolve(CLIENT_SRC, "..", "..", "..");

/** Where the API titles a 500. */
const MIDDLEWARE = join(
  REPO_ROOT,
  "src",
  "DM.Web.API",
  "Middleware",
  "ExceptionProblemDetailsFactoryExtensions.cs",
);

describe("the general refusals", () => {
  it("are one sentence each and carry no period", () => {
    for (const text of [SERVER_ERROR, REQUEST_FAILED, LETTER_NOT_SENT]) {
      expect(text.endsWith("."), `"${text}" заканчивается точкой`).toBe(false);
      expect(
        /[.!?]\s/.test(text),
        `"${text}" это два предложения в одной строке`,
      ).toBe(false);
    }
  });

  it("say a 5xx in the words the API titles one with", () => {
    // A toast over a bodiless 502 still speaks, in the same words.
    const middleware = readFileSync(MIDDLEWARE, "utf8");

    expect(
      middleware.includes(`"${SERVER_ERROR}"`),
      `клиент говорит "${SERVER_ERROR}", а middleware титулует 500 иначе`,
    ).toBe(true);
  });

  it("end a general plate with the same words a 5xx is titled with", () => {
    expect(GENERAL_REASONS.server).toBe(
      SERVER_ERROR[0].toLowerCase() + SERVER_ERROR.slice(1),
    );
  });
});
