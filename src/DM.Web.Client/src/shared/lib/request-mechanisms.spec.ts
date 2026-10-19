/**
 * @vitest-environment node
 */

// The race guard of requests is declared once; screens reach it through
// useGuardedRequest.
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

const THE_GUARD = "shared/lib/utils/requestGuard.ts";

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) &&
  !name.endsWith(".spec.ts") &&
  !name.endsWith(".d.ts");

describe("the race guard", () => {
  it("is declared in exactly one place", () => {
    const declarations = collect(CLIENT_SRC, isSource)
      .filter((file) =>
        /export function createRequestGuard/.test(readFileSync(file, "utf8")),
      )
      .map(asPath);

    expect(declarations).toEqual([THE_GUARD]);
  });
});
