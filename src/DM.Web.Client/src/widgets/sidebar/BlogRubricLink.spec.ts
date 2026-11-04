/**
 * @vitest-environment jsdom
 */

// The words a screen reader says for the counters of a rubric: a guest gets
// totals from the server, so they are not called unread, as for a blog row.
import { describe, it, expect, beforeEach } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import BlogRubricLink from "./BlogRubricLink.vue";

const RUBRIC = {
  id: "r-1",
  title: "Хроники",
  unreadPublicationsCount: 5,
  unreadCommentsCount: 12,
};

function labels() {
  const wrapper = mount(BlogRubricLink, {
    props: { rubric: RUBRIC as never, blogId: "b-1" },
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  return wrapper
    .findAll(".counter-pair a")
    .map((link) => link.attributes("aria-label"));
}

describe("BlogRubricLink counters", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("names the totals to a guest", () => {
    expect(labels()).toEqual(["Публикаций: 5", "Комментариев: 12"]);
  });

  it("names the unread counts to a signed-in reader", () => {
    useAuthStore().updateUser({ username: "reader" } as never);

    expect(labels()).toEqual([
      "Непрочитанных публикаций: 5",
      "Непрочитанных комментариев: 12",
    ]);
  });
});
