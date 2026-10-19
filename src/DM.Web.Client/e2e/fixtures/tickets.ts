import type { Browser, Page, Route } from "@playwright/test";
import { settle, windowContext } from "./window";

/**
 * The tickets of the pages of a ticket (tests/personal/tickets.spec.ts,
 * tickets-moderation.spec.ts and tickets-guest.spec.ts), and the readers of
 * what such a page shows.
 *
 * The tickets are answered here in the shape of the wire, so the lines read
 * are known whatever the seed holds, and the writes are answered here too:
 * nothing is written to the stand.
 */

/** The widths the pages of a ticket are read at. */
export const WIDTHS = [1366];

export const OPEN = "6c0a1d3e-0000-4000-8000-0000000000a1";
export const CLOSED = "6c0a1d3e-0000-4000-8000-0000000000a2";
export const TOKEN = "e2e0e2e0e2e0e2e0e2e0e2e0e2e0e2e0";

const NOW = new Date().toISOString();

type Person = {
  id: string;
  username: string;
  role: string;
  lastActivityUtc: string;
  isNewbie: boolean;
};

const person = (
  number: number,
  username: string,
  role: string,
  online = false,
): Person => ({
  id: `6c0a1d3e-0000-4000-8000-${String(number).padStart(12, "0")}`,
  username,
  role,
  lastActivityUtc: online ? NOW : "2026-10-01T10:00:00Z",
  isNewbie: false,
});

const AUTHOR = person(1, "TestUser", "RegularUser", true);
const SENIOR = person(2, "TestSeniorMod", "SeniorModerator");
const MODERATOR = person(3, "TestModerator", "Moderator");
export const ADMIN = person(4, "SolohinLex", "Admin");

type Comment = {
  id: string;
  author: Person | null;
  isGuest: boolean;
  createdUtc: string;
  text: string;
};

/** A ticket of the page, in the shape of the wire (TicketDetails). */
type Ticket = { id: string; comments: Comment[]; [field: string]: unknown };

export const asked: Comment = {
  id: "6c0a1d3e-0000-4000-8000-0000000000c1",
  author: SENIOR,
  isGuest: false,
  createdUtc: "2026-10-11T07:15:00Z",
  text:
    "Пришлите, пожалуйста, ссылку на сообщение, за которое выдано " +
    "предупреждение: в логе за этот день у вас их два.",
};

export const replied: Comment = {
  id: "6c0a1d3e-0000-4000-8000-0000000000c2",
  author: AUTHOR,
  isGuest: false,
  createdUtc: "2026-10-11T09:40:00Z",
  text: "Второе, в обсуждении правил. Я отвечал на вопрос новичка.",
};

const ticketBase = {
  isGuest: false,
  guestEmail: null,
  entityId: null,
  entityType: null,
  comment: "",
};

export const openTicket = (): Ticket => ({
  ...ticketBase,
  id: OPEN,
  reporter: AUTHOR,
  status: "WaitingForModeration",
  subtype: "ModeratorDecisionComplaint",
  createdUtc: "2026-10-10T19:37:00Z",
  description:
    "Считаю, что предупреждение за флуд выдано несправедливо: сообщение " +
    "было по теме обсуждения. Прошу пересмотреть решение.",
  commentsCount: 2,
  resolvedUtc: null,
  resolvedByUsername: null,
  comments: [asked, replied],
  canComment: true,
  canResolve: false,
});

