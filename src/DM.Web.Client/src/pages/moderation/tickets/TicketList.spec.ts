/**
 * @vitest-environment jsdom
 */

/**
 * The queue "Обращения", as moderation works it: one queue of every ticket
 * the reader's role is shown, complaints and the support desk alike.
 *
 * A queue draws its tickets in the block every screen draws a ticket with, so
 * the moderator reads a ticket the way its author does, with one line more:
 * who wrote it. The reason of each leads to the ticket's page, and the open
 * tickets stand first, the newest of them on top. A ticket that waits for
 * moderation has the red "*" after its state: it waits for the reader, unless
 * he filed it himself.
 *
 * Its two filters are the two lists over it, as they stood before 5 October
 * 2026: "Тип обращения" and "Статус", a choice in either applied at once. A
 * choice goes into the address, and the address is what the queue is asked
 * by. They are checked through a real router on both sides, because the
 * address is where they live: a filter that reached the request but not the
 * address would hand over a link to the whole queue. The statuses are the four
 * a ticket is stored with and named by on every screen, in the words they had
 * before 5 October: whose turn it is ("Ожидает ответа модерации", "Ожидает
 * ответа пользователя") or how it ended. Nobody takes a ticket in hand any
 * more (owner, 12.10, line 51), so no status is told apart by who took it.
 */
import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
  type VueWrapper,
} from "@vue/test-utils";
import { createMemoryHistory, createRouter, type Router } from "vue-router";
import { createPinia } from "pinia";
import { ticketApi, type Ticket } from "@/entities/ticket";
import FormField from "@/shared/ui/Form/FormField.vue";
import { TicketThread } from "@/widgets/ticket-thread";
import TicketList from "./TicketList.vue";

enableAutoUnmount(afterEach);

const ticket = (over: Partial<Ticket>): Ticket => ({
  id: "t",
  reporter: {
    id: "u-1",
    username: "Experienced",
    role: "RegularUser",
    lastActivityUtc: null,
  } as never,
  isGuest: false,
  status: "WaitingForModeration",
  subtype: "UserComplaint",
  createdUtc: "2026-10-01T10:00:00Z",
  description: "Текст",
  descriptionHtml: "Текст",
  comment: "",
  commentsCount: 0,
  ...over,
});

/** The moderator reading the queue, as the auth store reads him back. */
const signIn = (role = "Moderator", id = "u-reader") =>
  localStorage.setItem(
    "user",
    JSON.stringify({ id, username: "Moderator_Me", role, settings: {} }),
  );

let router: Router;

const answer = (tickets: Ticket[]) => ({
  data: { resources: tickets },
  error: null,
});

const render = async (
  tickets: Ticket[],
  query: Record<string, string> = {},
) => {
  const getTickets = vi
    .spyOn(ticketApi, "getTickets")
    .mockResolvedValue(answer(tickets) as never);
  router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: "/moderation/tickets",
        name: "moderation-tickets",
        component: { render: () => null },
      },
    ],
  });
  await router.push({ path: "/moderation/tickets", query });
  await router.isReady();
  const wrapper = mount(TicketList, {
    attachTo: document.body,
    global: {
      plugins: [router, createPinia()],
      // Registered by the app layer, which no test mounts.
      components: { FormField },
      stubs: {
        RouterLink: RouterLinkStub,
        SvgIcon: { template: '<span class="svg-icon-stub" />' },
      },
    },
  });
  await flushPromises();
  return { wrapper, getTickets };
};

const query = () => router.currentRoute.value.query;

/** The list under the caption. */
function select(wrapper: VueWrapper, caption: string) {
  const found = wrapper
    .findAll(".form-field")
    .find((f) => f.find(".form-field-label").text() === caption);
  if (!found) throw new Error(`no field ${caption}`);
  return found.get("select");
}

const optionWords = (wrapper: VueWrapper, caption: string) =>
  select(wrapper, caption)
    .findAll("option")
    .map((o) => o.text());

const chosen = (wrapper: VueWrapper, caption: string) =>
  (select(wrapper, caption).element as HTMLSelectElement).value;

async function choose(wrapper: VueWrapper, caption: string, value: string) {
  await select(wrapper, caption).setValue(value);
  await flushPromises();
}

beforeEach(() => {
  localStorage.clear();
  signIn();
});

