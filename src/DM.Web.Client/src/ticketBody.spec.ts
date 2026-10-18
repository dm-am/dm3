/**
 * @vitest-environment jsdom
 */

/**
 * Every screen that reads a ticket back shows its body and its conversation as
 * the server rendered them, not as they were typed.
 *
 * The support and complaint forms write the body in the markup editor, and
 * the server hands it out twice: `description` as written and
 * `descriptionHtml` rendered with the tag set of profile texts. The screens
 * used to print `description`, which was right while the form was a plain
 * text field; once it became the markup editor, a word made bold in the visual
 * mode reached the moderator, the author and the guest as "[b]word[/b]". The
 * answer of moderation went the same way. It is a comment of the conversation
 * now (owner, 12.10, line 52), written in the form of a topic's discussion,
 * and the server hands a comment out rendered only.
 *
 * All of them draw a ticket with one block (widgets/ticket-thread), and the
 * block is checked through the pages that read a ticket: the queue of
 * moderation and a moderator's page of one ticket, "Мои обращения" and the
 * page of one of them, and the guest's page. The lists hold no conversation,
 * only its count; the pages of one ticket hold it whole.
 *
 * Nobody takes a ticket in hand any more (owner, 12.10, line 51: "Любой
 * старше разбирать может"), and the last check keeps the taking out of the
 * client: no call, no field, no state and no word of it.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
} from "@vue/test-utils";
import { createPinia } from "pinia";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
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
    resolve: () => ({ href: "/support/track/T-1" }),
  }),
}));

// The page of one ticket asks the role of the reader first; this check is
// about what it draws once it may.
vi.mock("@/pages/moderation/lib/useRoleGate", async () => {
  const { ref } = await import("vue");
  return { useRoleGate: () => ({ hasAccess: ref(true), deniedText: "" }) };
});

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
const TYPED = "Не открывается [b]страница[/b]";
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
  description: TYPED,
  descriptionHtml: RENDERED,
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
  description: TYPED,
  descriptionHtml: RENDERED,
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
    route.params = { token: "T-1" };
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

// ---------------------------------------------------------------------------
// Nobody takes a ticket in hand
// ---------------------------------------------------------------------------

/** This spec sits at the root of the client sources it walks. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/**
 * The sources a reader runs. Specs are left out: they name the taking in
 * order to say it is gone.
 */
const sources = (dir: string, out: string[] = []): string[] => {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) sources(full, out);
    } else if (/\.(ts|vue)$/.test(name) && !/\.spec\.ts$/.test(name)) {
      out.push(full);
    }
  }
  return out;
};

/**
 * What the taking in hand was made of: the two calls of the wire, the field
 * and the filter of a ticket, the state it made, and the four words a reader
 * saw. Whole words, so that Object.assign and the assignment of a value stay
 * out of it.
 */
const TAKING: Record<string, RegExp> = {
  "the call that took a ticket":
    /\bassignTicketToMe\b|tickets\/[^"'`\s]*\/assign\b/,
  "the list of the tickets taken": /tickets\/assigned\b|\bgetMyAssigned/,
  "the filter of the queue": /\bisAssigned\b/,
  "the moderator who took it": /\bassignedModerator\w*/i,
  "the state of a ticket taken": /\bInWork\b|\bTICKET_STATE_/,
  "the words of the taking": /Взять в работу|В работе|Разбирает|Разобрал/,
};

describe("the taking of a ticket in hand", () => {
  // A walk that came back empty would find no taking anywhere.
  it("reads the sources a reader runs, the ticket ones among them", () => {
    const files = sources(CLIENT_SRC).map((file) =>
      relative(CLIENT_SRC, file).split("\\").join("/"),
    );
    expect(files.length).toBeGreaterThan(500);
    expect(files).toEqual(
      expect.arrayContaining([
        "entities/ticket/api/ticketApi.ts",
        "widgets/ticket-thread/TicketConversation.vue",
      ]),
    );
  });

  it("is gone from the client", () => {
    const found: string[] = [];
    for (const file of sources(CLIENT_SRC)) {
      const text = readFileSync(file, "utf8");
      for (const [what, pattern] of Object.entries(TAKING)) {
        if (pattern.test(text)) {
          found.push(
            `${relative(CLIENT_SRC, file).split("\\").join("/")}: ${what}`,
          );
        }
      }
    }
    expect(found).toEqual([]);
  });
});
