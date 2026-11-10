/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  type VueWrapper,
} from "@vue/test-utils";
import { Tooltip } from "@/shared/ui/Tooltip";
import CommentItem from "./CommentItem.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { submitDelete, submitLike, submitUnlike } from "./CommentItem.doubles";
import {
  LIKE_BUTTON,
  LINE,
  action,
  freshComment,
  mountComponent,
  plates,
  refused,
  signInAs,
} from "./CommentItem.fixtures";

// A question left open by one test would answer the next one's clicks.
enableAutoUnmount(afterEach);

beforeEach(freshComment);

const inQuestion = (selector: string) => {
  const found = document.body.querySelector<HTMLElement>(
    `.dialog-container ${selector}`,
  );
  if (!found) throw new Error(`${selector} is not in the question`);
  return found;
};

describe("CommentItem delete", () => {
  it("names the refusal in the question when the delete fails", async () => {
    submitDelete.mockResolvedValue({
      error: { type: "", title: "", status: 404, traceId: "t" },
    });
    const wrapper = await mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");
    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

    expect(submitDelete).toHaveBeenCalledWith("c-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    const dialog = wrapper.findComponent(ConfirmDialog);
    expect(dialog.props("show")).toBe(true);
    expect(inQuestion(".dialog-refusal").textContent?.trim()).toBe(
      "Не удалось удалить комментарий",
    );
    expect(plates()).toEqual([]);
    expect(wrapper.text()).not.toContain("Комментарий удален");
  });

  it("closes the question once the comment is gone", async () => {
    const wrapper = await mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");
    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(false);
  });

  it("asks before deleting", async () => {
    const wrapper = await mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");

    expect(submitDelete).not.toHaveBeenCalled();
    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(true);
  });

  it("deletes nothing when the question is answered no", async () => {
    const wrapper = await mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");
    inQuestion(".dialog-btn-cancel").click();
    await flushPromises();

    expect(submitDelete).not.toHaveBeenCalled();
    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(false);
  });
});

describe("CommentItem likes", () => {
  const liker = (username: string) => ({ id: `u-${username}`, username });

  it("shows the count and the reader's own like as the list brought them", async () => {
    signInAs("Reader");
    const wrapper = await mountComponent(vi.fn(), {
      likes: [liker("Reader"), liker("Other")] as never,
    });

    const like = wrapper.find(LIKE_BUTTON);
    expect(like.attributes("aria-pressed")).toBe("true");
    expect(like.text()).toBe("Нравится (2)");
  });

  it("shows someone else's likes without claiming the reader's", async () => {
    signInAs("Reader");
    const wrapper = await mountComponent(vi.fn(), {
      likes: [liker("Other")] as never,
    });

    const like = wrapper.find(LIKE_BUTTON);
    expect(like.attributes("aria-pressed")).toBe("false");
    expect(like.text()).toBe("Нравится (1)");
  });

  it("takes back a like that came with the list", async () => {
    signInAs("Reader");
    const wrapper = await mountComponent(vi.fn(), {
      likes: [liker("Reader")] as never,
    });

    await wrapper.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    expect(submitUnlike).toHaveBeenCalledWith("c-1");
    expect(submitLike).not.toHaveBeenCalled();
  });

  it("likes a comment the reader has not liked", async () => {
    signInAs("Reader");
    const wrapper = await mountComponent(vi.fn());

    await wrapper.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    expect(submitLike).toHaveBeenCalledWith("c-1");
    expect(submitUnlike).not.toHaveBeenCalled();
  });

  it("says what the server answered when it refuses the like", async () => {
    signInAs("Reader");
    submitLike.mockResolvedValue(refused(409, "Вы уже отметили Нравится"));
    const wrapper = await mountComponent(vi.fn());

    await wrapper.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).text()).toBe("Вы уже отметили Нравится");
    expect(plates()).toEqual([]);
    expect(wrapper.find(LIKE_BUTTON).attributes("aria-pressed")).toBe("false");
  });

  it("falls back to its own words for a mark and its removal", async () => {
    signInAs("Reader");
    submitLike.mockResolvedValue(refused(404));
    const liking = await mountComponent(vi.fn());
    await liking.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    submitUnlike.mockResolvedValue(refused(404));
    const unliking = await mountComponent(vi.fn(), {
      likes: [liker("Reader")] as never,
    });
    await unliking.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    expect(liking.find(LINE).text()).toBe("Не удалось отметить");
    expect(unliking.find(LINE).text()).toBe("Не удалось убрать отметку");
    expect(plates()).toEqual([]);
  });

  it("says a refusal the interceptor used to plate under the footer", async () => {
    signInAs("Reader");
    submitLike.mockResolvedValue(refused(403, "Вы не можете поставить лайк"));
    const wrapper = await mountComponent(vi.fn());

    await wrapper.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    expect(submitLike).toHaveBeenCalledTimes(1);
    expect(wrapper.find(LINE).text()).toBe("Вы не можете поставить лайк");
    expect(plates()).toEqual([]);
  });

  it("sends one request for two quick presses", async () => {
    signInAs("Reader");
    let answer: (value: unknown) => void = () => {};
    submitLike.mockReturnValue(new Promise((resolve) => (answer = resolve)));
    const wrapper = await mountComponent(vi.fn());

    await wrapper.find(LIKE_BUTTON).trigger("click");
    await wrapper.find(LIKE_BUTTON).trigger("click");
    answer({ error: null });
    await flushPromises();

    expect(submitLike).toHaveBeenCalledTimes(1);
    expect(plates()).toEqual([]);
  });
});

