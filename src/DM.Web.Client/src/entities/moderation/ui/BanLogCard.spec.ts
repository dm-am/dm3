/**
 * @vitest-environment jsdom
 */

/**
 * One entry of the ban list, written as DM2's bot wrote its ban log (the topic
 * "Бан-лог" of DM2), so a reader of that log reads this one the same way.
 *
 * The bot wrote every issue and every lift as a comment of one pattern, the
 * same in 1886 entries: the moderator, "выдал(а)" or "снял(а)", the scope of
 * the ban, "пользователю" or "с пользователя", the login, "до" and the end date
 * in bold, a full stop. The end of a lifted ban is the end it had, not the day
 * of the lift. A permanent ban is said in a word here and not by a date five
 * hundred years away, so it has no "до". Nothing in the entry is red or green:
 * a ban is the record of a decision, and DM2 coloured nothing but the logins.
 *
 * Where the entry says more than DM2's did, it says it for the reader: the
 * moderator a bold link, as the login, the first person a reader of the log
 * looks up; nothing underlined, the entry being one run of text; the reason
 * under "Примечание от модератора:", on a line of its own, as the [quote] of
 * the site's text, so the moderator's words stand apart from the site's
 * sentence; "Выдан:" or "Снят:" before the moment in the caption, so the moment
 * says what it is the moment of; the number of the entry a grey link in the
 * corner of the caption line, as the number of a comment.
 *
 * The lift stands where the server says this reader may lift this ban: the
 * card does not work the rule out from a role, so it can never offer a lift the
 * server then refuses.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import BanLogCard from "./BanLogCard.vue";

const NBSP = "\u00a0";

const person = (username: string) => ({
  id: `u-${username}`,
  username,
  lastActivityUtc: null,
  role: "RegularUser",
  isNewbie: false,
});

/** The running democratic ban of the seed: a week, its reason in a field. */
const ban = (over: Record<string, unknown> = {}) => ({
  id: "b-1",
  user: person("Experienced"),
  moderator: person("TestSeniorMod"),
  type: "Temporary",
  accessPolicy: "DemocraticBan",
  startedUtc: "2026-10-12T12:00:00Z",
  expiresUtc: "2026-10-19T12:00:00Z",
  comment: "Первый по сумме баллов (с учетом сгорания банов).",
  isActive: true,
  canLift: false,
  ...over,
});

const issued = (over: Record<string, unknown> = {}) => ({
  number: 16,
  kind: "Issued",
  occurredUtc: "2026-10-12T12:00:00Z",
  ban: ban(over),
});

/** The lift of a ban, at the moment of the lift; the ban keeps its own end. */
const lifted = (over: Record<string, unknown> = {}) => ({
  number: 17,
  kind: "Lifted",
  occurredUtc: "2026-10-13T11:12:00Z",
  ban: ban({
    user: person("RatingPositive"),
    expiresUtc: "2026-10-16T12:00:00Z",
    isActive: false,
    liftedUtc: "2026-10-13T11:12:00Z",
    liftedBy: person("SolohinLex"),
    liftReason: "Извинился, договорились",
    ...over,
  }),
});

/** Where the list sends the number of an entry. */
const NUMBER_TO = {
  name: "moderation-bans",
  query: { entry: "16" },
  hash: "#ban-16",
};

function render(
  entry: Record<string, unknown>,
  numberTo: unknown = NUMBER_TO,
  attrs: Record<string, unknown> = {},
) {
  return mount(BanLogCard, {
    props: { entry: entry as never, numberTo: numberTo as never },
    attrs,
    global: {
      stubs: {
        RouterLink: {
          props: ["to"],
          template:
            "<a class='router-link' :data-to='JSON.stringify(to)'><slot /></a>",
        },
      },
    },
  });
}

/** The sentence of the entry, as its text reads. */
const phrase = (wrapper: ReturnType<typeof render>) =>
  wrapper.get(".ban-log-card__phrase").element.textContent ?? "";

/** The note of the entry: its heading and the quote under it. */
const note = (wrapper: ReturnType<typeof render>) => ({
  label: wrapper.find(".ban-log-card__note-label").text(),
  quote: wrapper.find(".ban-log-card__note .quote").text(),
});

const linkTo = (link: { attributes: (name: string) => string | undefined }) =>
  JSON.parse(link.attributes("data-to") ?? "null");

