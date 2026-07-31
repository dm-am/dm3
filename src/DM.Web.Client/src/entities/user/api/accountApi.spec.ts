/**
 * @vitest-environment jsdom
 */

/**
 * A 403 from the sign-in endpoint is the answer to the sign-in form, not a
 * site-wide refusal: the server names the state of the account and the form
 * shows that sentence under the password field. The response interceptor's
 * generic "Недостаточно прав для этого действия" would put a sentence that
 * names nothing in its place, so this one request takes the refusal over.
 */
import { describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", () => ({
  Api: { post: vi.fn().mockResolvedValue({ data: null, error: null }) },
}));

import { Api } from "@/shared/api";
import accountApi from "./accountApi";

describe("accountApi.signIn", () => {
  it("takes the refusal over from the response interceptor", async () => {
    await accountApi.signIn({
      email: "reader@dm.am",
      password: "correct horse",
    });

    expect(vi.mocked(Api.post)).toHaveBeenCalledWith(
      "account/login",
      { email: "reader@dm.am", password: "correct horse" },
      { ownsRefusal: true },
    );
  });
});
