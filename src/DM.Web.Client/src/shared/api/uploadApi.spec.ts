/**
 * @vitest-environment node
 */

/**
 * The uploads list pages with skip/take, like every other list.
 *
 * /v1/uploads used to be the one endpoint that paged with number/size, and when
 * it was brought onto the shared vocabulary this caller was left behind. Nothing
 * caught it: the server ignores a parameter it does not know rather than
 * refusing it, `vue-tsc` was happy because the signature declared number/size
 * itself, and the architecture check that reads the retired names reads them off
 * the API assembly, which cannot see a client. So GET /v1/uploads answered 200
 * with skip=0&take=20 forever — page two of "Мои файлы" was page one, and the
 * paging line under it said "1" on every page.
 *
 * Asserted on the wire rather than on the signature: the page number is the
 * caller's own idea and the conversion is the only thing that matters.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("./client", () => ({
  default: { get: vi.fn().mockResolvedValue({ data: null, error: null }) },
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