export const closedTicket = (): Ticket => ({
  ...ticketBase,
  id: CLOSED,
  reporter: AUTHOR,
  status: "Closed",
  subtype: "UserComplaint",
  createdUtc: "2026-10-07T13:37:00Z",
  description:
    "У пользователя LongestLoginPossible на аватаре непристойная картинка." +
    "<br /><br />Нарушитель: LongestLoginPossible" +
    "<br /><br />Ссылка на нарушение: /users/LongestLoginPossible",
  commentsCount: 1,
  resolvedUtc: "2026-10-08T09:00:00Z",
  resolvedByUsername: "TestModerator",
  comments: [
    {
      id: "6c0a1d3e-0000-4000-8000-0000000000c3",
      author: MODERATOR,
      isGuest: false,
      createdUtc: "2026-10-08T08:58:00Z",
      text: "Аватар нарушает правила сайта, пользователю выдано предупреждение.",
    },
  ],
  canComment: false,
  canResolve: false,
});

const trackedTicket = (): {
  comments: Comment[];
  [field: string]: unknown;
} => ({
  status: "WaitingForUser",
  subtype: "AccessRecovery",
  subject: "",
  description:
    "Не могу войти: пароль не подходит, а письмо для сброса не приходит.",
  createdUtc: "2026-10-11T06:12:00Z",
  resolvedUtc: null,
  resolvedByUsername: null,
  comments: [
    {
      id: "6c0a1d3e-0000-4000-8000-0000000000c4",
      author: ADMIN,
      isGuest: false,
      createdUtc: "2026-10-11T11:30:00Z",
      text: "Напишите, на какую почту был зарегистрирован аккаунт.",
    },
    {
      id: "6c0a1d3e-0000-4000-8000-0000000000c5",
      author: null,
      isGuest: true,
      createdUtc: "2026-10-11T12:05:00Z",
      text: "На старую, она больше не работает.",
    },
  ],
  canComment: true,
});

type Stand = {
  page: Page;
  /** The writes the page sent, method and path, in their order. */
  writes: string[];
  /** The token each write by the token carried in its header. */
  tokens: string[];
};

/** Answers a route with a body as JSON; a page closed meanwhile is let go. */
export const json = (route: Route, body: unknown, status = 200) =>
  route
    .fulfill({
      status,
      contentType: "application/json",
      body: JSON.stringify(body),
    })
    .catch(() => {});

type Options = {
  /** The saved session the page opens with; none for a guest. */
  storage?: string;
  /** The tickets of the page, by their number. */
  tickets?: Ticket[];
  /** Whether the reader answers each of them as moderation. */
  canResolve?: boolean;
  /** Who an answer written on the page is signed with. */
  writer?: Person;
};

/** What the page reads and writes on the wire, held between its requests. */
type Wire = {
  options: Options;
  /** The tickets of the page, by their number, as they stand now. */
  tickets: Map<string, Ticket>;
  /** The guest's ticket, by TOKEN. */
  tracked: ReturnType<typeof trackedTicket>;
  writes: string[];
  tokens: string[];
  /** A comment as the server answers it; no writer is the guest. */
  comment: (text: string, writer: Person | null) => Comment;
};

function ticketWire(options: Options): Wire {
  const tickets = new Map<string, Ticket>(
    (options.tickets ?? []).map((ticket) => [
      ticket.id,
      { ...ticket, canResolve: options.canResolve ?? false },
    ]),
  );
  let written = 0;

  const comment = (text: string, writer: Person | null): Comment => {
    written += 1;
    return {
      id: `6c0a1d3e-0000-4000-8000-${String(100 + written).padStart(12, "0")}`,
      author: writer,
      isGuest: writer === null,
      createdUtc: new Date().toISOString(),
      text,
    };
  };

  return {
    options,
    tickets,
    tracked: trackedTicket(),
    writes: [],
    tokens: [],
    comment,
  };
}

/**
 * A write of the page. An answer written by the number is answered with the
 * ticket as it stands after it, the answer at the end of its conversation and
 * the ticket in the status sent with it (waiting for the author when none
 * was, closed by the writer for "Closed" and "Spam", waiting for moderation
 * for the author's own); the guest's is answered with itself. Every other
 * write is refused on its way out.
 */
