/**
 * @vitest-environment node
 */

/**
 * The uploads list pages with skip/take on the wire: the server ignores an
 * unknown parameter, so number/size answered page one forever.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("./client", () => ({
  default: {
    get: vi.fn().mockResolvedValue({ data: null, error: null }),
    postFile: vi.fn().mockResolvedValue({ data: null, error: null }),
    patch: vi.fn().mockResolvedValue({ data: null, error: null }),
  },
}));

import Api from "./client";
import uploadApi from "./uploadApi";

describe("uploadApi.getUploads", () => {
  beforeEach(() => vi.mocked(Api.get).mockClear());

  it("asks for the first page without an offset", async () => {
    await uploadApi.getUploads({ number: 1, size: 20 });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith("uploads", {
      username: undefined,
      skip: undefined,
      take: 20,
    });
  });

  it("converts a page number into the offset the wire takes", async () => {
    await uploadApi.getUploads({ number: 3, size: 25 });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith("uploads", {
      username: undefined,
      skip: 50,
      take: 25,
    });
  });

  it("sends neither of the names the endpoint retired", async () => {
    await uploadApi.getUploads({ number: 2, size: 20, username: "chuck" });

    const [, params] = vi.mocked(Api.get).mock.calls[0];
    expect(Object.keys(params as object)).not.toContain("number");
    expect(Object.keys(params as object)).not.toContain("size");
  });
});

/** Who sees a file of a post: chosen with the file, changed by its own request. */
describe("uploadApi audience", () => {
  beforeEach(() => {
    vi.mocked(Api.postFile).mockClear();
    vi.mocked(Api.patch).mockClear();
  });

  const formOf = () => vi.mocked(Api.postFile).mock.calls[0][1] as FormData;

  it("sends the mark in the form of the upload, a field per character", async () => {
    await uploadApi.directUpload(new File(["x"], "map.png"), "PostAttachment", {
      targetId: "p-1",
      audience: { visibleTo: "Selected", characterIds: ["c-1", "c-2"] },
    });

    expect(formOf().get("visibleTo")).toBe("Selected");
    expect(formOf().getAll("characterIds")).toEqual(["c-1", "c-2"]);
  });

  it("sends no mark for an upload that names none", async () => {
    await uploadApi.directUpload(new File(["x"], "me.png"), "UserAvatar");

    expect(formOf().has("visibleTo")).toBe(false);
    expect(formOf().has("characterIds")).toBe(false);
  });

  it("changes the mark by the file's address, with no characters for every reader", async () => {
    await uploadApi.updateAudience("u-1", {
      visibleTo: "All",
      characterIds: ["c-1"],
    });
    await uploadApi.updateAudience("u-1", {
      visibleTo: "Selected",
      characterIds: ["c-1"],
    });

    expect(vi.mocked(Api.patch).mock.calls).toEqual([
      ["uploads/u-1", { visibleTo: "All" }],
      ["uploads/u-1", { visibleTo: "Selected", characterIds: ["c-1"] }],
    ]);
  });
});
