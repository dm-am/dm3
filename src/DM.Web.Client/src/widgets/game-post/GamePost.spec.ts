/**
 * @vitest-environment jsdom
 */

/** The post as a whole: the order of its blocks and how much of a long text shows. */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import type { Post } from "@/entities/game";
import {
  clearRegistry,
  hasAny,
} from "@/shared/lib/composables/useExpandableRegistry";
import { createMockCharacter } from "@/entities/game/model/store.fixtures";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { UserRole } from "@/shared/api/models/common/user";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import {
  post,
  render,
  stubResizeObserver,
  type PostWrapper,
} from "./GamePost.fixtures";

// A post left mounted keeps its reviews in the registry of "Развернуть все".
enableAutoUnmount(afterEach);

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

stubResizeObserver();

/** The rolls stand between the game text and the metagame text, never folded. */
describe("GamePost dice rolls", () => {
  /** The line as a reader sees it: the joins that hold it together taken out. */
  const seen = (text: string) =>
    text
      .split(String.fromCharCode(0x2060))
      .join("")
      .replace(/\s+/g, " ")
      .trim();

  const rollOf = (value: number, comment?: string) => ({
    rolls: 1,
    edges: 20,
    bonus: 0,
    results: [{ value }],
    comment,
  });

  const withRolls = (count: number): Post => ({
    ...post,
    metagameText: "<p>Метаигровой текст</p>",
    diceRolls: Array.from({ length: count }, (_, i) =>
      rollOf(i + 1, `Бросок ${i + 1}`),
    ),
  });

  const rows = (wrapper: PostWrapper) =>
    wrapper.findAll(".dice-rolls > div").map((row) => seen(row.text()));

  beforeEach(() => {
    localStorage.clear();
    clearRegistry();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    clearRegistry();
  });

  it("writes every roll on a line of its own, between the game text and the metagame text", async () => {
    const wrapper = await render("/", withRolls(1));

    expect(rows(wrapper)).toEqual([
      "Результаты бросков:",
      '1) 1D20: 1 - "Бросок 1"',
    ]);
    const order = [...wrapper.get(".truncated-content").element.children].map(
      (block) => block.className.split(" ")[0],
    );
    expect(order).toEqual(["game-text", "dice-rolls", "metagame-text"]);
    wrapper.unmount();
  });

  it("leaves the page's Развернуть все nothing to open in the rolls", async () => {
    const wrapper = await render("/", {
      ...withRolls(8),
      reviewCount: 0,
    });

    // The reviews are the only fold of a post with nothing rated and no file.
    expect(hasAny.value).toBe(false);
    wrapper.unmount();
  });
});

/** The post in DM2's geometry. */
describe("GamePost geometry", () => {
  it("asks for the portrait at the width it is drawn", async () => {
    const wrapper = await render("/", {
      ...post,
      character: {
        ...createMockCharacter("ch-1", "Джек"),
        picture: asServed({ smallUrl: "/s.png", mediumUrl: "/m.png" }),
      },
    });

    expect(wrapper.get("img.avatar").attributes("sizes")).toBe("128px");
    wrapper.unmount();
  });
});

// In the game a person plays: the name stands without the badge of a site role.
describe("GamePost author", () => {
  it("names a staff author without the role, with or without a character", async () => {
    const staff: Post = {
      ...post,
      author: { ...createMockUser("u-author", "Author"), role: UserRole.Admin },
    };
    const bare = await render("/", staff);
    const played = await render("/", {
      ...staff,
      character: createMockCharacter("ch-1", "Джек"),
    });

    for (const wrapper of [bare, played]) {
      expect(wrapper.get(".meta-inner .user-link").text()).toBe("Author");
      expect(wrapper.find(".meta-inner .role-badge").exists()).toBe(false);
    }
  });
});

/** The lines beside the left column less the button's; jsdom answers the column 262 high. */
describe("GamePost text up to the button", () => {
  const realHeight = Object.getOwnPropertyDescriptor(
    HTMLElement.prototype,
    "offsetHeight",
  );

  beforeEach(() => {
    Object.defineProperty(HTMLElement.prototype, "offsetHeight", {
      configurable: true,
      get(this: HTMLElement) {
        return this.classList.contains("post-meta") ? 262 : 0;
      },
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    if (realHeight)
      Object.defineProperty(HTMLElement.prototype, "offsetHeight", realHeight);
  });

  async function budget(): Promise<number> {
    const wrapper = await render("/", post, { props: { truncatable: true } });
    await nextTick();
    return wrapper.getComponent(TruncatedContent).props("maxHeight") as number;
  }

  it("takes the lines beside the column: the column less the button, in whole lines", async () => {
    // 262 less 22 for the button is 240, twelve lines of 20.
    expect(await budget()).toBe(240);
  });
});
