// The green "new" of a blog: opened within seven days and not closed.
import { describe, it, expect } from "vitest";
import { useBlogDisplay } from "./useBlogDisplay";
import { BlogStatus } from "./types";

const DAY_MS = 24 * 60 * 60 * 1000;
const openedAgo = (ms: number) => new Date(Date.now() - ms).toISOString();

describe("useBlogDisplay.isNew", () => {
  it("marks a blog opened a minute ago while it is active", () => {
    const { isNew } = useBlogDisplay();
    expect(
      isNew({ activatedUtc: openedAgo(60_000), status: BlogStatus.Active }),
    ).toBe(true);
  });

  it("never marks a closed blog, however fresh its opening", () => {
    const { isNew } = useBlogDisplay();
    expect(
      isNew({ activatedUtc: openedAgo(60_000), status: BlogStatus.Closed }),
    ).toBe(false);
  });

  it("reads a subscription row by its date alone", () => {
    const { isNew } = useBlogDisplay();
    expect(isNew({ activatedUtc: openedAgo(60_000) })).toBe(true);
    expect(isNew({ activatedUtc: openedAgo(8 * DAY_MS) })).toBe(false);
    expect(isNew({})).toBe(false);
  });
});
