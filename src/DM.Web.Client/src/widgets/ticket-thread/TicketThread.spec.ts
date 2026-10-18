/**
 * @vitest-environment jsdom
 */

/**
 * One ticket, drawn once for every reader: the block over its conversation.
 *
 * A ticket works as a topic with answers and looks like one, and "Разбирает"
 * is not needed, any senior may take a ticket, nor green and red where nothing
 * is good or punished. So the block is the topic's: the reason as its
 * heading, the text the author wrote and nothing beside it, and under it the
 * caption of a topic, "Автор: имя [online], дата | Ответы (N) | состояние",
 * the number of answers a link as the number of comments of a topic is. All
 * of it grey but the idioms of every name on the site, the letter of the role
 * and [online], and one mark: the red "*" after the state of a ticket that
 * waits for the reader's answer, the star of a room that waits for his move.
 *
 * Checked here: the caption made of parts and who signs it (an account, a
 * removed account, a guest), the four states in the words they had before
 * 5 October and in no colour, the star, the link of the number of answers,
 * nothing drawn beside the text, nothing left of the assignment, the fold of
 * a ticket in a list, the address of a complaint as a link and the text set
 * as the text of a comment.
 */
import { describe, it, expect } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { removedAccountRoute } from "@/entities/user";
import type {
  Ticket,
  TicketComment,
  TicketDetails,
  TrackedTicket,
} from "@/entities/ticket";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import TicketThread from "./TicketThread.vue";
import SOURCE from "./TicketThread.vue?raw";

const HERE = dirname(fileURLToPath(import.meta.url));

const CREATED = "2026-10-03T17:38:00Z";

/** A moment within the threshold of [online]. */
const justNow = () => new Date().toISOString();

const complaint = (over: Partial<Ticket> = {}): Ticket =>
  ({
    id: "t-1",
    reporter: {
      username: "Experienced",
      role: "RegularUser",
      lastActivityUtc: justNow(),
    },
    isGuest: false,
    status: "WaitingForModeration",
    subtype: "UserComplaint",
    createdUtc: CREATED,
    description:
      "Оскорбляет [b]участников[/b]\n\nНарушитель: LongestLoginPossible",
    descriptionHtml:
      "Оскорбляет <strong>участников</strong><br /><br />Нарушитель: LongestLoginPossible",
    comment: "",
    commentsCount: 2,
    ...over,
  }) as Ticket;

const tracked = (over: Partial<TrackedTicket> = {}): TrackedTicket => ({
  status: "WaitingForUser",
  subtype: "AccessRecovery",
  subject: "",
  description: "Не могу войти",
  descriptionHtml: "Не могу войти",
  createdUtc: CREATED,
  resolvedUtc: null,
  resolvedByUsername: null,
  comments: [
    {
      id: "c-1",
      author: null,
      isGuest: true,
      createdUtc: CREATED,
      text: "Почта та же",
    },
  ],
  canComment: true,
  ...over,
});

const render = (
  ticket: Ticket | TicketDetails | TrackedTicket,
  props: {
    withAuthor?: boolean;
    to?: unknown;
    answersTo?: unknown;
    awaited?: boolean;
  } = {},
) =>
  mount(TicketThread, {
    props: { ticket, ...props } as never,
    global: { stubs: { RouterLink: RouterLinkStub } },
  });

/** Spaces as a reader copies them: the date's own are not breaking ones. */
const norm = (value: string) => value.replace(/\s+/g, " ").trim();
const text = (el: { text(): string }) => norm(el.text());

/**
 * The parts of the caption as a reader sees them. Runs of layout whitespace
 * are one space; the no-break space a date keeps after "в" is text and stays.
 */
const parts = (wrapper: ReturnType<typeof render>) =>
  [
    ...wrapper.element.querySelectorAll(
      ".record-block__caption .caption-line > .caption-line__part",
    ),
  ].map((part) =>
    (part.textContent ?? "").replace(/[^\S\u00a0]+/g, " ").trim(),
  );

