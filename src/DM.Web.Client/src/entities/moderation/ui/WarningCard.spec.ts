/**
 * @vitest-environment jsdom
 */

/**
 * One warning drawn once, for both readers.
 *
 * The owner (28.09) named what the card is made of, and every part of it is a
 * part of the site already. On top, the content the warning was issued for in
 * the dashed square of a comment, with DM2's caption of content under it:
 * "Название места, Автор: имя, дата". Under it, the decisions as the reviews of
 * a post: the warning, and the change of its points when there was one, each a
 * point of a list signed "<Что> от <кого>, <когда>". The points stand in the
 * kind, "Предупреждение (3 балла)" with the points it was issued with and
 * "Изменение баллов (1 балл)" with the points the change left, and nowhere on
 * the content (the owner, 05.10 and 12.10). A warning without points is
 * "Устное предупреждение", not "Комментарий". A kind that names points is
 * red, one that names none is grey: the warning by the points it was issued
 * with, the change by the points it left, so the colour never disagrees with
 * the number written beside it. Nothing on the card is green: green says a
 * thing is good, and a warning never is.
 *
 * The text arrives rendered, always (the owner, 05.10: "Всегда показывай"),
 * and it is the text the warning was issued for, with nothing said of a later
 * edit (the owner, 12.10: "Предупреждение же не к отредактированному"); the
 * address of it only where a guest could follow it. The card draws what it is
 * handed and decides nothing about that itself, so what is checked below is
 * that each shape of the row reads the way the owner's words say it should.
 */
import { describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { h } from "vue";
import { createPinia } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import {
  UserRole,
  type UserRef,
  type Username,
} from "@/shared/api/models/common";
import WarningCard from "./WarningCard.vue";

vi.mock("@/entities/user/@x/moderation", () => ({
  UserLink: {
    props: ["user"],
    template: "<span class='user-stub'>{{ user.username }}</span>",
  },
}));

const person = (username: string): UserRef => ({
  id: `u-${username}`,
  username: username as Username,
  lastActivityUtc: null,
  role: UserRole.RegularUser,
  isNewbie: false,
});

const EVIDENCE = "<strong>Грубость</strong> в адрес мастера";

const warning = (over: Record<string, unknown> = {}) => ({
  id: "w-1",
  user: person("Нарушитель"),
  moderator: person("Модератор"),
  points: 2,
  originalPoints: 2,
  reason: "Оскорбление",
  createdUtc: "2026-05-01T12:00:00",
  ...over,
});

/** A warning given for a comment in a topic a guest can read. */
const onContent = (over: Record<string, unknown> = {}) =>
  warning({
    entityType: "Comment",
    evidence: EVIDENCE,
    placeKind: "Topic",
    placeTitle: "Правила форума",
    entityUrl: "/forum/rules/12#comment-c-1",
    entityCreatedUtc: "2026-04-30T10:15:00",
    ...over,
  });

/** A warning whose points a second moderator has taken off. */
const corrected = (over: Record<string, unknown> = {}) =>
  warning({
    points: 0,
    originalPoints: 2,
    pointsChangedBy: "Старший",
    pointsChangedUtc: "2026-05-08T09:00:00",
    pointsChangeText: "Нарушения не было, модератор ошибся",
    ...over,
  });

function render(model: Record<string, unknown> = warning(), slots = {}) {
  return mount(WarningCard, {
    props: { warning: model as never },
    slots,
    global: {
      // The decision reads the reader's switch "Перенос слов" (RecordVerdict).
      plugins: [createPinia()],
      stubs: {
        RouterLink: {
          props: ["to"],
          // Two shapes of destination on this card: the address of the
          // offending object is a string and goes in `href`, a profile is a
          // named route and only `data-to` can carry it.
          template:
            "<a class='router-link' :href='typeof to === \"string\" ? to : undefined' :data-to='JSON.stringify(to)'><slot /></a>",
        },
      },
    },
  });
}

/** A caption line as it reads: runs of whitespace are one space. */
const line = (text: string) => text.replace(/\s+/g, " ").trim();

describe("the warning card, the content on top", () => {
  /**
   * The owner (28.09): "Где контент, к которому предупреждение, как на ДМ2?"
   * The text is rendered, as its place shows it, and not its source.
   */
  it("shows the offending text as its place renders it", () => {
    const wrapper = render(onContent());

    const text = wrapper.get(".warning-evidence");
    expect(text.find("strong").text()).toBe("Грубость");
    expect(text.classes()).toContain("bbcode-content");
  });

  // The points are the decision's, not the content's (the owner, 05.10: "У
  // самого контента их незачем писать").
  it("signs the text the way DM2 signed content: place, author, date, and no points", () => {
    const wrapper = render(onContent());

    expect(line(wrapper.get(".record-block__caption").text())).toBe(
      "Правила форума, Автор: Нарушитель, 30.04.2026 в 10:15",
    );
    expect(wrapper.get(".warning-place a").attributes("href")).toBe(
      "/forum/rules/12#comment-c-1",
    );
  });

  it("names the global chat by its kind", () => {
    const wrapper = render(
      onContent({
        entityType: "Message",
        placeKind: "GlobalChat",
        placeTitle: null,
        entityUrl: "/global-chat#msg-m-1",
      }),
    );

    expect(wrapper.get(".warning-place a").text()).toBe("Чат");
  });

  it("names a publication by its title, and by its kind without one", () => {
    expect(
      render(
        onContent({
          placeKind: "Publication",
          placeTitle: "Как я провел лето",
        }),
      )
        .get(".warning-place a")
        .text(),
    ).toBe("Как я провел лето");
    expect(
      render(onContent({ placeKind: "Publication", placeTitle: null }))
        .get(".warning-place a")
        .text(),
    ).toBe("Публикация");
  });

  /**
   * A text deleted after the warning, or standing where a guest cannot follow
   * it, is still the text the warning was given for (the owner, 05.10:
   * "Всегда показывай"); there is no address to open, so the place is named
   * and not linked.
   */
  it("names the place of a text without an address, and shows the text", () => {
    const wrapper = render(onContent({ entityUrl: null }));

    // The comma after the place is the line's, set after the part.
    expect(line(wrapper.get(".warning-place").text())).toBe("Правила форума");
    expect(wrapper.find(".warning-place a").exists()).toBe(false);
    expect(wrapper.find(".warning-evidence").exists()).toBe(true);
  });

  /**
   * The owner (12.10): "А зачем это вообще? Предупреждение же не к
   * отредактированному. Тут везде надо то, к чему предупреждение." The text
   * shown is the one the warning was issued for, and an edit made to it later
   * on the site is not the warning's business: the caption says nothing of it.
   * The server no longer sends the moment of an edit; the row below carries
   * one anyway, so that a field like it coming back stays off the card.
   */
  it("says nothing of an edit made to the text after the warning", () => {
    const wrapper = render(
      onContent({ entityEditedUtc: "2026-05-02T14:30:00" }),
    );

    expect(line(wrapper.get(".record-block__caption").text())).toBe(
      "Правила форума, Автор: Нарушитель, 30.04.2026 в 10:15",
    );
    expect(wrapper.text()).not.toContain("Отредактировано");
    expect(wrapper.text()).not.toContain("Изменено после предупреждения");
    expect(wrapper.find(".warning-edited").exists()).toBe(false);
    expect(wrapper.get(".warning-evidence strong").text()).toBe("Грубость");
  });

  // The line that stood where the log left a text out is gone, and so is the
  // reason moderation was told for it: the text is shown everywhere now.
  it("never says a text is for moderators alone", () => {
    const html = render(onContent({ entityUrl: null })).html();

    expect(html).not.toContain("Текст виден только модераторам");
    expect(html).not.toContain("Скрыт из лога");
  });

  /**
   * A warning issued on a profile is signed with DM2's "Профиль
   * пользователя", which leads to the profile. A profile the server sent no
   * copy of (a removed account) has no text line at all, not an empty one.
   */
  it("signs a warning on a profile with DM2's words and no text", () => {
    const wrapper = render(warning({ entityType: "Profile", profile: null }));

    expect(wrapper.find(".record-block__text").exists()).toBe(false);
    expect(line(wrapper.get(".record-block__caption").text())).toBe(
      "Профиль пользователя, Автор: Нарушитель",
    );
    expect(
      JSON.parse(wrapper.get(".warning-place a").attributes("data-to") ?? ""),
    ).toEqual({ name: "profile", params: { username: "Нарушитель" } });
  });

  it("says nothing of points on the content, changed or not", () => {
    for (const model of [
      warning(),
      corrected({ points: 1, originalPoints: 3 }),
    ]) {
      expect(
        line(render(model).get(".record-block__caption").text()),
      ).not.toMatch(/балл/);
    }
  });
});

/**
 * A warning on a profile shows the profile as it stood when the warning was
 * issued (the owner, 12.10, line 41: "почему профиль пользователя не видно?";
 * line 44: "Тут везде надо то, к чему предупреждение"): the avatar at the size
 * of the profile, in its own proportions and not rounded, "Статус:" and "О
 * себе:", in a column at every width, as the column of the profile has them.
 * The server took the copy at the moment of issue; the card draws what came
 * and leaves out what did not.
 */
describe("the warning card on a profile", () => {
  const PICTURE = {
    smallUrl: "/u/small.png",
    mediumUrl: "/u/medium.png",
    originalUrl: "/u/original.png",
    originalWidth: 731,
    originalHeight: 793,
  };

  const onProfile = (profile: Record<string, unknown> | null = {}) =>
    warning({
      entityType: "Profile",
      profile:
        profile === null
          ? null
          : {
              status: "Максимальная длина",
              info: "У меня <strong>самый длинный</strong> логин",
              picture: PICTURE,
              ...profile,
            },
    });

  it("shows the avatar of the moment at the size of the profile", () => {
    const picture = render(onProfile()).get("img.warning-profile__picture");

    expect(picture.attributes("src")).toBe("/u/original.png");
    expect(picture.attributes("width")).toBe("220");
    // The original keeps its proportions: a square crop may cut away what
    // the warning was given for.
    expect(picture.attributes("height")).toBe(
      String(Math.round((220 * 793) / 731)),
    );
  });

  it("writes the status and the words about the person as the profile did", () => {
    const wrapper = render(onProfile());

    expect(line(wrapper.get(".warning-profile__status").text())).toBe(
      "Статус: Максимальная длина",
    );
    expect(wrapper.get(".warning-profile__about-label").text()).toBe("О себе");
    const about = wrapper.get(".warning-profile__about");
    expect(about.classes()).toContain("bbcode-content");
    expect(about.get("strong").text()).toBe("самый длинный");
  });

  it("puts the profile over the caption of the record, in a column", () => {
    const block = render(onProfile()).get(".record-block");
    const all = [...block.element.querySelectorAll("*")];
    const order = [
      ".warning-profile__picture",
      ".warning-profile__status",
      ".warning-profile__about",
      ".record-block__caption",
    ].map((selector) => all.findIndex((element) => element.matches(selector)));

    expect(order.every((index) => index >= 0)).toBe(true);
    expect([...order].sort((a, b) => a - b)).toEqual(order);
    expect(line(block.get(".record-block__caption").text())).toBe(
      "Профиль пользователя, Автор: Нарушитель",
    );
  });

  /** No avatar then, no picture now: not the silhouette of a default avatar. */
  it("draws no picture where the profile had none", () => {
    const wrapper = render(onProfile({ picture: null }));

    expect(wrapper.find("img").exists()).toBe(false);
    expect(wrapper.find("svg").exists()).toBe(false);
    expect(wrapper.find(".warning-profile__status").exists()).toBe(true);
  });

  it("leaves out a part the profile did not have", () => {
    const noStatus = render(onProfile({ status: null }));
    expect(noStatus.find(".warning-profile__status").exists()).toBe(false);
    expect(noStatus.find(".warning-profile__about").exists()).toBe(true);

    const noAbout = render(onProfile({ info: null }));
    expect(noAbout.find(".warning-profile__about-label").exists()).toBe(false);
    expect(noAbout.find(".warning-profile__about").exists()).toBe(false);
  });

  /** A removed account: the server sends no copy, and nothing stands for it. */
  it("shows no profile when the server sent none", () => {
    const wrapper = render(onProfile(null));

    expect(wrapper.find(".warning-profile").exists()).toBe(false);
    expect(wrapper.find(".record-block__text").exists()).toBe(false);
  });

  /**
   * The caller adds its actions to the caption of the record as it adds them
   * to the caption of the decision: "Удалить картинку" stands after a bar.
   */
  it("carries the caller's action at the end of the caption of the record", () => {
    const withAction = render(onProfile(), {
      "record-controls":
        "<button class='control-stub'>Удалить картинку</button>",
    });

    expect(line(withAction.get(".record-block__caption").text())).toBe(
      "Профиль пользователя, Автор: Нарушитель | Удалить картинку",
    );
    expect(
      render(onProfile()).get(".record-block__caption").text(),
    ).not.toContain("|");
  });

  /** No picture of the site is rounded (the owner), this one included. */
  it("sets the picture square-cornered at the width of the profile's avatar", () => {
    const rule = ruleMap(
      componentCss(
        resolve(dirname(fileURLToPath(import.meta.url)), "WarningCard.vue"),
      ),
    ).get(".warning-profile__picture");

    expect(rule).toMatchObject({
      width: "220px",
      "max-width": "100%",
      height: "auto",
    });
    expect(rule?.["border-radius"]).toBeUndefined();
  });

  /**
   * The snapshot of a profile is read as the profile, so its parts are set in
   * the type of the profile page. That type is read off the profile page
   * itself: a copy that only names its original drifts the first time the
   * profile moves.
   */
  describe("in the type of the profile page", () => {
    const SRC = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
    const card = ruleMap(
      componentCss(resolve(SRC, "entities/moderation/ui/WarningCard.vue")),
    );
    const pick = (rule: Record<string, string> | undefined, keys: string[]) =>
      Object.fromEntries(keys.map((key) => [key, rule?.[key]]));

    it("writes the status as the status row of the profile", () => {
      const profile = ruleMap(
        componentCss(resolve(SRC, "pages/profile/ProfilePage.vue")),
      );
      const keys = ["font-size", "line-height", "color"];

      expect(pick(card.get(".warning-profile__status"), keys)).toEqual(
        pick(profile.get(".status-row"), keys),
      );
    });

    it("heads the words about the person as the profile heads them", () => {
      const tabs = ruleMap(
        componentCss(resolve(SRC, "shared/ui/Tabs/Tabs.vue")),
      );
      const heading = {
        ...tabs.get(".tabs--headings .tab"),
        ...tabs.get(".tabs--headings .tab.active"),
      };
      const keys = [
        "font-size",
        "font-weight",
        "text-transform",
        "letter-spacing",
        "color",
      ];

      expect(pick(card.get(".warning-profile__about-label"), keys)).toEqual(
        pick(heading, keys),
      );
    });
  });
});

/**
 * A warning issued on a ticket about a person names no text and no profile:
 * it may be about the private messages. "Профиль пользователя" over nothing is
 * what made the owner ask where the profile was, so it names the person.
 */
describe("the warning card that names nothing", () => {
  it.each([
    ["the open log", { entityType: null }],
    ["the moderation list", { entityType: "Unknown" }],
  ])("names the person in %s", (_, over) => {
    const wrapper = render(warning(over));

    expect(line(wrapper.get(".record-block__caption").text())).toBe(
      "Пользователь: Нарушитель",
    );
    expect(wrapper.text()).not.toContain("Профиль пользователя");
    expect(wrapper.find(".warning-profile").exists()).toBe(false);
  });
});

describe("the warning card, the decisions under it", () => {
  /**
   * The owner (28.09): "устное предупреждение, а не комментарий"; (05.10):
   * "Баллы пиши рядом с" the word "в скобочках, если это не устное
   * предупреждение".
   */
  it("calls a warning with points a warning, its points in brackets, in red", () => {
    const kind = render().get(".warning-verdict .record-verdict__kind");

    expect(kind.text()).toBe("Предупреждение (2 балла)");
    expect(kind.classes()).toContain("is-negative");
  });

  it.each([
    [1, "Предупреждение (1 балл)"],
    [5, "Предупреждение (5 баллов)"],
  ])("counts %i points in the brackets", (points, expected) => {
    expect(
      render(warning({ points, originalPoints: points }))
        .get(".warning-verdict .record-verdict__kind")
        .text(),
    ).toBe(expected);
  });

  /**
   * The owner (12.10): "Давай серое при изменении на 0 или устном. Если баллы
   * есть, то красное."
   */
  it("calls a warning without points a verbal warning, in grey", () => {
    const kind = render(warning({ points: 0, originalPoints: 0 })).get(
      ".warning-verdict .record-verdict__kind",
    );

    expect(kind.text()).toBe("Устное предупреждение");
    expect(kind.classes()).toContain("is-neutral");
  });

  /**
   * The warning is named by the points it was issued with, and the change
   * under it by the points it left: read the other way, 3 changed to 1 would
   * say "1 балл" twice and the 3 would be lost (the owner asked on 12.10 for
   * "Изменение баллов (x баллов)").
   */
  it("names the warning by the points it was issued with and the change by the points it left", () => {
    const wrapper = render(corrected({ originalPoints: 3, points: 1 }));

    expect(wrapper.get(".warning-verdict .record-verdict__kind").text()).toBe(
      "Предупреждение (3 балла)",
    );
    expect(wrapper.get(".warning-change .record-verdict__kind").text()).toBe(
      "Изменение баллов (1 балл)",
    );
  });

  /**
   * A warning of 2 points changed to 0: grey for the change, which left none,
   * and red for the warning, whose kind still says "(2 балла)". A kind is
   * coloured by the points it names.
   */
  it("keeps the points of a warning corrected to nothing in its kind, in red", () => {
    const kind = render(corrected()).get(
      ".warning-verdict .record-verdict__kind",
    );

    expect(kind.text()).toBe("Предупреждение (2 балла)");
    expect(kind.classes()).toContain("is-negative");
  });

  /**
   * "Если баллы есть, то красное": each kind by the points it names, the
   * warning by those it was issued with and the change by those it left. A
   * verbal warning stays grey over the points a change gave it, a warning of
   * points stays red over the change that took them off.
   */
  it.each([
    [2, 0, "is-negative", "is-neutral"],
    [3, 1, "is-negative", "is-negative"],
    [1, 3, "is-negative", "is-negative"],
    [0, 2, "is-neutral", "is-negative"],
  ])(
    "colours a warning of %i points changed to %i: the warning %s, the change %s",
    (originalPoints, points, warningTone, changeTone) => {
      const wrapper = render(corrected({ originalPoints, points }));

      expect(
        wrapper.get(".warning-verdict .record-verdict__kind").classes(),
      ).toContain(warningTone);
      expect(
        wrapper.get(".warning-change .record-verdict__kind").classes(),
      ).toContain(changeTone);
    },
  );

  /**
   * The reason is written in the BBCode editor of the warning window, and the
   * server sends it rendered: "[b]теме[/b]" stood with its brackets before.
   */
  it("shows the reason as it was formatted, in the colour of the text", () => {
    const wrapper = render(
      warning({ reason: "Оскорбление в <strong>теме</strong>" }),
    );
    const reason = wrapper.get(".warning-reason");

    expect(reason.text()).toBe("Оскорбление в теме");
    expect(reason.get("strong").text()).toBe("теме");
    expect(reason.classes()).toContain("bbcode-content");
    expect(
      wrapper.get(".warning-verdict .record-verdict__text").classes(),
    ).not.toContain("is-warning");
  });

  /**
   * A text of the site is set as the text of a comment (CommentItem): flush
   * on both sides at every width, and broken at syllables while the reader's
   * switch "Перенос слов" is on.
   */
  it("sets the words as the text of a comment, hyphenated by the switch", async () => {
    const pinia = createPinia();
    const wrapper = mount(WarningCard, {
      props: { warning: corrected() as never },
      global: { plugins: [pinia], stubs: { RouterLink: true } },
    });
    const ui = useUiStore(pinia);
    const texts = () => wrapper.findAll(".record-verdict__text");

    ui.setHyphenateWords(true);
    await wrapper.vm.$nextTick();
    expect(texts()).toHaveLength(2);
    for (const text of texts()) expect(text.classes()).toContain("hyphenated");

    ui.setHyphenateWords(false);
    await wrapper.vm.$nextTick();
    for (const text of texts())
      expect(text.classes()).not.toContain("hyphenated");

    const rules = ruleMap(
      componentCss(
        resolve(dirname(fileURLToPath(import.meta.url)), "RecordVerdict.vue"),
      ),
    );
    expect(rules.get(".record-verdict__text")?.["text-align"]).toBe("justify");
    expect(rules.get(".record-verdict__text.hyphenated")?.hyphens).toBe("auto");
  });

  it("signs the decision with its kind, the moderator and the date", () => {
    expect(
      line(render().get(".warning-verdict .record-verdict__caption").text()),
    ).toBe("Предупреждение (2 балла) от Модератор, 01.05.2026 в 12:00");
  });

  /**
   * The owner (28.09): "Почему изменения баллов оформлены как контент, а не
   * как предупреждение?" The change is a point of the same list, with the
   * points it left in its kind and the explanation as its words. The kind is a
   * noun, so "от" names who changed them: "Баллы ... от" read as if the points
   * had come from that moderator (the owner, 05.10).
   */
  it("draws the change of points as a decision under the warning", () => {
    const wrapper = render(corrected());
    const items = wrapper.findAll(".record-verdict");

    expect(items).toHaveLength(2);
    expect(items[1].classes()).toContain("warning-change");
    expect(items[1].get(".warning-change-text").text()).toBe(
      "Нарушения не было, модератор ошибся",
    );
    expect(line(items[1].get(".record-verdict__caption").text())).toBe(
      "Изменение баллов (0 баллов) от Старший, 08.05.2026 в 09:00",
    );
    expect(wrapper.find(".warning-change.record-block").exists()).toBe(false);
  });

  /**
   * The owner (12.10) on the change of a warning to nothing: "нахуя тут
   * зеленое? ... Давай серое при изменении на 0 или устном. Если баллы есть,
   * то красное." Which way the points went does not colour the change; what
   * it left does.
   */
  it("colours a change to nothing grey, and one that leaves points red", () => {
    const kindOf = (from: number, to: number) =>
      render(corrected({ originalPoints: from, points: to }))
        .get(".warning-change .record-verdict__kind")
        .classes();

    expect(kindOf(2, 0)).toContain("is-neutral");
    expect(kindOf(3, 1)).toContain("is-negative");
    expect(kindOf(1, 3)).toContain("is-negative");
    expect(kindOf(0, 2)).toContain("is-negative");
  });

  it("colours nothing on the card green, whatever its points", () => {
    for (const model of [
      warning(),
      warning({ points: 0, originalPoints: 0 }),
      corrected(),
      corrected({ originalPoints: 3, points: 1 }),
      corrected({ originalPoints: 0, points: 2 }),
    ]) {
      expect(render(model).find(".is-positive").exists()).toBe(false);
    }
  });

  it("draws no change until the points are changed", () => {
    expect(render().find(".warning-change").exists()).toBe(false);
  });

  it("leads to the profile of whoever changed the points", () => {
    const link = render(corrected()).get(".warning-change .router-link");

    expect(JSON.parse(link.attributes("data-to") ?? "null")).toEqual({
      name: "profile",
      params: { username: "Старший" },
    });
  });

  /**
   * The account of the moderator who changed the points can be gone; the
   * change is not. The kind and the moment stay, the missing name is simply
   * not there.
   */
  it("keeps the change when the name behind it is gone", () => {
    const caption = render(corrected({ pointsChangedBy: null })).get(
      ".warning-change .record-verdict__caption",
    );

    expect(line(caption.text())).toBe(
      "Изменение баллов (0 баллов), 08.05.2026 в 09:00",
    );
  });

  /**
   * The signature and the caller's control are one caption line: the control
   * follows the date after a bar, as the actions of a review do.
   */
  it("carries the caller's control at the end of the signature, and no bar when there is none", () => {
    const withControl = render(warning(), {
      controls: "<button class='control-stub'>Изменить баллы</button>",
    });
    const signature = withControl.get(
      ".warning-verdict .record-verdict__caption",
    );
    expect(line(signature.text())).toBe(
      "Предупреждение (2 балла) от Модератор, 01.05.2026 в 12:00 | Изменить баллы",
    );

    expect(
      render().get(".warning-verdict .record-verdict__caption").text(),
    ).not.toContain("|");
  });
});

/**
 * On a phone and in a narrow column both captions went down a second line:
 * the date under the author, the controls under the date (an audit of narrow
 * screens, 28.09.2026). From 5 October they stood in one line instead and put
 * their parts away under "Еще", the end of a name under "…". The owner (12
 * October 2026): on a phone every block is the block of the full version,
 * "НИКОГДА НЕ УКОРАЧИВАЙ". A caption wraps between its parts at every width
 * (CaptionLine), as the caption of a topic does: every part of it stands in
 * the line, the date with its time is one part, and the comma ends the part
 * before it, so a narrow record breaks after the comma and takes the date to
 * the next line whole. Where the line breaks is the browser's to decide; what
 * is read here is that nothing is put away for it to decide on.
 */
describe("the warning card wraps its captions", () => {
  const control = {
    controls: "<button class='control'>Изменить баллы</button>",
  };

  /** The parts of a caption line, in order. */
  const parts = (caption: ReturnType<ReturnType<typeof render>["get"]>) =>
    caption.findAll(".caption-line > .caption-line__part");

  // A caption stands in its block as text, so the block takes the caption's
  // size: on the size of the words above it the line would be their height.
  it("sets each caption block in the caption's size", () => {
    const here = dirname(fileURLToPath(import.meta.url));
    const size = (file: string, selector: string) =>
      ruleMap(componentCss(resolve(here, file))).get(selector)?.["font-size"];

    expect(size("RecordBlock.vue", ".record-block__caption")).toBe("12px");
    expect(size("RecordVerdict.vue", ".record-verdict__caption")).toBe("12px");
  });

  it("keeps each caption a line of its own, not a row that scrolls", () => {
    const wrapper = render(onContent(corrected()), control);
    const blocks = [
      wrapper.get(".record-block__caption"),
      ...wrapper.findAll(".record-verdict__caption"),
    ];

    expect(blocks).toHaveLength(3);
    for (const block of blocks) {
      expect(block.element.children).toHaveLength(1);
      expect(block.element.children[0].matches(".caption-line")).toBe(true);
    }
    expect(wrapper.find(".scroll-row").exists()).toBe(false);
  });

  it("draws every part of every caption, with no Еще and every name whole", () => {
    const wrapper = render(onContent(corrected()), control);

    expect(line(wrapper.get(".record-block__caption").text())).toBe(
      "Правила форума, Автор: Нарушитель, 30.04.2026 в 10:15",
    );
    expect(
      line(wrapper.get(".warning-verdict .record-verdict__caption").text()),
    ).toBe(
      "Предупреждение (2 балла) от Модератор, 01.05.2026 в 12:00 | Изменить баллы",
    );
    expect(
      line(wrapper.get(".warning-change .record-verdict__caption").text()),
    ).toBe("Изменение баллов (0 баллов) от Старший, 08.05.2026 в 09:00");
    expect(wrapper.find(".more-menu").exists()).toBe(false);
    expect(wrapper.find("[hidden]").exists()).toBe(false);
    expect(wrapper.text()).not.toMatch(/Еще|…/);
    const names = wrapper.findAll(".caption-line a");
    expect(names.length).toBeGreaterThan(0);
    for (const name of names) {
      expect(name.attributes("title")).toBeUndefined();
      expect(name.attributes("style")).toBeUndefined();
    }
    // Nothing is written for the queue of a line that put parts away: no
    // element of the card carries an attribute of the fitted row.
    const fitted = [wrapper.element, ...wrapper.element.querySelectorAll("*")]
      .flatMap((element) => element.getAttributeNames())
      .filter((attribute) => attribute.startsWith("data-fit"));
    expect(fitted).toEqual([]);
  });

  it("ends a part with its comma and keeps a date and its time one part", () => {
    const wrapper = render(onContent(), control);
    const [place, author, created] = parts(
      wrapper.get(".record-block__caption"),
    );
    const [signature, when, action] = parts(
      wrapper.get(".warning-verdict .record-verdict__caption"),
    );

    expect(place.element.lastElementChild?.textContent).toBe(",");
    expect(author.element.lastElementChild?.textContent).toBe(",");
    expect(line(created.text())).toBe(
      line(formatDateFull("2026-04-30T10:15:00")),
    );
    expect(created.find(".warning-date span").exists()).toBe(false);
    expect(signature.element.lastElementChild?.textContent).toBe(",");
    expect(line(when.text())).toBe(line(formatDateFull("2026-05-01T12:00:00")));
    expect(when.find(".record-verdict__date span").exists()).toBe(false);
    expect(action.find(".control").exists()).toBe(true);
  });

  it("has the control clicked where it stands in the line", async () => {
    const changed = vi.fn();
    const wrapper = render(warning(), {
      controls: () =>
        h("button", { class: "control", onClick: changed }, "Изменить баллы"),
    });
    const caption = wrapper.get(".warning-verdict .record-verdict__caption");

    await caption.get(".caption-line .control").trigger("click");
    expect(changed).toHaveBeenCalledTimes(1);
    expect(caption.find('[role="menu"]').exists()).toBe(false);
  });
});