describe("a queue of tickets", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    document.body.innerHTML = "";
  });

  it("names the author of each and leads to its page", async () => {
    const { wrapper } = await render([ticket({ id: "a" })]);

    const block = wrapper.getComponent(TicketThread);
    expect(block.props("withAuthor")).toBe(true);
    expect(block.props("to")).toEqual({
      name: "moderation-ticket",
      params: { ticketId: "a" },
    });
  });

  // The server sends the tickets the reader's role is shown, and the queue
  // draws every one of them: the support desk's stand among the complaints.
  it("puts the open tickets first, the newest on top, whatever their reason", async () => {
    const { wrapper } = await render([
      ticket({
        id: "closed",
        status: "Closed",
        createdUtc: "2026-10-05T10:00:00Z",
      }),
      ticket({
        id: "asked",
        status: "WaitingForUser",
        createdUtc: "2026-10-04T10:00:00Z",
      }),
      ticket({ id: "older", createdUtc: "2026-09-01T10:00:00Z" }),
      ticket({ id: "newer", createdUtc: "2026-10-02T10:00:00Z" }),
      ticket({
        id: "bug",
        subtype: "Bug",
        createdUtc: "2026-09-15T10:00:00Z",
      }),
    ]);

    // Waiting for moderation first, then waiting for the author, then ended.
    expect(
      wrapper
        .findAllComponents(TicketThread)
        .map((block) => (block.props("ticket") as Ticket).id),
    ).toEqual(["newer", "bug", "older", "asked", "closed"]);
  });

  it("names in each caption how many answers it holds and whose turn it is", async () => {
    const { wrapper } = await render([
      ticket({ id: "a", status: "WaitingForUser", commentsCount: 2 }),
    ]);

    const caption = wrapper
      .get(".record-block__caption")
      .text()
      .replace(/\s+/g, " ");
    expect(caption).toContain("Автор: Experienced");
    expect(caption).toContain("Ответы (2)");
    expect(caption).toContain("Ожидает ответа пользователя");
  });

  // A ticket waiting for moderation waits for every moderator it is shown to,
  // but not for its author, a moderator himself; one waiting for the user
  // waits for its author alone.
  it("marks with the star the tickets that wait for the reader", async () => {
    const mine = {
      id: "u-reader",
      username: "Moderator_Me",
      role: "Moderator",
    };
    const { wrapper } = await render([
      ticket({ id: "theirs", createdUtc: "2026-10-04T10:00:00Z" }),
      ticket({
        id: "own",
        reporter: mine as never,
        createdUtc: "2026-10-03T10:00:00Z",
      }),
      ticket({ id: "asked", status: "WaitingForUser" }),
      ticket({
        id: "own-asked",
        status: "WaitingForUser",
        reporter: mine as never,
      }),
      ticket({ id: "closed", status: "Closed" }),
    ]);

    expect(
      Object.fromEntries(
        wrapper
          .findAllComponents(TicketThread)
          .map((block) => [
            (block.props("ticket") as Ticket).id,
            block.props("awaited"),
          ]),
      ),
    ).toEqual({
      theirs: true,
      own: false,
      asked: false,
      "own-asked": true,
      closed: false,
    });
  });

  it("says so when the queue is empty", async () => {
    const { wrapper } = await render([]);

    expect(wrapper.text()).toContain("Обращений пока нет");
  });
});

