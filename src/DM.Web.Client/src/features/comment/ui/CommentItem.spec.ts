/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import {
  enableAutoUnmount,
  mount,
  flushPromises,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia } from "pinia";
import { Tooltip } from "@/shared/ui/Tooltip";
import CommentItem from "./CommentItem.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import {
  route,
  submitDelete,
  submitEdit,
  submitLike,
  submitUnlike,
} from "./CommentItem.doubles";
import {
  LIKE_BUTTON,
  LINE,
  RouterLinkStub,
  action,
  comment,
  freshComment,
  mountComponent,
  plates,
  refused,
  signInAs,
} from "./CommentItem.fixtures";

vi.mock("vue-router", async () =>
  (await import("./CommentItem.doubles")).vueRouter(),
);

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
    const wrapper = mountComponent(vi.fn());

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
    const wrapper = mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");
    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(false);
  });

  it("asks before deleting", async () => {
    const wrapper = mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");

    expect(submitDelete).not.toHaveBeenCalled();
    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(true);
  });

  it("deletes nothing when the question is answered no", async () => {
    const wrapper = mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");
    inQuestion(".dialog-btn-cancel").click();
    await flushPromises();

    expect(submitDelete).not.toHaveBeenCalled();
    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(false);
  });
});

describe("CommentItem likes", () => {
  const liker = (username: string) => ({ id: `u-${username}`, username });

  it("shows the count and the reader's own like as the list brought them", () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn(), {
      likes: [liker("Reader"), liker("Other")] as never,
    });

    const like = wrapper.find(LIKE_BUTTON);
    expect(like.attributes("aria-pressed")).toBe("true");
    expect(like.text()).toBe("Нравится (2)");
  });

  it("shows someone else's likes without claiming the reader's", () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn(), {
      likes: [liker("Other")] as never,
    });

    const like = wrapper.find(LIKE_BUTTON);
    expect(like.attributes("aria-pressed")).toBe("false");
    expect(like.text()).toBe("Нравится (1)");
  });

  it("takes back a like that came with the list", async () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn(), {
      likes: [liker("Reader")] as never,
    });

    await wrapper.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    expect(submitUnlike).toHaveBeenCalledWith("c-1");
    expect(submitLike).not.toHaveBeenCalled();
  });

  it("likes a comment the reader has not liked", async () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn());

    await wrapper.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    expect(submitLike).toHaveBeenCalledWith("c-1");
    expect(submitUnlike).not.toHaveBeenCalled();
  });

  it("says what the server answered when it refuses the like", async () => {
    signInAs("Reader");
    submitLike.mockResolvedValue(refused(409, "Вы уже отметили Нравится"));
    const wrapper = mountComponent(vi.fn());

    await wrapper.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).text()).toBe("Вы уже отметили Нравится");
    expect(plates()).toEqual([]);
    expect(wrapper.find(LIKE_BUTTON).attributes("aria-pressed")).toBe("false");
  });

  it("falls back to its own words for a mark and its removal", async () => {
    signInAs("Reader");
    submitLike.mockResolvedValue(refused(404));
    const liking = mountComponent(vi.fn());
    await liking.find(LIKE_BUTTON).trigger("click");
    await flushPromises();

    submitUnlike.mockResolvedValue(refused(404));
    const unliking = mountComponent(vi.fn(), {
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
    const wrapper = mountComponent(vi.fn());

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
    const wrapper = mountComponent(vi.fn());

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
    const found = wrapper
      .findAllComponents(RouterLinkStub)
      .find((link) => link.classes().includes("comment-number"));
    if (!found) throw new Error("Номер комментария не ссылка");
    return found;
  };

  // The link keeps the reader's view: page 3 of a filtered list is not page 3 of the whole.
  it("leads to the comment's anchor in the view of the list the reader is on", () => {
    route.query = {
      number: "3",
      search: "кубы",
      authors: "Experienced",
      sortOrder: "desc",
    };
    const wrapper = mountComponent(vi.fn(), {}, { number: 12 });

    expect(numberLink(wrapper).props("to")).toEqual({
      query: {
        number: "3",
        search: "кубы",
        authors: "Experienced",
        sortOrder: "desc",
      },
      hash: "#comment-c-1",
    });
  });

  it("carries no page number on a page that has none", () => {
    const wrapper = mountComponent(vi.fn(), {}, { number: 12 });

    expect(numberLink(wrapper).props("to")).toEqual({
      query: {},
      hash: "#comment-c-1",
    });
  });

  it("stands last in the footer, outside its caption line, in both layouts", () => {
    localStorage.clear();
    for (const compact of [true, false]) {
      const wrapper = mountComponent(vi.fn(), {}, { number: 12, compact });

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
    const { createRouter, createMemoryHistory } =
      await vi.importActual<typeof import("vue-router")>("vue-router");
    const page = { render: () => null };
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: "/profile/:username", name: "profile", component: page },
        { path: "/:any(.*)*", component: page },
      ],
    });
    await router.push("/forum/newbies/3?number=2&authors=Experienced");
    route.query = { number: "2", authors: "Experienced" };

    const wrapper = mount(CommentItem, {
      global: { plugins: [createPinia(), router] },
      props: {
        comment: comment(),
        number: 12,
        fetchEditSource: vi.fn() as never,
        submitEdit: submitEdit as never,
        submitDelete: submitDelete as never,
        submitLike: submitLike as never,
        submitUnlike: submitUnlike as never,
      },
    });

    const link = wrapper.get("a.comment-number");
    expect(link.attributes("href")).toBe(
      "/forum/newbies/3?number=2&authors=Experienced#comment-c-1",
    );
    expect(link.attributes("aria-current")).toBeUndefined();
  });

  it("has no tooltip, and a name that says which comment it is", () => {
    for (const compact of [true, false]) {
      const wrapper = mountComponent(vi.fn(), {}, { number: 12, compact });

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

  it("is read out with the name the list gives what it counts", () => {
    const wrapper = mountComponent(vi.fn(), {}, { number: 3, noun: "Ответ" });

    expect(numberLink(wrapper).attributes("aria-label")).toBe("Ответ 3");
  });
});

describe("CommentItem full layout", () => {
  it("puts the author's column beside the text and the number under both", () => {
    localStorage.clear();
    const wrapper = mountComponent(vi.fn(), {}, { compact: false, number: 3 });

    const columns = wrapper.get(".comment-columns").element;
    expect([...columns.children].map((child) => child.className)).toEqual([
      "comment-header",
      "comment-body",
    ]);
    const footer = wrapper.get(".comment-footer").element;
    expect(footer.parentElement).toBe(wrapper.element);
    expect(columns.compareDocumentPosition(footer)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });

  it("asks for the avatar at the width it is drawn", () => {
    const wrapper = mountComponent(
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

  it("keeps the compact layout without a column", () => {
    const wrapper = mountComponent(vi.fn(), {}, { compact: true, number: 3 });

    expect(wrapper.find(".comment-header").exists()).toBe(false);
    expect(wrapper.find(".avatar-link").exists()).toBe(false);
    expect(wrapper.find(".comment-edit-line").exists()).toBe(false);
    expect(wrapper.find(".comment-footer .comment-caption").exists()).toBe(
      true,
    );
  });
});
