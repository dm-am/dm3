/**
 * @vitest-environment jsdom
 */

// The member's tickets in the server's order, each leading to its page, the
// caption without the author and with the star where the turn is his; the
// four states of the list and its pages.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { ticketApi, type Ticket } from "@/entities/ticket";
import { TicketThread } from "@/widgets/ticket-thread";
import MyTicketsPage from "./MyTicketsPage.vue";

enableAutoUnmount(afterEach);

const ticket = (
  id: string,
  createdUtc: string,
  over: Partial<Ticket> = {},
): Ticket => ({
  id,
  reporter: {
    id: "u-1",
    username: "Experienced",
    role: "RegularUser",
    lastActivityUtc: null,
  } as never,
  isGuest: false,
  status: "WaitingForModeration",
  subtype: "Bug",
  createdUtc,
  description: "Текст",
  comment: "",
  commentsCount: 0,
  ...over,
});

const PagingStub = {
  name: "Paging",
  props: ["paging", "to", "useQuery"],
  template: "<nav class='paging-stub' />",
};

const render = async (address = "/my-tickets") => {
  const { wrapper } = await mountPage(MyTicketsPage, address, {
    routes: [
      ["my-tickets", "/my-tickets"],
      ["my-ticket", "/my-tickets/:ticketId"],
      ["support", "/support"],
      ["complaint", "/complaint"],
    ],
    stubs: { Paging: PagingStub },
  });
  return wrapper;
};

const answer = (resources: Ticket[], paging?: object) =>
  ({ data: { resources, paging }, error: null }) as never;

describe("the page of my tickets", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("waits on three blocks of a ticket", async () => {
    vi.spyOn(ticketApi, "getMyTickets").mockReturnValue(
      new Promise(() => {}) as never,
    );
    const wrapper = await render();
    await flushPromises();

    expect(wrapper.findAll(".ticket-skeleton")).toHaveLength(3);
  });

  it("says it failed and tries again on the word", async () => {
    const get = vi
      .spyOn(ticketApi, "getMyTickets")
      .mockResolvedValueOnce({ data: null, error: { status: 500 } } as never)
      .mockResolvedValueOnce(answer([]));
    const wrapper = await render();
    await flushPromises();

    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить обращения",
    );
    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();
    expect(get).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".error-state").exists()).toBe(false);
  });

  it("says there are none under the lead", async () => {
    vi.spyOn(ticketApi, "getMyTickets").mockResolvedValue(answer([]));
    const wrapper = await render();
    await flushPromises();

    expect(wrapper.text()).toContain("Обращений пока нет");
    expect(wrapper.findAllComponents(TicketThread)).toHaveLength(0);
  });

  // The server sends them newest first; the page draws them as they came.
  it("draws the tickets in the order the server sent, each leading to its page", async () => {
    vi.spyOn(ticketApi, "getMyTickets").mockResolvedValue(
      answer([
        ticket("old", "2026-09-01T10:00:00Z"),
        ticket("new", "2026-10-01T10:00:00Z"),
      ]),
    );
    const wrapper = await render();
    await flushPromises();

    const blocks = wrapper.findAllComponents(TicketThread);
    expect(blocks.map((block) => block.props("to"))).toEqual([
      { name: "my-ticket", params: { ticketId: "old" } },
      { name: "my-ticket", params: { ticketId: "new" } },
    ]);
    // The author is the reader.
    expect(blocks.every((block) => !block.props("withAuthor"))).toBe(true);
  });

  it("says when each was filed, how many answers it holds and whose turn it is, and not who wrote it", async () => {
    vi.spyOn(ticketApi, "getMyTickets").mockResolvedValue(
      answer([
        ticket("a", "2026-10-01T10:00:00Z", {
          status: "WaitingForUser",
          commentsCount: 3,
        }),
      ]),
    );
    const wrapper = await render();
    await flushPromises();

    const caption = wrapper
      .get(".record-block__caption")
      .text()
      .replace(/\s+/g, " ");
    expect(caption).toContain("Ответы (3)");
    expect(caption).toContain("Ожидает ответа пользователя");
    expect(caption).not.toContain("Автор");
    expect(caption).not.toContain("Experienced");
  });

  // Every ticket is the reader's: those waiting for the user wait for him.
  it("marks with the star the tickets that wait for the reader", async () => {
    localStorage.setItem(
      "user",
      JSON.stringify({
        id: "u-1",
        username: "Experienced",
        role: "RegularUser",
      }),
    );
    vi.spyOn(ticketApi, "getMyTickets").mockResolvedValue(
      answer([
        ticket("his", "2026-10-02T10:00:00Z", { status: "WaitingForUser" }),
        ticket("theirs", "2026-10-01T10:00:00Z"),
      ]),
    );
    const wrapper = await render();
    await flushPromises();

    expect(
      wrapper
        .findAllComponents(TicketThread)
        .map((block) => [
          (block.props("ticket") as Ticket).id,
          block.props("awaited"),
        ]),
    ).toEqual([
      ["his", true],
      ["theirs", false],
    ]);
    expect(wrapper.findAll(".star")).toHaveLength(1);
  });

  it("asks the server for the page in the address and pages the list", async () => {
    const get = vi
      .spyOn(ticketApi, "getMyTickets")
      .mockResolvedValue(
        answer([ticket("a", "2026-10-01T10:00:00Z")], { current: 2, pages: 3 }),
      );
    const wrapper = await render("/my-tickets?number=2");
    await flushPromises();

    expect(get).toHaveBeenCalledWith(expect.objectContaining({ number: 2 }));
    const pager = wrapper.getComponent(PagingStub);
    expect(pager.props("paging")).toEqual({ current: 2, pages: 3 });
    expect(pager.props("to")).toEqual({ name: "my-tickets" });
  });
});