describe("CommentItem number", () => {
  const numberLink = (wrapper: VueWrapper) => {
    const found = wrapper.find("a.comment-number");
    if (!found.exists()) throw new Error("Номер комментария не ссылка");
    return found;
  };

  // The link keeps the reader's view: page 3 of a filtered list is not page 3 of the whole.
  it("leads to the comment's anchor in the view of the list the reader is on", async () => {
    const wrapper = await mountComponent(
      vi.fn(),
      {},
      { number: 12 },
      "/forum?number=3&search=кубы&authors=Experienced&sortOrder=desc",
    );

    expect(numberLink(wrapper).attributes("href")).toBe(
      `/forum?number=3&search=${encodeURIComponent("кубы")}&authors=Experienced&sortOrder=desc#comment-c-1`,
    );
  });

  it("carries no page number on a page that has none", async () => {
    const wrapper = await mountComponent(vi.fn(), {}, { number: 12 });

    expect(numberLink(wrapper).attributes("href")).toBe("/forum#comment-c-1");
  });

  it("stands last in the footer, outside its caption line, in both layouts", async () => {
    localStorage.clear();
    for (const compact of [true, false]) {
      const wrapper = await mountComponent(
        vi.fn(),
        {},
        { number: 12, compact },
      );

      const number = numberLink(wrapper).element;
      const footer = wrapper.get(".comment-footer").element;
      expect(number.parentElement).toBe(footer);
      expect(footer.lastElementChild).toBe(number);
      expect(number.closest(".caption-line")).toBeNull();
      expect(wrapper.find(".comment-header .comment-number").exists()).toBe(
        false,
      );
      expect(wrapper.find(".comment-caption").exists()).toBe(compact);
    }
  });

  // Only the real router marks links to the current page as current.
  it("is a link of the real router, not announced as the current page", async () => {
    const wrapper = await mountComponent(
      vi.fn(),
      {},
      { number: 12 },
      "/forum/newbies/3?number=2&authors=Experienced",
    );

    const link = wrapper.get("a.comment-number");
    expect(link.attributes("href")).toBe(
      "/forum/newbies/3?number=2&authors=Experienced#comment-c-1",
    );
    expect(link.attributes("aria-current")).toBeUndefined();
  });

  it("has no tooltip, and a name that says which comment it is", async () => {
    for (const compact of [true, false]) {
      const wrapper = await mountComponent(
        vi.fn(),
        {},
        { number: 12, compact },
      );

      const link = numberLink(wrapper);
      expect(
        wrapper
          .findAllComponents(Tooltip)
          .some((tip) => tip.find(".comment-number").exists()),
      ).toBe(false);
      expect(link.attributes("title")).toBeUndefined();
      expect(link.attributes("aria-label")).toBe("Комментарий 12");
      expect(link.text()).toBe("12");
    }
  });

  it("is read out with the name the list gives what it counts", async () => {
    const wrapper = await mountComponent(
      vi.fn(),
      {},
      { number: 3, noun: "Ответ" },
    );

    expect(numberLink(wrapper).attributes("aria-label")).toBe("Ответ 3");
  });
});

describe("CommentItem full layout", () => {
  it("puts the author's column beside the text and the number under both", async () => {
    localStorage.clear();
    const wrapper = await mountComponent(
      vi.fn(),
      {},
      { compact: false, number: 3 },
    );

    const columns = wrapper.get(".comment-columns").element;
    expect([...columns.children].map((child) => child.className)).toEqual([
      "comment-header",
      "comment-body",
    ]);
    const footer = wrapper.get(".comment-footer").element;
    expect(footer.parentElement).toBe(
      wrapper.findComponent(CommentItem).element,
    );
    expect(columns.compareDocumentPosition(footer)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });

  it("asks for the avatar at the width it is drawn", async () => {
    const wrapper = await mountComponent(
      vi.fn(),
      {
        author: {
          username: "SolohinLex",
          role: "RegularUser",
          lastActivityUtc: null,
          picture: { smallUrl: "/s.png", mediumUrl: "/m.png" },
        } as never,
      },
      { compact: false },
    );

    expect(wrapper.get("img.avatar").attributes("sizes")).toBe("128px");
  });

  it("keeps the compact layout without a column", async () => {
    const wrapper = await mountComponent(
      vi.fn(),
      {},
      { compact: true, number: 3 },
    );

    expect(wrapper.find(".comment-header").exists()).toBe(false);
    expect(wrapper.find(".avatar-link").exists()).toBe(false);
    expect(wrapper.find(".comment-edit-line").exists()).toBe(false);
    expect(wrapper.find(".comment-footer .comment-caption").exists()).toBe(
      true,
    );
  });
});
