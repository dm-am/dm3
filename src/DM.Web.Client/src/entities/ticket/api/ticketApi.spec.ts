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
  Api: {
    get: vi.fn().mockResolvedValue({ data: null, error: null }),
    post: vi.fn().mockResolvedValue({ data: null, error: null }),
  },
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

/**
 * A queue of moderation is asked for by its status and its reason, and by
 * nothing else: nobody takes a ticket in hand any more (owner, 12.10, line 51),
 * and the wire has no flag of who took one.
 */
describe("ticketApi.getTickets", () => {
  beforeEach(() => vi.mocked(Api.get).mockClear());

  it("asks for a queue by status and reason alone", async () => {
    await ticketApi.getTickets({ status: "WaitingForUser", subtype: "Bug" });

    const [path, query] = vi.mocked(Api.get).mock.calls[0];
    expect(path).toBe("moderation/tickets");
    expect(query).toEqual({ status: "WaitingForUser", subtype: "Bug" });
    expect(Object.keys(query ?? {})).toEqual(["status", "subtype"]);
  });

  it("offers no way to take a ticket in hand", () => {
    expect(
      Object.getOwnPropertyNames(Object.getPrototypeOf(ticketApi)),
    ).not.toContain("assignTicketToMe");
  });
});

/**
 * An answer of a ticket's conversation goes where its writer reads the
 * ticket: a member and a moderator to the ticket by its number, the guest to
 * the ticket of his token, the token in its header (TokenGatedActionsShould:
 * a URL is written verbatim into the proxy access log). Who wrote it is the
 * session's or the token's to say. The body is the text, and for moderation
 * the status the answer moves the ticket to: closing is an answer too, sent
 * the same way, and there is no closing of its own.
 */
describe("ticketApi answers", () => {
  beforeEach(() => vi.mocked(Api.post).mockClear());

  it("writes a member's answer to the ticket by its number, the text alone", async () => {
    await ticketApi.createComment("t-1", { text: "Вот ссылка" });

    expect(vi.mocked(Api.post)).toHaveBeenCalledWith(
      "moderation/tickets/t-1/comments",
      { text: "Вот ссылка" },
    );
  });

  it("writes moderation's answer with the status it moves the ticket to", async () => {
    await ticketApi.createComment("t-1", { text: "", status: "Closed" });

    expect(vi.mocked(Api.post)).toHaveBeenCalledWith(
      "moderation/tickets/t-1/comments",
      { text: "", status: "Closed" },
    );
  });

  it("offers no closing apart from an answer", () => {
    expect(
      Object.getOwnPropertyNames(Object.getPrototypeOf(ticketApi)),
    ).not.toContain("resolveTicket");
  });

  it("writes the guest's comment to the ticket of his token, the token in its header", async () => {
    const token = "5eedc0de5eedc0de5eedc0de5eedc0de";

    await ticketApi.createTrackedComment(token, "Почта та же");

    const [path, body, options] = vi.mocked(Api.post).mock.calls[0];
    expect(path).toBe("tickets/track/comments");
    expect(path).not.toContain(token);
    expect(body).toEqual({ text: "Почта та же" });
    expect(options).toEqual({ headers: { "X-Dm-Ticket-Token": token } });
  });
});