describe("an entry of the ban list, in the words of DM2", () => {
  it("writes an issue as the bot of DM2 did", () => {
    expect(phrase(render(issued()))).toBe(
      `TestSeniorMod выдал(а) демократический бан пользователю Experienced до${NBSP}19.10.2026.`,
    );
  });

  /**
   * DM2: "SolohinLex снял(а) демократический бан c пользователя Рамиус до
   * 20.02.2026" on the day it was lifted, 13.02: the end is the end the ban
   * had. The Latin "c" DM2 wrote in 35 lifts is not copied.
   */
  it("writes a lift with the end the ban had", () => {
    expect(phrase(render(lifted()))).toBe(
      `SolohinLex снял(а) демократический бан с пользователя RatingPositive до${NBSP}16.10.2026.`,
    );
  });

  /** The entry is one run of text: no word in it is set apart by a line. */
  it("underlines nothing, in a lift as in an issue", () => {
    expect(render(lifted()).find("u").exists()).toBe(false);
    expect(render(issued()).find("u").exists()).toBe(false);
  });

  it("says a permanent ban in a word, with no end", () => {
    const wrapper = render(
      issued({
        moderator: person("SolohinLex"),
        user: person("LongestLoginPossible"),
        type: "Permanent",
        accessPolicy: "FullBan",
        expiresUtc: "2126-10-09T12:00:00Z",
        comment: "Рекламный бот",
      }),
    );

    expect(phrase(wrapper)).toBe(
      "SolohinLex выдал(а) бессрочный полный бан пользователю LongestLoginPossible.",
    );
    expect(phrase(wrapper)).not.toContain(" до");
  });

  it("signs the ban of the warning points by the account that issued it", () => {
    const wrapper = render(
      issued({
        moderator: person("Робот-Администратор"),
        user: person("Тест Елки"),
        expiresUtc: "2026-10-14T12:00:00Z",
      }),
    );

    expect(phrase(wrapper)).toBe(
      `Робот-Администратор выдал(а) демократический бан пользователю Тест Елки до${NBSP}14.10.2026.`,
    );
  });

  it("sets the moderator, the login and the end in bold", () => {
    const bold = render(issued())
      .findAll(".ban-log-card__phrase b")
      .map((part) => part.text());

    expect(bold).toEqual(["TestSeniorMod", "Experienced", "19.10.2026"]);
  });

  /**
   * The one who issued the ban, or lifted it, leads to his profile as the
   * login does: whose decision it was is the first thing a reader looks up.
   */
  it("links the moderator and the login to their profiles", () => {
    const issue = render(issued()).findAll(".ban-log-card__phrase a");
    expect(issue.map(linkTo)).toEqual([
      { name: "profile", params: { username: "TestSeniorMod" } },
      { name: "profile", params: { username: "Experienced" } },
    ]);

    const lift = render(lifted()).findAll(".ban-log-card__phrase a");
    expect(lift.map(linkTo)).toEqual([
      { name: "profile", params: { username: "SolohinLex" } },
      { name: "profile", params: { username: "RatingPositive" } },
    ]);
  });

  /**
   * An account that is gone signs nothing by name: the name every removed
   * account writes with, a link to the profile that gathers them, the
   * moderator who issued or lifted the ban as the banned login.
   */
  it("names a removed account the way the site names it everywhere", () => {
    const removed = { name: "profile", params: { username: "Аккаунт удален" } };
    const issue = render(issued({ moderator: null, user: null }));

    expect(phrase(issue)).toBe(
      `Аккаунт удален выдал(а) демократический бан пользователю Аккаунт удален до${NBSP}19.10.2026.`,
    );
    expect(issue.findAll(".ban-log-card__phrase a").map(linkTo)).toEqual([
      removed,
      removed,
    ]);

    const lift = render(lifted({ liftedBy: null }));
    expect(phrase(lift).startsWith("Аккаунт удален снял(а)")).toBe(true);
    expect(linkTo(lift.findAll(".ban-log-card__phrase a")[0])).toEqual(removed);
  });
});

