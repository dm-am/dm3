/**
 * @vitest-environment node
 */

// The page becomes the offset the wire takes, and the size stays inside the API's page size.
import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("@/shared/api", () => ({
  Api: {
    get: vi.fn().mockResolvedValue({ data: null, error: null }),
    post: vi.fn().mockResolvedValue({ data: null, error: null }),
  },
  X_DM_TICKET_TOKEN: "X-Dm-Ticket-Token",
}));

import { Api } from "@/shared/api";
import { MAX_API_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
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
    // One above the API's limit is an illegal take, and the offset is computed
    // from the size actually requested.
    await ticketApi.getMyTickets({ number: 2, take: MAX_API_PAGE_SIZE + 1 });

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith("moderation/tickets/mine", {
      status: undefined,
      subtype: undefined,
      skip: MAX_API_PAGE_SIZE,
      take: MAX_API_PAGE_SIZE,
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

describe("ticketApi.getTickets", () => {
  beforeEach(() => vi.mocked(Api.get).mockClear());

  it("asks for a queue by status and reason alone", async () => {
    await ticketApi.getTickets({ status: "WaitingForUser", subtype: "Bug" });

    const [path, query] = vi.mocked(Api.get).mock.calls[0];
    expect(path).toBe("moderation/tickets");
    expect(query).toEqual({ status: "WaitingForUser", subtype: "Bug" });
    expect(Object.keys(query ?? {})).toEqual(["status", "subtype"]);
  });
});

// An answer goes where its writer reads the ticket; a guest's token travels in a header, not the URL.
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