describe("the filters of a queue", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    document.body.innerHTML = "";
  });

  /**
   * The two lists under their captions, as before 5 October 2026, and not
   * the filter line with its panel and bubbles.
   */
  it("stands as two lists under their captions", async () => {
    const { wrapper } = await render([ticket({})]);

    expect(
      wrapper.findAll(".filters .form-field-label").map((l) => l.text()),
    ).toEqual(["Тип обращения", "Статус"]);
    expect(wrapper.find(".filter-btn").exists()).toBe(false);
    expect(wrapper.find(".bubbles-row").exists()).toBe(false);
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false);
  });

  it("offers the types a moderator is shown and the four statuses, each from no filter on", async () => {
    const { wrapper } = await render([ticket({})]);

    expect(optionWords(wrapper, "Тип обращения")).toEqual([
      "Все типы",
      "Жалоба на пользователя",
      "Предложение по улучшению сайта",
    ]);
    // The four a ticket is stored with, in the order the queue stands in.
    expect(optionWords(wrapper, "Статус")).toEqual([
      "Все статусы",
      "Ожидает ответа модерации",
      "Ожидает ответа пользователя",
      "Закрыто",
      "Спам",
    ]);
  });

  // The reasons each role is shown, by the rule of the server
  // (TicketVisibility): a reason the reader is never shown would narrow the
  // queue to nothing.
  it("offers a senior moderator the complaints about a junior's decision too", async () => {
    signIn("SeniorModerator");
    const { wrapper } = await render([ticket({})]);

    expect(optionWords(wrapper, "Тип обращения")).toEqual([
      "Все типы",
      "Жалоба на пользователя",
      "Жалоба на решение младшего модератора",
      "Предложение по улучшению сайта",
    ]);
  });

  it("offers an administrator every reason, the support desk's among them", async () => {
    signIn("Admin");
    const { wrapper } = await render([ticket({})]);

    expect(optionWords(wrapper, "Тип обращения")).toEqual([
      "Все типы",
      "Жалоба на пользователя",
      "Жалоба на решение младшего модератора",
      "Жалоба на решение старшего модератора",
      "Предложение по улучшению сайта",
      "Ошибка",
      "Восстановление доступа",
      "Проблемы с регистрацией",
      "Удаление аккаунта",
      "Восстановление удаленного аккаунта",
      "Персональные данные",
      "Вопрос по соглашению",
    ]);
  });

  it("applies a choice at once, into the address and the request", async () => {
    const { wrapper, getTickets } = await render([ticket({})]);

    await choose(wrapper, "Статус", "WaitingForUser");

    expect(query()).toEqual({ status: "WaitingForUser" });
    expect(getTickets).toHaveBeenLastCalledWith({
      status: "WaitingForUser",
      subtype: undefined,
    });

    await choose(wrapper, "Тип обращения", "UserComplaint");

    expect(query()).toEqual({
      subtype: "UserComplaint",
      status: "WaitingForUser",
    });
    expect(getTickets).toHaveBeenLastCalledWith({
      status: "WaitingForUser",
      subtype: "UserComplaint",
    });
  });

  /** Each status is asked of the server as it is, and nothing beside it. */
  it.each([
    "WaitingForModeration",
    "WaitingForUser",
    "Closed",
    "Spam",
  ] as const)("asks the server for the status %s", async (status) => {
    const { getTickets } = await render([], { status });

    expect(getTickets).toHaveBeenCalledWith({ status, subtype: undefined });
  });

  it("asks for the whole queue with no filter in the address", async () => {
    const { getTickets } = await render([]);

    expect(getTickets).toHaveBeenCalledWith({ subtype: undefined });
  });

  /**
   * A type the reader's role is not shown or a status that is no more ("В
   * работе" of the taking in hand) is no filter at all: a request the server
   * would answer with an empty queue would read as a queue with nothing in it.
   */
  it("takes a value it does not know for no filter", async () => {
    const { wrapper, getTickets } = await render([ticket({})], {
      subtype: "Bug",
      status: "InWork",
    });

    expect(getTickets).toHaveBeenCalledWith({ subtype: undefined });
    expect(chosen(wrapper, "Тип обращения")).toBe("");
    expect(chosen(wrapper, "Статус")).toBe("");
  });

  it("shows what the address holds", async () => {
    const { wrapper } = await render([ticket({})], {
      subtype: "SiteImprovementSuggestion",
      status: "Closed",
    });

    expect(chosen(wrapper, "Тип обращения")).toBe("SiteImprovementSuggestion");
    expect(chosen(wrapper, "Статус")).toBe("Closed");
  });

  it("takes a filter off by the first line of its list", async () => {
    const { wrapper, getTickets } = await render([ticket({})], {
      subtype: "UserComplaint",
      status: "WaitingForModeration",
    });

    await choose(wrapper, "Тип обращения", "");

    expect(query()).toEqual({ status: "WaitingForModeration" });
    expect(getTickets).toHaveBeenLastCalledWith({
      status: "WaitingForModeration",
      subtype: undefined,
    });
  });

  /** A queue narrowed to nothing is not an empty queue. */
  it("says nothing was found when the filters match nothing", async () => {
    const { wrapper } = await render([], { status: "Spam" });

    expect(wrapper.text()).toContain(
      "Обращений по заданным фильтрам не найдено",
    );
    expect(wrapper.text()).not.toContain("Обращений пока нет");
  });

  /**
   * The back button moves the address under the queue while an answer is on
   * its way; what the address names last is what stays.
   */
  it("keeps the queue the address names when an older answer lands late", async () => {
    const answers = new Map<string, (value: unknown) => void>();
    const { wrapper, getTickets } = await render([]);
    getTickets.mockImplementation(
      (params) =>
        new Promise((resolve) => {
          answers.set(String(params?.status ?? "all"), resolve);
        }) as never,
    );

    await router.push({ query: { status: "Closed" } });
    await flushPromises();
    await router.push({ query: { status: "Spam" } });
    await flushPromises();

    answers.get("Spam")?.(answer([ticket({ id: "spam", status: "Spam" })]));
    await flushPromises();
    answers.get("Closed")?.(
      answer([ticket({ id: "closed", status: "Closed" })]),
    );
    await flushPromises();

    expect(
      wrapper
        .findAllComponents(TicketThread)
        .map((block) => (block.props("ticket") as Ticket).id),
    ).toEqual(["spam"]);
  });
});