describe("the note of a ban entry", () => {
  /**
   * The reason stands under "Примечание от модератора:", DM2's "Комментарий:"
   * named for what it is, and the note itself is a BBCode quote on the line
   * below, so the moderator's words stand apart from the site's sentence. The
   * quote is the [quote] of the site's text, drawn by the rules of the
   * rendered text (bbcode-content).
   */
  it("puts the reason under its heading, as a quote of the site's text", () => {
    const wrapper = render(issued());

    expect(note(wrapper)).toEqual({
      label: "Примечание от модератора:",
      quote: "Первый по сумме баллов (с учетом сгорания банов).",
    });
    expect(wrapper.get(".ban-log-card__note").classes()).toContain(
      "bbcode-content",
    );
    expect(wrapper.text()).not.toContain("Комментарий");
  });

  it("puts a blank line between the sentence and the heading of the note", () => {
    const record = render(issued()).get(".record-block__text").element;
    const children = [...record.children].map((child) =>
      child.className ? `${child.tagName}.${child.className}` : child.tagName,
    );

    expect(children.slice(0, 5)).toEqual([
      "SPAN.ban-log-card__phrase",
      "BR",
      "BR",
      "SPAN.ban-log-card__note-label",
      "DIV.ban-log-card__note bbcode-content",
    ]);
  });

  /**
   * The reason of a ban is written in the BBCode editor, and the server sends
   * it rendered: "[b]спам[/b]" came out with its brackets before.
   */
  it("shows a reason the server rendered as it was formatted", () => {
    const wrapper = render(issued({ comment: "<strong>спам</strong> в чате" }));
    const quote = wrapper.get(".ban-log-card__note .quote");

    expect(quote.get("strong").text()).toBe("спам");
    expect(quote.text()).not.toContain("[b]");
  });

  /** The reason of a lift is a plain field, and its text is text. */
  it("shows the reason of a lift as it was typed", () => {
    const wrapper = render(lifted({ liftReason: "<b>не</b> спам" }));

    expect(note(wrapper)).toEqual({
      label: "Примечание от модератора:",
      quote: "<b>не</b> спам",
    });
    expect(wrapper.find(".ban-log-card__note b").exists()).toBe(false);
  });

  /**
   * DM2 kept the heading of an entry without a reason, "Комментарий:" with
   * nothing after it (four such entries on the eleventh page of its log
   * alone). The entry keeps its heading the same way, with no quote under it:
   * an empty quote would say there is something to read.
   */
  it("keeps the heading of the note over nothing for an entry without a reason", () => {
    for (const wrapper of [
      render(issued({ comment: "" })),
      render(lifted({ liftReason: null })),
      render(lifted({ liftReason: "  " })),
    ]) {
      const record = wrapper.get(".record-block__text").element;
      const children = [...record.children].map((child) =>
        child.className ? `${child.tagName}.${child.className}` : child.tagName,
      );

      expect(children).toEqual([
        "SPAN.ban-log-card__phrase",
        "BR",
        "BR",
        "SPAN.ban-log-card__note-label",
        "DIV.ban-log-card__footer",
      ]);
      expect(wrapper.get(".ban-log-card__note-label").text()).toBe(
        "Примечание от модератора:",
      );
      expect(wrapper.find(".quote").exists()).toBe(false);
    }
  });
});

describe("the caption of a ban entry", () => {
  /** The line as it reads, its spaces of any width as one. */
  const plain = (text: string) => text.replace(/\s+/g, " ").trim();
  const caption = (wrapper: ReturnType<typeof render>) =>
    plain(wrapper.get(".ban-log-card__caption").text());

  /** "Выдан:" or "Снят:" says what the moment under the entry is the moment of. */
  it("says what happened before the moment it happened", () => {
    expect(caption(render(issued()))).toBe(
      plain(`Выдан: ${formatDateFull("2026-10-12T12:00:00Z")}`),
    );
    expect(caption(render(lifted()))).toBe(
      plain(`Снят: ${formatDateFull("2026-10-13T11:12:00Z")}`),
    );
  });

  it("offers the lift where the server says the reader may lift the ban", async () => {
    const wrapper = render(issued({ canLift: true }));

    expect(caption(wrapper)).toBe(
      plain(`Выдан: ${formatDateFull("2026-10-12T12:00:00Z")} | Снять бан`),
    );
    await wrapper.get(".caption-action").trigger("click");
    expect(wrapper.emitted("lift")).toEqual([[]]);
  });

  /**
   * A senior moderator may not lift a ban of his own nor a permanent one, and
   * the server says so in the flag: the card reads the flag and not a role.
   */
  it("offers no lift where the server says the reader may not", () => {
    const wrapper = render(issued({ canLift: false }));

    expect(wrapper.find(".caption-action").exists()).toBe(false);
    expect(caption(wrapper)).not.toContain("|");
  });

  it("offers no lift on the entry of a lift", () => {
    expect(
      render(lifted({ canLift: true }))
        .find(".caption-action")
        .exists(),
    ).toBe(false);
  });
});

