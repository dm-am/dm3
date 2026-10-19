import { afterEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { createRouter, createWebHistory } from "vue-router";
import { permalinkOrigin } from "@/shared/config/site";
import type { Comment } from "@/shared/api/models/common/comment";
import CommentItem from "../ui/CommentItem.vue";
import { useCommentWarnDialog } from "./useCommentWarnDialog";

// What the list hands the window is the subject, not the window itself.
const modal = vi.hoisted(() => ({
  attrs: {} as Record<string, unknown>,
  open: vi.fn(),
}));
vi.mock("vue-final-modal", () => ({
  useModal: (options: { attrs: Record<string, unknown> }) => {
    modal.attrs = options.attrs;
    return { open: modal.open, close: vi.fn() };
  },
  VueFinalModal: { render: () => null },
}));
vi.mock("@/features/moderation-actions/@x/comment", () => ({
  WarningDialog: { template: "<div />" },
}));
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: { template: "<textarea />" },
}));

async function warnOn(fullPath: string, id: string, number?: number) {
  const router = createRouter({
    history: createWebHistory(),
    routes: [{ path: "/:rest(.*)*", component: { render: () => null } }],
  });
  await router.push(fullPath);
  await router.isReady();

  let warnComment: (id: string, number?: number) => void = () => {};
  mount(
    defineComponent({
      setup() {
        ({ warnComment } = useCommentWarnDialog(
          (commentId) =>
            ({
              id: commentId,
              author: { username: "Experienced" },
              text: "<strong>Грубость</strong> в адрес мастера",
            }) as Comment,
        ));
        return () => h("div");
      },
    }),
    { global: { plugins: [router] } },
  );
  warnComment(id, number);
  return router;
}

describe("useCommentWarnDialog", () => {
  afterEach(() => {
    window.history.replaceState(null, "", "/");
    modal.open.mockClear();
  });

  it("quotes the comment in the view of the list the moderator is on", async () => {
    const router = await warnOn(
      "/forum/newbies/3?authors=Experienced&sortOrder=desc&number=2",
      "c1",
    );
    const query = router.currentRoute.value.query;

    expect(modal.attrs.entityLink).toBe(
      permalinkOrigin() + router.resolve({ query, hash: "#comment-c1" }).href,
    );
    expect(modal.attrs.username).toBe("Experienced");
    expect(modal.open).toHaveBeenCalledOnce();
  });

  it("hands the window the text of the comment and its number", async () => {
    await warnOn("/forum/newbies/3?number=2", "c1", 7);

    expect(modal.attrs.evidence).toBe(
      "<strong>Грубость</strong> в адрес мастера",
    );
    expect(modal.attrs.number).toBe(7);
    expect(modal.attrs.entityId).toBe("c1");
    expect(modal.attrs.entityType).toBe("Comment");
  });

  it("opens the window without a number where the press brought none", async () => {
    await warnOn("/forum/newbies/3", "c1");

    expect(modal.attrs.number).toBeUndefined();
    expect(modal.open).toHaveBeenCalledOnce();
  });
});

// The real item, wired as the lists wire it: what it emits is all the window gets.
describe("the press under a comment", () => {
  afterEach(() => {
    window.history.replaceState(null, "", "/");
    localStorage.clear();
    modal.open.mockClear();
  });

  it("brings the number the comment shows to the window", async () => {
    const router = createRouter({
      history: createWebHistory(),
      routes: [{ path: "/:rest(.*)*", component: { render: () => null } }],
    });
    await router.push("/forum/newbies/3?number=2");
    await router.isReady();
    // A moderator over an author below his rank, so the item offers the warning.
    localStorage.setItem(
      "user",
      JSON.stringify({ username: "Mod", role: "Moderator" }),
    );

    const comment = {
      id: "c1",
      author: { username: "Experienced", role: "RegularUser" },
      createdUtc: new Date().toISOString(),
      modifiedUtc: null,
      text: "<strong>Грубость</strong> в адрес мастера",
      isRemoved: false,
      likes: [],
    } as unknown as Comment;

    const list = mount(
      defineComponent({
        setup() {
          const { warnComment } = useCommentWarnDialog(() => comment);
          return () =>
            h(CommentItem, { comment, number: 27, onWarn: warnComment });
        },
      }),
      {
        global: {
          plugins: [router, createPinia()],
          // The item's links lead to routes this router does not have.
          stubs: { RouterLink: { props: ["to"], template: "<a><slot /></a>" } },
        },
      },
    );

    const press = list
      .findAll(".caption-action")
      .find((action) => action.text() === "Предупреждение");
    expect(press, "the item offers the warning").toBeDefined();
    await press!.trigger("click");

    expect(modal.open).toHaveBeenCalledOnce();
    expect(modal.attrs.entityId).toBe("c1");
    expect(modal.attrs.number).toBe(27);
  });
});