async function answerWrite(route: Route, wire: Wire) {
  const request = route.request();
  const method = request.method();
  const { pathname } = new URL(request.url());
  wire.writes.push(`${method} ${pathname}`);
  const commented = pathname.match(
    /^\/v1\/moderation\/tickets\/([^/]+)\/comments$/,
  );
  const ticket = commented && wire.tickets.get(commented[1]);
  if (method === "POST" && ticket) {
    const { text, status } = request.postDataJSON() as {
      text: string;
      status?: string;
    };
    const writer = wire.options.writer ?? AUTHOR;
    if (text)
      ticket.comments = [...ticket.comments, wire.comment(text, writer)];
    if (status === "Closed" || status === "Spam") {
      Object.assign(ticket, {
        status,
        canComment: false,
        canResolve: false,
        resolvedUtc: new Date().toISOString(),
        resolvedByUsername: writer.username,
      });
    } else {
      ticket.status = status ? status : "WaitingForModeration";
    }
    return json(route, { resource: ticket });
  }
  if (method === "POST" && pathname === "/v1/tickets/track/comments") {
    wire.tokens.push(request.headers()["x-dm-ticket-token"] ?? "");
    const { text } = request.postDataJSON() as { text: string };
    const added = wire.comment(text, null);
    wire.tracked.comments = [...wire.tracked.comments, added];
    return json(route, { resource: added }, 201);
  }
  return route.abort().catch(() => {});
}

/**
 * A read of the page: the reader's list of the tickets, one by its number,
 * the guest's by TOKEN. Every other read goes on to the stand.
 */
async function answerRead(route: Route, wire: Wire) {
  const request = route.request();
  const { pathname } = new URL(request.url());

  if (pathname === "/v1/moderation/tickets/mine") {
    // A list holds the number of the answers, not the answers.
    const list = [...wire.tickets.values()].map(({ comments, ...listed }) => ({
      ...listed,
      commentsCount: comments.length,
    }));
    return json(route, {
      resources: list,
      paging: {
        pages: 1,
        current: 1,
        skip: 0,
        take: 20,
        total: list.length,
      },
    });
  }
  const one = pathname.match(/^\/v1\/moderation\/tickets\/([^/]+)$/);
  if (one && wire.tickets.has(one[1])) {
    return json(route, { resource: wire.tickets.get(one[1]) });
  }
  if (
    pathname === "/v1/tickets/track" &&
    request.headers()["x-dm-ticket-token"] === TOKEN
  ) {
    return json(route, { resource: wire.tracked });
  }
  return route.fallback().catch(() => {});
}

/** Opens a path in a window of a width, the tickets answered here (answerRead, answerWrite). */
export async function open(
  browser: Browser,
  width: number,
  path: string,
  options: Options = {},
): Promise<Stand> {
  const context = await windowContext(browser, {
    width,
    storageState: options.storage,
  });
  const page = await context.newPage();
  const wire = ticketWire(options);

  await page.route(
    (url) => url.pathname.startsWith("/v1/"),
    async (route) => {
      const method = route.request().method();
      if (method === "OPTIONS" || method === "HEAD") {
        return route.fallback().catch(() => {});
      }
      return method === "GET"
        ? answerRead(route, wire)
        : answerWrite(route, wire);
    },
  );

  await page.goto(path);
  await settle(page);
  return { page, writes: wire.writes, tokens: wire.tokens };
}

/** Shuts the page with its reads still on the wire left unanswered. */
export async function close(page: Page) {
  await page.unrouteAll({ behavior: "ignoreErrors" });
  await page.context().close();
}

export const caption = async (page: Page) =>
  (await page.locator("main#main .record-block__caption").first().innerText())
    .replace(/\s+/g, " ")
    .trim();

export const commentTexts = (page: Page) =>
  page.locator("main#main .ticket-conversation .comment-text").allInnerTexts();

