/**
 * @vitest-environment jsdom
 */

/**
 * "Мои обращения", as the member reads them.
 *
 * The owner turned the page down whole (05.10): a table with a grey header,
 * four columns breaking every cell into three lines, the answer under a
 * triangle and two dropdowns over a list of two rows. What stands there now is
 * the member's tickets in the block every screen draws a ticket with, newest
 * first, each reason leading to the ticket's own page; no filters. The caption
 * of each says when it was filed, how many answers its conversation holds and
 * whose turn it is, in grey words, with a red "*" where the turn is the
 * reader's: not who wrote it, for every ticket here is the reader's, and
 * nothing under the block, for moderation answers in the conversation of the
 * ticket's page. Checked here are the four states the list can be in
 * (waiting, failed, empty, filled), the star, and its pages, which the server
 * takes.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
} from "@vue/test-utils";
import { createPinia } from "pinia";
import { ticketApi, type Ticket } from "@/entities/ticket";
import { TicketThread } from "@/widgets/ticket-thread";
import MyTicketsPage from "./MyTicketsPage.vue";

enableAutoUnmount(afterEach);

const { route } = vi.hoisted(() => ({
  route: { query: {} as Record<string, string>, params: {} },
}));
vi.mock("vue-router", async () => {
  const { reactive: wrap } = await import("vue");
  const live = wrap(route);
  return {
    useRoute: () => live,
    useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  };
});

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
  descriptionHtml: "Текст",
  comment: "",
  commentsCount: 0,
  ...over,
});

const PagingStub = {
  name: "Paging",
  props: ["paging", "to", "useQuery"],
  template: "<nav class='paging-stub' />",
};

const render = () =>
  mount(MyTicketsPage, {
    global: {
      plugins: [createPinia()],
      stubs: {
        RouterLink: RouterLinkStub,
        PageTitle: true,
        Paging: PagingStub,
      },
    },
  });

const answer = (resources: Ticket[], paging?: object) =>
  ({ data: { resources, paging }, error: null }) as never;

describe("Мои обращения", () => {
  beforeEach(() => {
    route.query = {};
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("waits on three blocks of a ticket", async () => {
    vi.spyOn(ticketApi, "getMyTickets").mockReturnValue(
      new Promise(() => {}) as never,
    );
    const wrapper = render();
    await flushPromises();

    expect(wrapper.findAll(".ticket-skeleton")).toHaveLength(3);
  });

  it("says it failed and tries again on the word", async () => {
    const get = vi
      .spyOn(ticketApi, "getMyTickets")
      .mockResolvedValueOnce({ data: null, error: { status: 500 } } as never)
      .mockResolvedValueOnce(answer([]));
    const wrapper = render();
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
    const wrapper = render();
    await flushPromises();

    expect(wrapper.text()).toContain("Обращений пока нет");
    expect(wrapper.findAllComponents(TicketThread)).toHaveLength(0);
  });

  it("draws the tickets newest first, each leading to its page", async () => {
    vi.spyOn(ticketApi, "getMyTickets").mockResolvedValue(
      answer([
        ticket("old", "2026-09-01T10:00:00Z"),
        ticket("new", "2026-10-01T10:00:00Z"),
      ]),
    );
    const wrapper = render();
    await flushPromises();

    const blocks = wrapper.findAllComponents(TicketThread);
    expect(blocks.map((block) => block.props("to"))).toEqual([
      { name: "my-ticket", params: { ticketId: "new" } },
      { name: "my-ticket", params: { ticketId: "old" } },
    ]);
    // The author is the reader: nobody needs to be told who wrote these.
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
    const wrapper = render();
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

  // The reader is the author of every ticket here: the ones that wait for the
  // user wait for him, the ones that wait for moderation do not.
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
    const wrapper = render();
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

  it("offers no filters over the list", async () => {
    vi.spyOn(ticketApi, "getMyTickets").mockResolvedValue(
      answer([ticket("a", "2026-10-01T10:00:00Z")]),
    );
    const wrapper = render();
    await flushPromises();

    expect(wrapper.find("select, .select, [role='combobox']").exists()).toBe(
      false,
    );
  });

  it("asks the server for the page in the address and pages the list", async () => {
    route.query = { number: "2" };
    const get = vi
      .spyOn(ticketApi, "getMyTickets")
      .mockResolvedValue(
        answer([ticket("a", "2026-10-01T10:00:00Z")], { current: 2, pages: 3 }),
      );
    const wrapper = render();
    await flushPromises();

    expect(get).toHaveBeenCalledWith(expect.objectContaining({ number: 2 }));
    const pager = wrapper.getComponent(PagingStub);
    expect(pager.props("paging")).toEqual({ current: 2, pages: 3 });
    expect(pager.props("to")).toEqual({ name: "my-tickets" });
  });
});
