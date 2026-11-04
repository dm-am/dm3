/**
 * @vitest-environment jsdom
 */

/**
 * Every screen that reads a ticket back shows its body and its conversation as
 * the server rendered them, markup drawn and not escaped: the server hands the
 * body out as `description` and a comment of the conversation as `text`,
 * both rendered. All of them draw a ticket with one block
 * (widgets/ticket-thread), checked here through the pages that read a ticket:
 * the queue of moderation and a moderator's page of one ticket, "Мои обращения"
 * and the page of one of them, and the guest's page.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
} from "@vue/test-utils";
import { createPinia } from "pinia";
import {
  ticketApi,
  type TicketComment,
  type TicketDetails,
  type TrackedTicket,
} from "@/entities/ticket";
import TicketList from "@/pages/moderation/tickets/TicketList.vue";
import ModerationTicketPage from "@/pages/moderation/ModerationTicketPage.vue";
import MyTicketsPage from "@/pages/personal/MyTicketsPage.vue";
import MyTicketPage from "@/pages/personal/MyTicketPage.vue";
import TicketTrackPage from "@/pages/support/TicketTrackPage.vue";

enableAutoUnmount(afterEach);

const { route } = vi.hoisted(() => ({
  route: {
    query: {} as Record<string, string>,
    params: {} as Record<string, string>,
    hash: "",
  },
}));
vi.mock("vue-router", () => ({
  useRoute: () => route,
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    resolve: () => ({ href: "/support/track#token=T-1" }),
  }),
}));

// A heavy tiptap component; the form under an open conversation holds one.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    name: "BBCodeEditor",
    props: ["modelValue"],
    methods: { clear() {} },
    template: "<textarea class='editor-stub' />",
  },
}));

const RENDERED = "Не открывается <strong>страница</strong>";
const ANSWER_RENDERED = "Исправили <em>загрузку</em>";

const answer: TicketComment = {
  id: "c-1",
  author: {
    id: "u-mod",
    username: "TestModerator",
    role: "Moderator",
    lastActivityUtc: null,
  } as never,
  isGuest: false,
  createdUtc: "2026-09-27T09:00:00Z",
  text: ANSWER_RENDERED,
};

const ticket: TicketDetails = {
  id: "t-1",
  reporter: {
    id: "u-1",
    username: "Автор",
    role: "RegularUser",
    lastActivityUtc: null,
  } as never,
  isGuest: false,
  status: "WaitingForUser",
  subtype: "Bug",
  createdUtc: "2026-09-26T10:00:00Z",
  description: RENDERED,
  comment: "",
  commentsCount: 1,
  comments: [answer],
  canComment: true,
  canResolve: true,
  resolvedUtc: null,
  resolvedByUsername: null,
};

const tracked: TrackedTicket = {
  status: "WaitingForUser",
  subtype: "Bug",
  subject: "",
  description: RENDERED,
  createdUtc: "2026-09-26T10:00:00Z",
  resolvedUtc: null,
  resolvedByUsername: null,
  comments: [answer],
  canComment: true,
};

const global = {
  plugins: [createPinia()],
  stubs: { RouterLink: RouterLinkStub, PageTitle: true },
};

/** The body as a reader sees it: bold where it was made so, no brackets. */
function expectBodyRendered(wrapper: ReturnType<typeof mount>) {
  expect(wrapper.text()).toContain("Не открывается страница");
  expect(wrapper.text()).not.toContain("[b]");
  expect(wrapper.find(".ticket-body strong").text()).toBe("страница");
}

/** The answer of moderation, a comment of the conversation, rendered. */
function expectConversationRendered(wrapper: ReturnType<typeof mount>) {
  expect(wrapper.text()).toContain("Исправили загрузку");
  expect(wrapper.find(".comment-text em").text()).toBe("загрузку");
}

describe("a ticket read back", () => {
  beforeEach(() => {
    route.query = {};
    route.params = {};
    route.hash = "";
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("is rendered in the queue of moderation", async () => {
    vi.spyOn(ticketApi, "getTickets").mockResolvedValue({
      data: { resources: [ticket] },
      error: null,
    } as never);
    const wrapper = mount(TicketList, {
      props: { subtypes: ["Bug"], emptyText: "Обращений пока нет" },
      global,
    });
    await flushPromises();

    expectBodyRendered(wrapper);
  });

  it("is rendered on a moderator's page of the ticket", async () => {
    route.params = { ticketId: "t-1" };
    vi.spyOn(ticketApi, "getTicket").mockResolvedValue({
      data: { resource: ticket },
      error: null,
    } as never);
    const wrapper = mount(ModerationTicketPage, { global });
    await flushPromises();

    expectBodyRendered(wrapper);
    expectConversationRendered(wrapper);
  });

  it("is rendered in Мои обращения", async () => {
    vi.spyOn(ticketApi, "getMyTickets").mockResolvedValue({
      data: { resources: [ticket] },
      error: null,
    } as never);
    const wrapper = mount(MyTicketsPage, { global });
    await flushPromises();

    expectBodyRendered(wrapper);
  });

  it("is rendered on the author's page of the ticket", async () => {
    route.params = { ticketId: "t-1" };
    vi.spyOn(ticketApi, "getTicket").mockResolvedValue({
      data: { resource: ticket },
      error: null,
    } as never);
    const wrapper = mount(MyTicketPage, { global });
    await flushPromises();

    expectBodyRendered(wrapper);
    expectConversationRendered(wrapper);
  });

  it("is rendered on the guest's tracking page", async () => {
    route.hash = "#token=T-1";
    vi.spyOn(ticketApi, "trackTicket").mockResolvedValue({
      data: { resource: tracked },
      error: null,
    } as never);
    const wrapper = mount(TicketTrackPage, { global });
    await flushPromises();

    expectBodyRendered(wrapper);
    expectConversationRendered(wrapper);
  });
});