/**
 * What of a ticket is hidden from a reader: a word cut short, a box that
 * scrolls, a piece that stands out of the column, "Еще" or "…" in place of
 * what is there. The block, the answers, the lines and the choice of the
 * status are read; the editor of the form is the site's own and is not.
 */
export async function hiddenParts(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const column = document.querySelector("main#main");
    if (!column) return ["no column"];
    const edge = column.getBoundingClientRect();
    const parts = [
      ...column.querySelectorAll<HTMLElement>(
        ".ticket-thread, .ticket-conversation",
      ),
    ];
    const found: string[] = [];
    const say = (what: string, element: Element) =>
      found.push(`${what}: ${(element.textContent ?? "").trim().slice(0, 60)}`);
    for (const part of parts) {
      for (const element of [
        part,
        ...part.querySelectorAll<HTMLElement>("*"),
      ]) {
        if (element.closest(".bbcode-editor-wrapper")) continue;
        const style = getComputedStyle(element);
        const box = element.getBoundingClientRect();
        if (box.width === 0 || style.visibility === "hidden") continue;
        if (box.left < edge.left - 0.5 || box.right > edge.right + 0.5) {
          say("stands out of the column", element);
        }
        const clipped = element.scrollWidth > element.clientWidth + 1;
        if (clipped && style.textOverflow === "ellipsis")
          say("cut short", element);
        if (clipped && /auto|scroll/.test(style.overflowX))
          say("scrolls", element);
      }
      const words = [...part.querySelectorAll<HTMLElement>("*")]
        .filter((element) => !element.closest(".bbcode-editor-wrapper"))
        .filter((element) => element.children.length === 0)
        .map((element) => element.innerText ?? "")
        .join(" ");
      if (/…|(^|\s)Еще(\s|$)/.test(words)) say("shortened", part);
    }
    return found;
  });
}

/**
 * The elements of a ticket coloured red or green, by their words. The letter
 * of a role and [online] keep their green, as by every name of the site, and
 * the star of a ticket that waits for the reader its red. The colours are read
 * as the browser computed them and compared with the two accents as the theme
 * of the page resolves them.
 */
export async function colouredParts(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const resolved = (name: string) => {
      const probe = document.createElement("span");
      probe.style.color = `var(${name})`;
      document.body.append(probe);
      const colour = getComputedStyle(probe).color;
      probe.remove();
      return colour;
    };
    const accents = new Set([
      resolved("--accent-red"),
      resolved("--accent-green"),
    ]);
    const parts = document.querySelectorAll<HTMLElement>(
      "main#main .ticket-thread, main#main .ticket-conversation",
    );
    return [...parts]
      .flatMap((part) => [part, ...part.querySelectorAll<HTMLElement>("*")])
      .filter((element) => !element.closest(".role-letter, .online, .star"))
      .filter((element) => !element.closest(".bbcode-editor-wrapper"))
      .filter((element) => {
        const style = getComputedStyle(element);
        return [
          style.color,
          style.backgroundColor,
          style.borderTopColor,
          style.textDecorationColor,
        ].some((colour) => accents.has(colour));
      })
      .map((element) => (element.textContent ?? "").trim().slice(0, 60));
  });
}

/** The inset, the frame and the ground of a block, as computed. */
export const frameOf = (page: Page, selector: string) =>
  page
    .locator(selector)
    .first()
    .evaluate((element) => {
      const style = getComputedStyle(element);
      return {
        padding: style.padding,
        border: `${style.borderTopWidth} ${style.borderTopStyle} ${style.borderTopColor}`,
        background: style.backgroundColor,
      };
    });

/** Writes into the form of the conversation, in its visual mode. */
export async function write(page: Page, text: string) {
  const field = page.locator(
    "main#main .ticket-answer-form .wysiwyg-content [contenteditable='true']",
  );
  await field.click();
  await page.keyboard.type(text);
}

export const press = (page: Page, words: string) =>
  page
    .locator("main#main")
    .getByRole("button", { name: words, exact: true })
    .click();
