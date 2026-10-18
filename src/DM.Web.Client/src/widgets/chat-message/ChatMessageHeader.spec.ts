/**
 * @vitest-environment jsdom
 */

/**
 * The header of a chat message says the whole name and the time, and on a
 * phone it is the header of the full version.
 *
 * Before 5 October 2026 the header scrolled sideways on a phone, as one run.
 * Then it was held to one line, where "(скрыть)" went into "Еще" and a long
 * name gave up its end under "…". The owner, 12 October 2026: nothing is
 * shortened on a phone, and nothing scrolls sideways. The header is a line of
 * text again, as it was: where it has no room, it wraps between the name and
 * the time, and a name longer than the line breaks as any word of the site
 * does. "(скрыть)" over a removed message a moderator opened is a part of the
 * line.
 *
 * And the pictures of the full version: the avatar is 72 at any width, where a
 * narrow feed drew it 48.
 */
import { describe, it, expect, afterEach } from "vitest";
import { mount, RouterLinkStub, type VueWrapper } from "@vue/test-utils";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import ChatMessage from "./ChatMessage.vue";
import type { MessageWithContinuation } from "@/shared/lib/utils/chat";

const SOURCE = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "ChatMessage.vue",
);

const LONG = "Широкоплечийбогатырь";

const message = (over: Record<string, unknown> = {}) =>
  ({
    id: "m1",
    createdUtc: "2026-10-05T07:38:00Z",
    author: { username: LONG },
    text: "<p>Привет</p>",
    isContinuation: false,
    isRemoved: false,
    edits: [],
    likes: [],
    ...over,
  }) as unknown as MessageWithContinuation;

const mounted: VueWrapper[] = [];

function open(props: Record<string, unknown>) {
  const wrapper = mount(ChatMessage, {
    props: { message: message(), ...props },
    attachTo: document.body,
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  mounted.push(wrapper);
  return wrapper;
}

afterEach(() => {
  while (mounted.length) mounted.pop()?.unmount();
  document.body.innerHTML = "";
});

const header = (wrapper: VueWrapper) => wrapper.get(".msg-header");
const name = (wrapper: VueWrapper) =>
  header(wrapper).get(".msg-author").element as HTMLElement;

/** What a reader copies of the line: the parts that stand, as text. */
function copied(element: Element): string {
  let text = "";
  const walk = (node: Node) => {
    if (node instanceof HTMLElement && node.hidden) return;
    if (node.nodeType === Node.TEXT_NODE) text += node.textContent ?? "";
    node.childNodes.forEach(walk);
  };
  walk(element);
  return text.replace(/\s+/g, " ").trim();
}

/** Nothing of the line is put away, nor held to one line by a measure. */
function putAway(wrapper: VueWrapper) {
  const line = header(wrapper);
  return {
    measured: line.attributes("data-fit-row") !== undefined,
    hidden: line.findAll("[hidden]").length,
    more: line.text().includes("Еще") || line.text().includes("…"),
    scrolls: wrapper.find(".scroll-row").exists(),
  };
}

const NOTHING_PUT_AWAY = {
  measured: false,
  hidden: 0,
  more: false,
  scrolls: false,
};

/** The declarations the component gives one selector, merged. */
function declared(selector: string): Record<string, string> {
  return Object.assign(
    {},
    ...styleRules(componentCss(SOURCE))
      .filter((rule) =>
        rule.selector.split(",").some((one) => one.trim() === selector),
      )
      .map((rule) => rule.declarations),
  ) as Record<string, string>;
}

describe("the header of a chat message", () => {
  it("is a line of text, with nothing put away and nothing that scrolls", () => {
    const wrapper = open({});

    expect(putAway(wrapper)).toEqual(NOTHING_PUT_AWAY);
    // The line wraps where it has no room: no rule holds it to one line.
    expect(declared(".msg-header")["white-space"]).toBeUndefined();
    expect(readFileSync(SOURCE, "utf8")).not.toMatch(
      /MessageHeader|FitRow|MoreMenu|data-fit-/,
    );
  });

  it("says the whole name, then the time", () => {
    const wrapper = open({});

    expect(name(wrapper).textContent).toBe(LONG);
    expect(name(wrapper).getAttribute("style")).toBeNull();
    expect(name(wrapper).title).toBe("");
    expect(copied(header(wrapper).element)).toMatch(
      new RegExp(`^${LONG} \\d\\d:\\d\\d$`),
    );
  });

  it("does the same in the compact layout, time first", () => {
    const wrapper = open({ compact: true });

    expect(putAway(wrapper)).toEqual(NOTHING_PUT_AWAY);
    expect(name(wrapper).getAttribute("style")).toBeNull();
    expect(copied(header(wrapper).element)).toMatch(
      new RegExp(`^\\d\\d:\\d\\d ${LONG}$`),
    );
  });

  it("keeps no room after the time of a message nobody edited", () => {
    const wrapper = open({});

    expect(header(wrapper).find(".msg-icon-placeholder").exists()).toBe(false);
  });

  it("still shows the pencil after the time of an edited one", () => {
    const wrapper = open({
      message: message({ edits: [{ modifiedUtc: "2026-10-05T07:40:00Z" }] }),
    });

    expect(header(wrapper).find(".msg-edited-icon").exists()).toBe(true);
  });
});

describe("the header of a removed message a moderator opened", () => {
  const removed = (compact: boolean) =>
    open({
      message: message({ isRemoved: true }),
      isModerator: true,
      isDeletedExpanded: true,
      compact,
    });

  it('has "(скрыть)" as a part of the line, and no "Еще"', () => {
    const wrapper = removed(false);

    expect(putAway(wrapper)).toEqual(NOTHING_PUT_AWAY);
    expect(copied(header(wrapper).element)).toMatch(
      new RegExp(`^${LONG} \\d\\d:\\d\\d \\(скрыть\\)$`),
    );
  });

  it("has it in the compact layout too, after the name", () => {
    const wrapper = removed(true);

    expect(putAway(wrapper)).toEqual(NOTHING_PUT_AWAY);
    expect(copied(header(wrapper).element)).toMatch(
      new RegExp(`^\\d\\d:\\d\\d ${LONG} \\(скрыть\\)$`),
    );
  });
});

/**
 * A feed narrower than 480 once drew the avatar 48 instead of 72, so a phone
 * showed a smaller picture than the full version. The avatar and the gutter of
 * a continuation, which keeps the avatar's width, are 72 at any width.
 */
describe("the picture of a message", () => {
  it("has no narrow form to be told to take", () => {
    const wrapper = open({ narrow: true });

    expect(wrapper.get(".chat-message").classes()).not.toContain("narrow");
    expect(wrapper.props()).not.toHaveProperty("narrow");
  });

  it("is 72 wide, and so is the gutter of a continuation", () => {
    const narrowRules = styleRules(componentCss(SOURCE)).filter((rule) =>
      rule.selector.includes(".narrow"),
    );
    expect(narrowRules).toEqual([]);
    for (const box of [".msg-avatar", ".msg-avatar-placeholder"]) {
      expect(declared(box).width, box).toBe("72px");
      expect(declared(box).height, box).toBe("72px");
    }
    expect(declared(".msg-time-gutter").width).toBe("72px");
  });
});
