/**
 * @vitest-environment jsdom
 */

// The queue: tickets in the block every screen draws one with, the red "*"
// where a ticket waits for the reader. The two filters live in the address
// and are checked through a real router on both sides.
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

  // The server stands the queue in order (status, then newest first) and sends
  // the reasons the reader's role is shown; the queue draws every one as it came.
  it("draws every ticket in the order the server sent it, whatever its reason", async () => {
    const { wrapper } = await render([
      ticket({ id: "asked", status: "WaitingForUser" }),
      ticket({ id: "bug", subtype: "Bug" }),
      ticket({ id: "closed", status: "Closed" }),
      ticket({ id: "open", createdUtc: "2026-09-01T10:00:00Z" }),
    ]);

    expect(
      wrapper
        .findAllComponents(TicketThread)
        .map((block) => (block.props("ticket") as Ticket).id),
    ).toEqual(["asked", "bug", "closed", "open"]);
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

  // A ticket waiting for moderation waits for every moderator but its author;
  // one waiting for the user waits for its author alone.
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

  // The two lists under their captions.
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
    ]);
    expect(optionWords(wrapper, "Статус")).toEqual([
      "Все статусы",
      "Ожидает ответа модерации",
      "Ожидает ответа пользователя",
      "Закрыто",
      "Спам",
    ]);
  });

  // The reasons each role is shown, by the server's rule (TicketVisibility).
  it("offers a senior moderator the complaints about a junior's decision too", async () => {
    signIn("SeniorModerator");
    const { wrapper } = await render([ticket({})]);

    expect(optionWords(wrapper, "Тип обращения")).toEqual([
      "Все типы",
      "Жалоба на пользователя",
      "Жалоба на решение младшего модератора",
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

  // A value the queue does not know is no filter at all.
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
      subtype: "UserComplaint",
      status: "Closed",
    });

    expect(chosen(wrapper, "Тип обращения")).toBe("UserComplaint");
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

  // What the address names last is what stays.
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
