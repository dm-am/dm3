/**
 * @vitest-environment jsdom
 */

// The card draws what it is handed; checked is that each shape of the row reads as it should.
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
          // An address goes in `href`, a named route of a profile only in `data-to`.
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
  // Rendered as its place shows it, not its source.
  it("shows the offending text as its place renders it", () => {
    const wrapper = render(onContent());

    const text = wrapper.get(".warning-evidence");
    expect(text.find("strong").text()).toBe("Грубость");
    expect(text.classes()).toContain("bbcode-content");
  });

  // The points are the decision's, not the content's.
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

  // A text with no address to open is named and not linked.
  it("names the place of a text without an address, and shows the text", () => {
    const wrapper = render(onContent({ entityUrl: null }));

    // The comma after the place is the line's, set after the part.
    expect(line(wrapper.get(".warning-place").text())).toBe("Правила форума");
    expect(wrapper.find(".warning-place a").exists()).toBe(false);
    expect(wrapper.find(".warning-evidence").exists()).toBe(true);
  });

  // A profile with no copy from the server has no text line at all.
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

// The profile as the server copied it at the moment of issue.
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
    // A square crop may cut away what the warning was given for.
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

  // The card copies the type of the profile page until they share a mixin; this holds the copy.
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

// It may be about the private messages, so it names the person.
// The open log by its reader: a place he does not read, or none found, is one block for all.
describe("the warning card of a place the reader does not read", () => {
  const restricted = () =>
    warning({ entityType: "Comment", accessRestricted: true });

  it("stands one block for the text, and names the author alone", () => {
    const wrapper = render(restricted());

    expect(wrapper.get(".warning-restricted").text()).toBe("Доступ ограничен");
    expect(wrapper.find(".warning-evidence").exists()).toBe(false);
    expect(line(wrapper.get(".record-block__caption").text())).toBe(
      "Автор: Нарушитель",
    );
    expect(wrapper.find(".warning-place").exists()).toBe(false);
  });
});

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
  it("calls a warning with points a warning, its points in brackets, in red", () => {
    const kind = render().get(".warning-verdict .record-verdict__kind");

    expect(kind.text()).toBe("Предупреждение (2 балла)");
    expect(kind.classes()).toContain("is-negative");
  });

  it("calls a warning without points a verbal warning, in grey", () => {
    const kind = render(warning({ points: 0, originalPoints: 0 })).get(
      ".warning-verdict .record-verdict__kind",
    );

    expect(kind.text()).toBe("Устное предупреждение");
    expect(kind.classes()).toContain("is-neutral");
  });

  // Read the other way, 3 changed to 1 would say "1 балл" twice and lose the 3.
  it("names the warning by the points it was issued with and the change by the points it left", () => {
    const wrapper = render(corrected({ originalPoints: 3, points: 1 }));

    expect(wrapper.get(".warning-verdict .record-verdict__kind").text()).toBe(
      "Предупреждение (3 балла)",
    );
    expect(wrapper.get(".warning-change .record-verdict__kind").text()).toBe(
      "Изменение баллов (1 балл)",
    );
  });

  // A kind is coloured by the points it names.
  it("keeps the points of a warning corrected to nothing in its kind, in red", () => {
    const kind = render(corrected()).get(
      ".warning-verdict .record-verdict__kind",
    );

    expect(kind.text()).toBe("Предупреждение (2 балла)");
    expect(kind.classes()).toContain("is-negative");
  });

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

  it("hyphenates the words of the decisions by the switch", async () => {
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
  });

  it("signs the decision with its kind, the moderator and the date", () => {
    expect(
      line(render().get(".warning-verdict .record-verdict__caption").text()),
    ).toBe("Предупреждение (2 балла) от Модератор, 01.05.2026 в 12:00");
  });

  // A noun, so "от" names who changed the points.
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

  it("colours nothing on the card green, whatever its points", () => {
    for (const model of [
      warning(),
      warning({ points: 0, originalPoints: 0 }),
      corrected(),
      corrected({ originalPoints: 3, points: 1 }),
      corrected({ originalPoints: 0, points: 2 }),
    ]) {
      const card = render(model);
      expect(card.find(".is-positive").exists()).toBe(false);
      // Nor are the words of the decisions set as a [mod] block: one is green.
      expect(card.find(".record-verdict-list .mod-block").exists()).toBe(false);
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

  it("keeps the change when the name behind it is gone", () => {
    const caption = render(corrected({ pointsChangedBy: null })).get(
      ".warning-change .record-verdict__caption",
    );

    expect(line(caption.text())).toBe(
      "Изменение баллов (0 баллов), 08.05.2026 в 09:00",
    );
  });

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

// A caption wraps between its parts at every width; nothing is put away for want of room.
describe("the warning card wraps its captions", () => {
  const control = {
    controls: "<button class='control'>Изменить баллы</button>",
  };

  /** The parts of a caption line, in order. */
  const parts = (caption: ReturnType<ReturnType<typeof render>["get"]>) =>
    caption.findAll(".caption-line > .caption-line__part");

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
