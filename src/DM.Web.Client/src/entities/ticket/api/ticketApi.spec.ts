/**
 * @vitest-environment node
 */

/**
 * "Мои обращения" asks for a page, and for one the wire will accept.
 *
 * GET /v1/moderation/tickets/mine used to answer with every ticket its author
 * ever filed; it binds PagingQuery now, so a caller that names no page gets
 * twenty rows and no way to reach the rest. Two things have to hold of the
 * request for the screen to work. The page the reader is on has to become the
 * offset the wire takes — the page number is this application's own vocabulary
 * and the API ignores it rather than refusing it, which reads as "page three is
 * page one". And the page size has to stay inside [Range(1, 100)]: "Сущностей
 * на странице" offers 200, PagingQuery refuses it with 400, and the page shows
 * that refusal as "Не удалось загрузить обращения" where the list should be.
 *
 * Asserted on the wire rather than on the signature: the conversion is the only
 * part of this the caller cannot see for itself.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("@/shared/api", () => ({
  Api: { get: vi.fn().mockResolvedValue({ data: null, error: null }) },
  X_DM_TICKET_TOKEN: "X-Dm-Ticket-Token",
}));

import { Api } from "@/shared/api";
import ticketApi from "./ticketApi";

describe("ticketApi.getMyTickets", () => {
  beforeEach(() => vi.mocked(Api.get).mockClear());

  it("asks for the first page without an offset", async () => {
    await ticketApi.getMyTickets({ number: 1, take: 20 });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith("moderation/tickets/mine", {
      status: undefined,
      subtype: undefined,
      skip: undefined,
      take: 20,
    });
  });

  it("converts a page number into the offset the wire takes", async () => {
    await ticketApi.getMyTickets({ number: 3, take: 20, status: "Closed" });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith("moderation/tickets/mine", {
      status: "Closed",
      subtype: undefined,
      skip: 40,
      take: 20,
    });
  });

  it("clamps a page size the wire would refuse", async () => {
    // 200 is a legal "Сущностей на странице" setting and an illegal take, and
    // the offset is computed from the size actually requested.
    await ticketApi.getMyTickets({ number: 2, take: 200 });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith("moderation/tickets/mine", {
      status: undefined,
      subtype: undefined,
      skip: 100,
      take: 100,
    });
  });

  it("names a page even when the caller names none", async () => {
    await ticketApi.getMyTickets({ subtype: "Bug" });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith("moderation/tickets/mine", {
      status: undefined,
      subtype: "Bug",
      skip: undefined,
      take: 20,
    });
  });
});
