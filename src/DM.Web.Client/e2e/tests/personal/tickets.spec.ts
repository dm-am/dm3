import {
  test,
  expect,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";
import { PHONES, phoneContext } from "../../fixtures/phone";
import type { Browser, Page, Route } from "@playwright/test";

/**
 * A ticket works as a topic with answers and looks like one, and nobody takes
 * it in hand. The page of a ticket is the page of a topic: the block of the
 * ticket in the frame of a topic, its caption "Автор: имя, дата | Ответы (N) |
 * состояние", the answers of the author and of moderation under it as compact
 * comments, read only, and the form at their foot while the ticket is open,
 * its counter at the 10000 the server takes. A closed ticket has no form and
 * says in one plate who closed it. A moderator chooses over the field of his
 * answer the status it moves the ticket to, "Ожидает ответа пользователя"
 * unless he closes it or marks it as spam, and the text and the status go in
 * one request; there is no block "Решение", no warning and no ban. The
 * guest's page by its token is the same page, his words signed "Гость".
 *
 * On a phone of 360, 390 and 430 nothing of a ticket is hidden: no "Еще", no
 * "…", no line cut short, nothing standing out of the column or scrolled in a
 * box of its own. The editor of the form is the site's own and is not read
 * here. Whether the page is wider than the window is read once for every page
 * of the site, in common/page-width.spec.ts. Nothing is red or green but the
 * letter of a role and [online], as by every name of the site, and the red
 * "*" after the state of a ticket that waits for the reader.
 *
 * The tickets are answered here in the shape of the wire, so the lines read
 * are known whatever the seed holds, and the writes are answered here too:
 * nothing is written to the stand. One test reads the guest ticket the seed
 * writes with a known token, as it stands on the stand.
 */

// The stand is shared and answers 429 past 300 requests a minute; a read is
// asked again after a wait (fetchPastLimit).
test.describe.configure({ timeout: 120_000 });

/** The phones by the width of their screens, and the full version. */
const WIDTHS = [...PHONES, 1366];

const OPEN = "6c0a1d3e-0000-4000-8000-0000000000a1";
const CLOSED = "6c0a1d3e-0000-4000-8000-0000000000a2";
const TOKEN = "e2e0e2e0e2e0e2e0e2e0e2e0e2e0e2e0";
/** The open guest ticket of the seed (DataSeeder.GuestTicketToken). */
const SEEDED_TOKEN = "5eedc0de5eedc0de5eedc0de5eedc0de";

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
const ADMIN = person(4, "SolohinLex", "Admin");

type Comment = {
  id: string;
  author: Person | null;
  isGuest: boolean;
  createdUtc: string;
  text: string;
};

/** A ticket of the page, in the shape of the wire (TicketDetails). */
type Ticket = { id: string; comments: Comment[]; [field: string]: unknown };

const asked: Comment = {
  id: "6c0a1d3e-0000-4000-8000-0000000000c1",
  author: SENIOR,
  isGuest: false,
  createdUtc: "2026-10-11T07:15:00Z",
  text:
    "Пришлите, пожалуйста, ссылку на сообщение, за которое выдано " +
    "предупреждение: в логе за этот день у вас их два.",
};

const replied: Comment = {
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
  description: "",
};

const openTicket = (): Ticket => ({
  ...ticketBase,
  id: OPEN,
  reporter: AUTHOR,
  status: "WaitingForModeration",
  subtype: "ModeratorDecisionComplaint",
  createdUtc: "2026-10-10T19:37:00Z",
  descriptionHtml:
    "Считаю, что предупреждение за флуд выдано несправедливо: сообщение " +
    "было по теме обсуждения. Прошу пересмотреть решение.",
  commentsCount: 2,
  resolvedUtc: null,
  resolvedByUsername: null,
  comments: [asked, replied],
  canComment: true,
  canResolve: false,
});

const closedTicket = (): Ticket => ({
  ...ticketBase,
  id: CLOSED,
  reporter: AUTHOR,
  status: "Closed",
  subtype: "UserComplaint",
  createdUtc: "2026-10-07T13:37:00Z",
  descriptionHtml:
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
  description: "",
  descriptionHtml:
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

const json = (route: Route, body: unknown, status = 200) =>
  route
    .fulfill({
      status,
      contentType: "application/json",
      body: JSON.stringify(body),
    })
    .catch(() => {});

/**
 * A read sent on to the stand and asked again past its rate limit; null once
 * the page it was for is gone, which a test that is over leaves behind.
 */
async function fetchPastLimit(route: Route) {
  try {
    let response = await route.fetch();
    for (let i = 0; i < 10 && response.status() === 429; i += 1) {
      await new Promise((resolve) => setTimeout(resolve, 3000));
      response = await route.fetch();
    }
    return response;
  } catch {
    return null;
  }
}

async function passOn(route: Route) {
  const response = await fetchPastLimit(route);
  if (response) await route.fulfill({ response }).catch(() => {});
}

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

/**
 * Opens a path on a phone of a width, as a phone lays the page out
 * (fixtures/phone.ts), or in a window of the full version, the tickets
 * answered here: one by its number, the reader's list of them, the guest's by
 * TOKEN. An answer written by the number is answered with the ticket as it
 * stands after it, the answer at the
 * end of its conversation and the ticket in the status sent with it (waiting
 * for the author when none was, closed by the writer for "Closed" and "Spam",
 * waiting for moderation for the author's own); the guest's is answered with
 * itself. Every other write is refused on its way out, and every other read
 * goes on to the stand.
 */
async function open(
  browser: Browser,
  width: number,
  path: string,
  options: Options = {},
): Promise<Stand> {
  const context =
    width < 768
      ? await phoneContext(browser, { width, storageState: options.storage })
      : await browser.newContext({
          ...(options.storage ? { storageState: options.storage } : {}),
          viewport: { width, height: 900 },
        });
  const page = await context.newPage();
  const writes: string[] = [];
  const tokens: string[] = [];
  const tickets = new Map<string, Ticket>(
    (options.tickets ?? []).map((ticket) => [
      ticket.id,
      { ...ticket, canResolve: options.canResolve ?? false },
    ]),
  );
  const tracked = trackedTicket();
  let written = 0;

  /** A comment as the server answers it; no writer is the guest. */
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

  await page.route(
    (url) => url.pathname.startsWith("/v1/"),
    async (route) => {
      const request = route.request();
      const method = request.method();
      const { pathname } = new URL(request.url());
      const token = request.headers()["x-dm-ticket-token"];

      if (method === "OPTIONS" || method === "HEAD") {
        return route.fallback().catch(() => {});
      }
      if (method !== "GET") {
        writes.push(`${method} ${pathname}`);
        const commented = pathname.match(
          /^\/v1\/moderation\/tickets\/([^/]+)\/comments$/,
        );
        const ticket = commented && tickets.get(commented[1]);
        if (method === "POST" && ticket) {
          const { text, status } = request.postDataJSON() as {
            text: string;
            status?: string;
          };
          const writer = options.writer ?? AUTHOR;
          if (text)
            ticket.comments = [...ticket.comments, comment(text, writer)];
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
          tokens.push(token ?? "");
          const { text } = request.postDataJSON() as { text: string };
          const added = comment(text, null);
          tracked.comments = [...tracked.comments, added];
          return json(route, { resource: added }, 201);
        }
        return route.abort().catch(() => {});
      }

      if (pathname === "/v1/moderation/tickets/mine") {
        // A list holds the number of the answers, not the answers.
        const list = [...tickets.values()].map(({ comments, ...listed }) => ({
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
      if (one && tickets.has(one[1])) {
        return json(route, { resource: tickets.get(one[1]) });
      }
      if (pathname === "/v1/tickets/track" && token === TOKEN) {
        return json(route, { resource: tracked });
      }
      return passOn(route);
    },
  );

  await page.goto(path);
  await page.evaluate(() => document.fonts.ready);
  return { page, writes, tokens };
}

/** Shuts the page with its reads still on the wire left unanswered. */
async function close(page: Page) {
  await page.unrouteAll({ behavior: "ignoreErrors" });
  await page.context().close();
}

const caption = async (page: Page) =>
  (await page.locator("main#main .record-block__caption").first().innerText())
    .replace(/\s+/g, " ")
    .trim();

const commentTexts = (page: Page) =>
  page.locator("main#main .ticket-conversation .comment-text").allInnerTexts();

/**
 * What of a ticket is hidden from a reader: a word cut short, a box that
 * scrolls, a piece that stands out of the column, "Еще" or "…" in place of
 * what is there. The block, the answers, the lines and the choice of the
 * status are read; the editor of the form is the site's own and is not.
 */
async function hiddenParts(page: Page): Promise<string[]> {
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
async function colouredParts(page: Page): Promise<string[]> {
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
const frameOf = (page: Page, selector: string) =>
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
async function write(page: Page, text: string) {
  const field = page.locator(
    "main#main .ticket-answer-form .wysiwyg-content [contenteditable='true']",
  );
  await field.click();
  await page.keyboard.type(text);
}

const press = (page: Page, words: string) =>
  page
    .locator("main#main")
    .getByRole("button", { name: words, exact: true })
    .click();

test.describe("The page of a ticket, as a topic with answers", () => {
  for (const width of WIDTHS) {
    test(`reads as a topic to its author, nothing hidden and nothing coloured, at ${width}`, async ({
      browser,
    }) => {
      const { page } = await open(browser, width, `/my-tickets/${OPEN}`, {
        storage: SECONDARY_STORAGE_STATE,
        tickets: [openTicket()],
      });
      await expect(page.locator("main#main .ticket-thread")).toBeVisible({
        timeout: 20000,
      });

      expect(await caption(page)).toMatch(
        /^Автор: TestUser \[online\], .+ \| Ответы \(2\) \| Ожидает ответа модерации$/,
      );
      await expect(
        page.locator("main#main .ticket-conversation .comment.compact"),
      ).toHaveCount(2);
      expect(await commentTexts(page)).toEqual([asked.text, replied.text]);
      await expect(
        page.locator("main#main .ticket-conversation .comment-number"),
      ).toHaveText(["1", "2"]);
      await expect(
        page.locator("main#main .ticket-conversation"),
      ).not.toContainText(/Редактировать|Удалить|Предупреждение/);
      await expect(page.locator("main#main .like-badge")).toHaveCount(0);
      await expect(page.locator("main#main .ticket-answer-form")).toContainText(
        "0 / 10000",
      );
      // The author chooses no status: his answer hands the ticket on.
      await expect(
        page.locator("main#main .ticket-answer-form select"),
      ).toHaveCount(0);
      expect(await hiddenParts(page)).toEqual([]);
      expect(await colouredParts(page)).toEqual([]);
      await close(page);
    });
  }

  test("takes the author's answer at the end and hands the turn to moderation", async ({
    browser,
  }) => {
    const { page, writes } = await open(browser, 390, `/my-tickets/${OPEN}`, {
      storage: SECONDARY_STORAGE_STATE,
      tickets: [{ ...openTicket(), status: "WaitingForUser" }],
    });
    await expect(page.locator("main#main .ticket-answer-form")).toBeVisible({
      timeout: 20000,
    });

    await write(page, "Ссылка на сообщение: /forum/general/5");
    await press(page, "Отправить");

    await expect(
      page.locator("main#main .ticket-conversation .comment-text").last(),
    ).toHaveText("Ссылка на сообщение: /forum/general/5");
    await expect(
      page.locator("main#main .ticket-conversation .comment-number").last(),
    ).toHaveText("3");
    expect(await caption(page)).toMatch(
      /Ответы \(3\) \| Ожидает ответа модерации$/,
    );
    expect(writes).toEqual([`POST /v1/moderation/tickets/${OPEN}/comments`]);
    await close(page);
  });

  test("offers no form on a closed ticket and says who closed it", async ({
    browser,
  }) => {
    const { page } = await open(browser, 390, `/my-tickets/${CLOSED}`, {
      storage: SECONDARY_STORAGE_STATE,
      tickets: [closedTicket()],
    });
    await expect(page.locator("main#main .ticket-thread")).toBeVisible({
      timeout: 20000,
    });

    await expect(page.locator("main#main .ticket-answer-form")).toHaveCount(0);
    await expect(
      page.locator("main#main .ticket-conversation .refusal-note"),
    ).toHaveText(/^Обращение закрыто .+, закрыл\(а\) TestModerator\.$/);
    expect(await hiddenParts(page)).toEqual([]);
    await close(page);
  });

  // The frame, the ground and the inset of the block are those of a topic.
  test("draws the block in the frame of a topic", async ({ browser }) => {
    const ticket = await open(browser, 1366, `/my-tickets/${OPEN}`, {
      storage: SECONDARY_STORAGE_STATE,
      tickets: [openTicket()],
    });
    await expect(ticket.page.locator("main#main .record-block")).toBeVisible({
      timeout: 20000,
    });
    const ofTicket = await frameOf(
      ticket.page,
      "main#main .ticket-thread .record-block",
    );
    await close(ticket.page);

    const topic = await open(browser, 1366, "/forum/general/5", {
      storage: SECONDARY_STORAGE_STATE,
    });
    await expect(topic.page.locator("main#main .topic").first()).toBeVisible({
      timeout: 20000,
    });
    const ofTopic = await frameOf(topic.page, "main#main .topic");
    await close(topic.page);

    expect(ofTicket).toEqual(ofTopic);
  });
});

test.describe("Мои обращения", () => {
  for (const width of [390, 1366]) {
    test(`name the answers and the turn of each, and neither the author nor a colour, at ${width}`, async ({
      browser,
    }) => {
      const { page } = await open(browser, width, "/my-tickets", {
        storage: SECONDARY_STORAGE_STATE,
        tickets: [openTicket(), closedTicket()],
      });
      await expect(page.locator("main#main .ticket-thread")).toHaveCount(2, {
        timeout: 20000,
      });

      const captions = (
        await page.locator("main#main .record-block__caption").allInnerTexts()
      ).map((text) => text.replace(/\s+/g, " ").trim());
      expect(captions).toEqual([
        expect.stringMatching(
          /^\d{2}\.\d{2}\.\d{4} .+ \| Ответы \(2\) \| Ожидает ответа модерации$/,
        ),
        expect.stringMatching(
          /^\d{2}\.\d{2}\.\d{4} .+ \| Ответы \(1\) \| Закрыто$/,
        ),
      ]);
      expect(await hiddenParts(page)).toEqual([]);
      expect(await colouredParts(page)).toEqual([]);
      await close(page);
    });
  }
});

test.describe("A moderator's page of a ticket", () => {
  for (const width of WIDTHS) {
    test(`offers the status over the field of the answer, nobody taking it in hand, at ${width}`, async ({
      browser,
    }) => {
      const { page } = await open(
        browser,
        width,
        `/moderation/tickets/${OPEN}`,
        {
          storage: PRIMARY_STORAGE_STATE,
          tickets: [openTicket()],
          canResolve: true,
          writer: ADMIN,
        },
      );
      const status = page.locator("main#main .ticket-answer-form select");
      await expect(status).toBeVisible({ timeout: 20000 });

      await expect(page.locator("main#main")).not.toContainText(
        /Взять в работу|Разбирает|В работе|Решение|Закрыть обращение|Выдать предупреждение|Выдать бан/,
      );
      // One editor on the page, the form's, and the status over it.
      await expect(
        page.locator("main#main .bbcode-editor-wrapper"),
      ).toHaveCount(1);
      await expect(status).toHaveValue("WaitingForUser");
      await expect(status.locator("option")).toHaveText([
        "Ожидает ответа пользователя",
        "Закрыто",
        "Спам",
      ]);
      const order = await page.evaluate(() => {
        const choice = document.querySelector(
          "main#main .ticket-answer-form select",
        );
        const field = document.querySelector(
          "main#main .ticket-answer-form .bbcode-editor-wrapper",
        );
        return choice && field
          ? choice.compareDocumentPosition(field) &
              Node.DOCUMENT_POSITION_FOLLOWING
          : 0;
      });
      expect(order, "the status stands over the field").toBeTruthy();
      // The ticket waits for moderation, and the reader answers as moderation.
      await expect(
        page.locator("main#main .record-block__caption .star"),
      ).toHaveText("*");
      expect(await hiddenParts(page)).toEqual([]);
      expect(await colouredParts(page)).toEqual([]);
      await close(page);
    });
  }

  test("closes with the last word in one request", async ({ browser }) => {
    const { page, writes } = await open(
      browser,
      390,
      `/moderation/tickets/${OPEN}`,
      {
        storage: PRIMARY_STORAGE_STATE,
        tickets: [openTicket()],
        canResolve: true,
        writer: ADMIN,
      },
    );
    const status = page.locator("main#main .ticket-answer-form select");
    await expect(status).toBeVisible({ timeout: 20000 });

    await status.selectOption("Closed");
    await write(page, "Предупреждение снято, извините за ошибку.");
    await press(page, "Отправить");

    await expect(
      page.locator("main#main .ticket-conversation .refusal-note"),
    ).toHaveText(/^Обращение закрыто .+, закрыл\(а\) SolohinLex\.$/);
    expect(writes).toEqual([`POST /v1/moderation/tickets/${OPEN}/comments`]);
    expect((await commentTexts(page)).at(-1)).toBe(
      "Предупреждение снято, извините за ошибку.",
    );
    await expect(page.locator("main#main .ticket-answer-form")).toHaveCount(0);
    await close(page);
  });

  test("closes as spam without a word", async ({ browser }) => {
    const { page, writes } = await open(
      browser,
      1366,
      `/moderation/tickets/${OPEN}`,
      {
        storage: PRIMARY_STORAGE_STATE,
        tickets: [openTicket()],
        canResolve: true,
        writer: ADMIN,
      },
    );
    const status = page.locator("main#main .ticket-answer-form select");
    await expect(status).toBeVisible({ timeout: 20000 });

    await status.selectOption("Spam");
    await press(page, "Отправить");

    await expect(
      page.locator("main#main .ticket-conversation .refusal-note"),
    ).toHaveText(/^Обращение закрыто как спам .+, закрыл\(а\) SolohinLex\.$/);
    expect(writes).toEqual([`POST /v1/moderation/tickets/${OPEN}/comments`]);
    expect(await commentTexts(page)).toEqual([asked.text, replied.text]);
    await close(page);
  });
});

test.describe("The guest's page of a ticket", () => {
  for (const width of PHONES) {
    test(`signs the guest Гость and hides nothing, at ${width}`, async ({
      browser,
    }) => {
      const { page } = await open(browser, width, `/support/track/${TOKEN}`);
      await expect(page.locator("main#main .ticket-thread")).toBeVisible({
        timeout: 20000,
      });

      expect(await caption(page)).toMatch(
        /^Автор: Гость, .+ \| Ответы \(2\) \| Ожидает ответа пользователя$/,
      );
      const guest = page
        .locator("main#main .ticket-conversation .comment")
        .last()
        .locator(".author-info");
      await expect(guest).toHaveText(/^Автор:\s+Гость$/);
      await expect(guest.locator("a")).toHaveCount(0);
      await expect(page.locator("main#main .ticket-answer-form")).toContainText(
        "0 / 10000",
      );
      // The address of the page is an ordinary link in one sentence, and
      // nothing copies it. Read as the reader sees it: the text of the
      // paragraph also holds the line breaks of its template at both ends.
      await expect(page.locator("main#main .lead-text")).toHaveText(
        /^Ответ появится на этой странице: http\S+\/support\/track\/e2e0e2e0e2e0e2e0e2e0e2e0e2e0e2e0\. Сохраните ссылку, чтобы вернуться к обращению\.$/,
        { useInnerText: true },
      );
      await expect(page.locator("main#main")).not.toContainText(
        /Скопировать|номер обращения/i,
      );
      expect(await hiddenParts(page)).toEqual([]);
      expect(await colouredParts(page)).toEqual([]);
      await close(page);
    });
  }

  test("writes by the token in its header, never in the address", async ({
    browser,
  }) => {
    const { page, writes, tokens } = await open(
      browser,
      390,
      `/support/track/${TOKEN}`,
    );
    await expect(page.locator("main#main .ticket-answer-form")).toBeVisible({
      timeout: 20000,
    });

    await write(page, "Почта была на старом адресе, он больше не работает.");
    await press(page, "Отправить");

    await expect(
      page.locator("main#main .ticket-conversation .comment-text").last(),
    ).toHaveText("Почта была на старом адресе, он больше не работает.");
    expect(writes).toEqual(["POST /v1/tickets/track/comments"]);
    expect(tokens).toEqual([TOKEN]);
    expect(await caption(page)).toMatch(/Ожидает ответа модерации$/);
    await close(page);
  });

  // The ticket as the seed writes it, read from the stand: a request for
  // access, open, with one word the guest added.
  test("reads the seeded guest ticket by its known token", async ({
    browser,
  }) => {
    const { page } = await open(browser, 390, `/support/track/${SEEDED_TOKEN}`);
    await expect(page.locator("main#main .ticket-thread")).toBeVisible({
      timeout: 20000,
    });

    await expect(page.locator("main#main .record-block__title")).toHaveText(
      "Восстановление доступа",
    );
    expect(await caption(page)).toMatch(/^Автор: Гость, /);
    expect(await commentTexts(page)).toEqual([
      "Уточню: старая почта больше не работает, логин в аккаунте тот же, что в письме.",
    ]);
    await expect(page.locator("main#main .ticket-answer-form")).toContainText(
      "0 / 10000",
    );
    await close(page);
  });
});