describe("the number of a ban entry", () => {
  const number = (wrapper: ReturnType<typeof render>) =>
    wrapper.get(".ban-log-card__number");

  /**
   * The number is the address of the entry, in the right corner as the number
   * of a comment is: it stands in the footer after the caption line, out of
   * it, the last thing in the record.
   */
  it("is a link to the entry in the corner of the caption line", () => {
    const wrapper = render(issued());
    const link = number(wrapper);
    const footer = wrapper.get(".ban-log-card__footer").element;

    expect(link.text()).toBe("16");
    expect(linkTo(link)).toEqual(NUMBER_TO);
    expect(link.element.parentElement).toBe(footer);
    expect(footer.lastElementChild).toBe(link.element);
    expect(link.element.closest(".caption-line")).toBeNull();
    expect(link.attributes("aria-label")).toBe("Запись 16");
  });

  it("is drawn only where the list says where it leads", () => {
    expect(render(issued(), null).find(".ban-log-card__number").exists()).toBe(
      false,
    );
  });

  /**
   * The anchor the list gives the entry is the record's, the element that
   * carries the ground: the mark of the entry a link arrived at is drawn on
   * the ground.
   */
  it("hands the anchor of the entry to the record", () => {
    const wrapper = render(issued(), NUMBER_TO, { id: "ban-16" });

    expect(wrapper.get(".record-block").attributes("id")).toBe("ban-16");
    expect(wrapper.get("article").attributes("id")).toBeUndefined();
  });
});

describe("the look of a ban entry", () => {
  const css = componentCss(
    resolve(dirname(fileURLToPath(import.meta.url)), "BanLogCard.vue"),
  );
  const rules = ruleMap(css);

  /**
   * DM2 coloured nothing in a ban but the logins, and red and green here say
   * points and what is good, neither of which a ban entry is.
   */
  it("is neither red nor green", () => {
    const wrapper = render(issued({ canLift: true }));

    expect(css).not.toMatch(/accent-red|accent-green/);
    expect(wrapper.find(".is-negative, .is-positive").exists()).toBe(false);
  });

  /**
   * The text of the entry is set as the text of a comment (RecordBlock):
   * justified, its words broken by "Перенос слов". Only the caption line is
   * set flush left, as every caption is.
   */
  it("leaves the text to the record and sets only the caption flush left", () => {
    const aligned = [...rules]
      .filter(([, declarations]) => "text-align" in declarations)
      .map(([selector]) => selector);

    const unhyphenated = [...rules]
      .filter(([, declarations]) => "hyphens" in declarations)
      .map(([selector]) => selector);

    expect(aligned).toEqual([".ban-log-card__footer"]);
    expect(unhyphenated).toEqual([".ban-log-card__footer"]);
  });

  /** The number of a comment (CommentItem, .comment-number). */
  it("pins the number to the right edge as a grey link, not underlined", () => {
    const declarations = rules.get(".ban-log-card__number");

    expect(declarations).toMatchObject({
      "margin-left": "auto",
      color: "var(--text-muted)",
    });
    expect(declarations?.["text-decoration"]).toBe("none");
  });

  /**
   * The reason of a lift is typed in a field of several lines, and the lines
   * the moderator broke it into stay broken; the rendered reason of a ban
   * carries its own breaks.
   */
  it("keeps the lines of the reason of a lift", () => {
    expect(rules.get(".ban-log-card__lift-reason")?.["white-space"]).toBe(
      "pre-line",
    );
    expect(
      render(lifted({ liftReason: "Извинился.\nДоговорились." }))
        .get(".ban-log-card__note .quote")
        .classes(),
    ).toContain("ban-log-card__lift-reason");
    expect(
      render(issued()).get(".ban-log-card__note .quote").classes(),
    ).not.toContain("ban-log-card__lift-reason");
  });
});