describe("the block of a ticket", () => {
  it("is headed by its reason and reads its text as the server rendered it", () => {
    const wrapper = render(complaint());

    expect(text(wrapper.get(".record-block__title"))).toBe(
      "Жалоба на пользователя",
    );
    expect(wrapper.get(".ticket-body strong").text()).toBe("участников");
    expect(wrapper.text()).not.toContain("[b]");
    // On its own page the reason leads nowhere: the reader is there.
    expect(wrapper.find(".record-block__title a").exists()).toBe(false);
  });

  it("has nothing under it: what moderation answered is the conversation's", () => {
    const wrapper = render(
      complaint({ status: "Closed", resolvedUtc: "2026-10-04T07:15:00Z" }),
    );

    expect(wrapper.find(".record-verdict-list").exists()).toBe(false);
    expect(wrapper.find(".record-verdict").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Нарушителю");
  });
});

/**
 * The caption of a topic (TopicCard): the author with the letter of his role
 * and his presence, a comma, the date to the minute, the number of comments
 * and the state in a word. A phone breaks the line after the comma and keeps
 * the date whole, as a topic's does.
 */
describe("the caption of a ticket", () => {
  it("names the author, the date, the comments and the state, in that order", () => {
    expect(parts(render(complaint()))).toEqual([
      "Автор: Experienced [online],",
      formatDateFull(CREATED),
      "Ответы (2)",
      "Ожидает ответа модерации",
    ]);
  });

  it("leads from the author's name to his profile", () => {
    const link = render(complaint())
      .findAllComponents(RouterLinkStub)
      .find((one) => one.element.closest(".record-block__caption"));

    expect(link?.props("to")).toEqual({
      name: "profile",
      params: { username: "Experienced" },
    });
    expect(link?.text()).toBe("Experienced");
  });

  it("writes the letter of a staff author's role, as the caption of a topic does", () => {
    const wrapper = render(
      complaint({
        reporter: {
          username: "TestModerator",
          role: "Moderator",
          lastActivityUtc: null,
        } as never,
      }),
    );

    expect(parts(wrapper)[0]).toBe("Автор: TestModerator [M] [offline],");
  });

  // A removed account and a guest both arrive without a reporter; the flag of
  // the wire is what tells them apart, never the missing name.
  it("signs the ticket of a removed account with the link every such name has", () => {
    const wrapper = render(complaint({ reporter: null, isGuest: false }));

    expect(parts(wrapper)[0]).toBe("Автор: Аккаунт удален,");
    const link = wrapper
      .findAllComponents(RouterLinkStub)
      .find((one) => one.element.closest(".record-block__caption"));
    expect(link?.props("to")).toEqual(removedAccountRoute);
  });

  // The word the guest signs his comments with under the block (CommentItem),
  // written as a name is, as "Аккаунт удален" is in the same place: on the
  // guest's page in the compact layout "Автор: гость" over "Автор: Гость"
  // read as a slip (review C6a, 3).
  it("signs the ticket of a guest with a word and no link", () => {
    const wrapper = render(
      complaint({
        reporter: null,
        isGuest: true,
        guestEmail: "guest@example.com",
        subtype: "AccessRecovery",
      }),
    );

    expect(parts(wrapper)[0]).toBe("Автор: Гость,");
    expect(
      wrapper
        .findAllComponents(RouterLinkStub)
        .filter((one) => one.element.closest(".record-block__caption")),
    ).toEqual([]);
  });

  // The guest's own page reads the ticket by its token, and that wire has no
  // reporter and no flag: every ticket read that way is a guest's.
  it("signs a ticket read by its token as the guest's, and counts its conversation", () => {
    expect(parts(render(tracked()))).toEqual([
      "Автор: Гость,",
      formatDateFull(CREATED),
      "Ответы (1)",
      "Ожидает ответа пользователя",
    ]);
  });

  // The page appends a comment it sent to the conversation it holds; the
  // count it was sent with is the one before that.
  it("counts the conversation the page holds over the count it was sent", () => {
    const comment: TicketComment = {
      id: "c-1",
      author: null,
      isGuest: false,
      createdUtc: CREATED,
      text: "Вот ссылка",
    };
    const details: TicketDetails = {
      ...complaint({ commentsCount: 1 }),
      comments: [comment, { ...comment, id: "c-2" }],
      canComment: true,
      canResolve: false,
      resolvedByUsername: null,
    };

    expect(parts(render(details))[2]).toBe("Ответы (2)");
  });

  it("leaves the author out of the reader's own list", () => {
    const wrapper = render(complaint(), {
      withAuthor: false,
      to: { name: "my-ticket", params: { ticketId: "t-1" } },
    });

    expect(parts(wrapper)).toEqual([
      formatDateFull(CREATED),
      "Ответы (2)",
      "Ожидает ответа модерации",
    ]);
  });

  it("is made of plain parts, with nothing queued to be put away", () => {
    const caption = render(complaint()).get(".record-block__caption");
    const queued = [caption.element, ...caption.element.querySelectorAll("*")]
      .flatMap((element) => [...element.attributes])
      .filter((attribute) => attribute.name.startsWith("data-fit"));

    expect(queued).toEqual([]);
  });
});

/**
 * The words the states had before 5 October: "Ждет ответа" without saying
 * whose turn it is lies once both sides write, and "В работе" was the
 * assignment, which is gone. None of them has a colour of its own.
 */
describe("the state of a ticket", () => {
  it.each([
    ["WaitingForModeration", "Ожидает ответа модерации"],
    ["WaitingForUser", "Ожидает ответа пользователя"],
    ["Closed", "Закрыто"],
    ["Spam", "Спам"],
  ] as const)("%s reads %s, in the grey of the caption", (status, word) => {
    const wrapper = render(complaint({ status }));
    const part = wrapper.findAll(
      ".record-block__caption .caption-line > .caption-line__part",
    )[3];

    expect(text(part)).toBe(word);
    expect(part.find("[class]").exists()).toBe(false);
  });

  // The two colours of the site that are not grey stand only where every name
  // of the site has them, the letter of the role and [online], and the red of
  // the star by a room that waits for the reader's move.
  it("is coloured nowhere but by a name and by the star", () => {
    const rules = styleRules(componentCss(resolve(HERE, "TicketThread.vue")));
    const coloured = rules
      .filter((rule) =>
        /accent-(green|red)/.test(rule.declarations.color ?? ""),
      )
      .map((rule) => [rule.selector, rule.declarations.color]);

    expect(coloured).toEqual([
      [expect.stringMatching(/\.role-letter/), "var(--accent-green)"],
      [expect.stringMatching(/\.online/), "var(--accent-green)"],
      [expect.stringMatching(/\.star/), "var(--accent-red)"],
    ]);
  });
});

/**
 * The red "*" after the state, with a space before it, where the ticket waits
 * for the reader's answer: the star of a room that waits for his move
 * (GameRoomLink). Whom it waits for is the page's to say (awaitsReader).
 */
describe("the star of a ticket that waits for the reader", () => {
  const state = (wrapper: ReturnType<typeof render>) =>
    wrapper.findAll(
      ".record-block__caption .caption-line > .caption-line__part",
    )[3];

  it("stands after the state, a space apart, and names what it means", () => {
    const part = state(render(complaint(), { awaited: true }));
    const star = part.get(".star");

    expect(part.text()).toBe("Ожидает ответа модерации *");
    expect(star.text()).toBe("*");
    expect(star.attributes("aria-label")).toBe("Ожидает вашего ответа");
  });

  it("is not there where the ticket waits for somebody else", () => {
    const part = state(render(complaint()));

    expect(part.find(".star").exists()).toBe(false);
    expect(part.text()).toBe("Ожидает ответа модерации");
  });
});

/**
 * The number of answers is a link to the ticket's page, as the number of
 * comments of a topic is (TopicCard): in a list where the heading leads, on
 * the page of one ticket where the page names itself.
 */
describe("the number of answers", () => {
  const link = (wrapper: ReturnType<typeof render>) =>
    wrapper
      .findAllComponents(RouterLinkStub)
      .find((one) => one.classes("answers-link"));

  it("leads in a list where the heading leads", () => {
    const to = { name: "my-ticket", params: { ticketId: "t-1" } };
    const counter = link(render(complaint(), { to }));

    expect(counter?.props("to")).toEqual(to);
    expect(counter?.text()).toBe("2");
  });

  it("leads on the page of one ticket where the page names itself", () => {
    const self = { name: "moderation-ticket", params: { ticketId: "t-1" } };
    const counter = link(render(complaint(), { answersTo: self }));

    expect(counter?.props("to")).toEqual(self);
  });

  it("is a number alone where it is told no place", () => {
    const wrapper = render(complaint());

    expect(link(wrapper)).toBeUndefined();
    expect(parts(wrapper)[2]).toBe("Ответы (2)");
  });
});

/**
 * The text is the author's and nothing else: what the form asked besides it
 * the server wrote into it as lines of text, so the block draws no lines of its
 * own over it and adds none to it, nothing grey and nothing small. The address
 * a guest left is one of those lines (TicketService); the copy of it the wire
 * of moderation carries beside the text is not drawn a second time.
 */
describe("what a ticket says", () => {
  it("is its text, the violator a line of it", () => {
    const wrapper = render(complaint());

    expect(text(wrapper.get(".ticket-body"))).toBe(
      "Оскорбляет участниковНарушитель: LongestLoginPossible",
    );
    expect(wrapper.find(".record-block__details").exists()).toBe(false);
    expect(SOURCE).not.toMatch(/#details|targetUsername/);
  });

  it("adds nothing to the text, the address a guest left included", () => {
    const guest = complaint({
      reporter: null,
      isGuest: true,
      guestEmail: "guest@example.com",
      subtype: "AccessRecovery",
      descriptionHtml:
        "Не могу войти<br /><br />Контакт для ответа: guest@example.com",
    });
    const body = render(guest).get(".ticket-body");

    expect(body.element.innerHTML).toBe(
      "Не могу войти<br><br>Контакт для ответа: guest@example.com",
    );
    expect(render(guest).find(".record-block__details").exists()).toBe(false);
  });

  it("adds no line where no address was left", () => {
    expect(render(tracked()).get(".ticket-body").text()).toBe("Не могу войти");
  });

  // Nobody takes a ticket in hand any more: any moderator to whom its reason
  // is shown answers and closes it.
  it("says nothing of who took it or who answered it", () => {
    const closed = {
      ...complaint({ status: "Closed" }),
      assignedModeratorUsername: "TestModerator",
      answerAuthorUsername: "TestSeniorMod",
    } as Ticket;
    const wrapper = render(closed);

    expect(wrapper.text()).not.toMatch(/Разбирает|Разобрал|В работе/);
    expect(SOURCE).not.toMatch(/assigned|Разбирает|Разобрал|В работе/i);
  });
});

describe("a ticket in a list", () => {
  const to = { name: "my-ticket", params: { ticketId: "t-1" } };

  it("leads from its reason to its page", () => {
    const link = render(complaint(), { to })
      .findAllComponents(RouterLinkStub)
      .find((one) => one.element.closest(".record-block__title"));

    expect(link?.props("to")).toEqual(to);
    expect(link?.text()).toBe("Жалоба на пользователя");
  });

  it("folds a long text, which its own page shows whole", () => {
    const listed = render(complaint(), { to }).getComponent({
      name: "TruncatedContent",
    });
    const whole = render(complaint()).getComponent({
      name: "TruncatedContent",
    });

    expect(listed.props("truncatable")).toBe(true);
    expect(whole.props("truncatable")).toBe(false);
  });
});

describe("the text of a ticket", () => {
  it("leads from the address of the violation to it", () => {
    const wrapper = render(
      complaint({
        description:
          "Оскорбляет участников.\n\nСсылка на нарушение: /games/example",
        descriptionHtml:
          "Оскорбляет участников.<br /><br />Ссылка на нарушение: /games/example",
      }),
    );
    const link = wrapper.get(".ticket-body a");

    expect(link.attributes("href")).toBe("/games/example");
    expect(link.text()).toBe("/games/example");
    expect(link.attributes("rel")).toBe("noopener noreferrer");
    expect(text(wrapper.get(".ticket-body"))).toBe(
      "Оскорбляет участников.Ссылка на нарушение: /games/example",
    );
  });

  // Read from the compiled styles: scoped SASS is not computed in a test
  // environment. The frame (RecordBlock) sets its text as a comment's, flush
  // on both sides and hyphenated by the reader's switch, and the block of a
  // ticket takes none of it back: no rule of its own sets the text.
  it("stands as the text of a comment, nothing of it set apart", () => {
    const rules = styleRules(componentCss(resolve(HERE, "TicketThread.vue")));

    expect(
      rules
        .filter((rule) => /ticket-body|record-block__text/.test(rule.selector))
        .map((rule) => rule.selector),
    ).toEqual([]);
    expect(
      rules.flatMap((rule) => Object.keys(rule.declarations)),
    ).not.toContain("text-align");
  });
});
