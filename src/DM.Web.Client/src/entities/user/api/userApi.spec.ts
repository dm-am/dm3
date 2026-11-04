/**
 * @vitest-environment node
 */

// An unbound query parameter is ignored, so the page has to go out as skip.
import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("@/shared/api", () => ({
  Api: { get: vi.fn().mockResolvedValue({ data: null, error: null }) },
}));

import { Api } from "@/shared/api";
import type { Username } from "@/shared/api/models/community";
import userApi from "./userApi";

const reader = "Reader" as Username;

describe("the endorsement lists", () => {
  beforeEach(() => vi.mocked(Api.get).mockClear());

  it("ask for the page the reader is on, in the paging the API binds", async () => {
    await userApi.getUserEndorsements(reader, { number: 3, take: 20 });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith(
      "users/Reader/endorsements",
      { skip: 40, take: 20 },
    );
  });

  it("page the written list the same way", async () => {
    await userApi.getWrittenUserEndorsements(reader, { number: 2, take: 5 });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith(
      "users/Reader/written-endorsements",
      { skip: 5, take: 5 },
    );
  });

  it("ask for no offset on the first page", async () => {
    await userApi.getUserEndorsements(reader, {
      number: 1,
      take: 20,
      search: "спасибо",
    });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith(
      "users/Reader/endorsements",
      { take: 20, search: "спасибо" },
    );
  });
});
