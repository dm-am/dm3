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
import { formatDateFull } from "@/shared/lib/utils/datetime";
import CommentItem from "./CommentItem.vue";
import type { Comment } from "@/shared/api/models/common/comment";
import {
  submitDelete,
  submitEdit,
  submitLike,
  submitUnlike,
} from "./CommentItem.doubles";
import {
  LIKE_BUTTON,
  RouterLinkStub,
  action,
  comment,
  freshComment,
  mountComponent,
  signInAs,
} from "./CommentItem.fixtures";

vi.mock("vue-router", async () =>
  (await import("./CommentItem.doubles")).vueRouter(),
);

enableAutoUnmount(afterEach);

beforeEach(freshComment);

describe("CommentItem warning", () => {
  const mountItem = (over: Partial<Comment> = {}, number?: number) =>
    mount(CommentItem, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: { template: "<a><slot /></a>" } },
      },
      props: {
        comment: comment(over),
        fetchEditSource: vi.fn() as never,
        submitEdit: submitEdit as never,
        submitDelete: submitDelete as never,
        submitLike: submitLike as never,
        submitUnlike: submitUnlike as never,
        number,
      },
    });

  it("asks the list for the warning window about this comment", async () => {
    signInAs("Mod", "Moderator");
    const wrapper = mountItem({}, 12);

    await action(wrapper, "Предупреждение").trigger("click");

    expect(wrapper.emitted("warn")).toEqual([["c-1", 12]]);
  });

  it.each(["Moderator", "SeniorModerator", "Admin"])(
    "offers a moderator no warning about a comment of a %s",
    (role) => {
      signInAs("Mod", "Moderator");
      const wrapper = mountItem({
        author: { username: "Staff", role, lastActivityUtc: null } as never,
      });

      expect(
        wrapper.findAll(".caption-action").map((one) => one.text()),
      ).not.toContain("Предупреждение");
    },
  );

  it("offers a senior moderator the warning about a comment of a moderator", () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = mountItem({
      author: {
        username: "Staff",
        role: "Moderator",
        lastActivityUtc: null,
      } as never,
    });

    expect(
      wrapper.findAll(".caption-action").map((one) => one.text()),
    ).toContain("Предупреждение");
  });

  it("offers no warning about a comment whose account was removed", () => {
    signInAs("Mod", "Moderator");
    const wrapper = mountItem({ author: null as never });

    expect(
      wrapper.findAll(".caption-action").map((one) => one.text()),
    ).not.toContain("Предупреждение");
  });
});

describe("CommentItem on a blacklist", () => {
  const onTheList = (over: Partial<Comment> = {}) =>
    mountComponent(vi.fn(), over, { viewerBlacklisted: true });

  const actions = (wrapper: VueWrapper) =>
    wrapper.findAll(".caption-action").map((one) => one.text());

  it("offers Нравится on somebody else's comment as it does to anybody", async () => {
    signInAs("Reader");
    const wrapper = onTheList({
      likes: [{ id: "u-1", username: "Other" }] as never,
    });

    await wrapper.get(LIKE_BUTTON).trigger("click");
    await flushPromises();

    expect(submitLike).toHaveBeenCalledWith("c-1");
  });

  it("keeps Удалить and takes Редактировать off the reader's own comment", () => {
    // Signed in as the author, inside the quarter of an hour.
    const free = mountComponent(vi.fn());
    expect(actions(free)).toEqual(["Редактировать", "Удалить"]);

    const listed = onTheList();
    expect(actions(listed)).toEqual(["Удалить"]);
  });
});

describe("CommentItem read only", () => {
  const readOnly = (
    over: Partial<Comment> = {},
    extra: Record<string, unknown> = {},
  ) =>
    mount(CommentItem, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: RouterLinkStub },
      },
      props: { comment: comment(over), readonly: true, number: 3, ...extra },
    });

  it("offers nothing to press, in either layout, even to a senior moderator", () => {
    signInAs("Senior", "SeniorModerator");
    for (const compact of [true, false]) {
      const wrapper = readOnly(
        { likes: [{ id: "u-1", username: "Other" }] as never },
        { compact },
      );

      expect(wrapper.find(".caption-action").exists()).toBe(false);
      expect(wrapper.find(".like-badge").exists()).toBe(false);
      expect(wrapper.text()).not.toMatch(
        /Редактировать|Удалить|Предупреждение/,
      );
      wrapper.unmount();
    }
  });

  it("keeps its number in the corner, and no empty line beside it", () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = readOnly({}, { compact: false });

    expect(wrapper.get(".comment-footer .comment-number").text()).toBe("3");
    expect(wrapper.find(".comment-caption").exists()).toBe(false);
    expect(wrapper.find(".comment-edit-line").exists()).toBe(false);
  });

  it("keeps the author and the date in the compact caption", () => {
    const wrapper = readOnly(
      { createdUtc: "2026-09-09T06:33:00Z" as never },
      { compact: true },
    );

    expect(
      wrapper
        .findAll(".comment-footer .caption-line__part")
        .map((part) => part.text().replace(/\s+/g, " ").trim()),
    ).toEqual([
      "Автор: SolohinLex [offline],",
      formatDateFull("2026-09-09T06:33:00Z").replace(/\s+/g, " "),
    ]);
  });
});
