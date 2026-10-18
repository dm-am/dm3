import { afterEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { createRouter, createWebHistory } from "vue-router";
import { permalinkOrigin } from "@/shared/config/site";
import type { Comment } from "@/shared/api/models/common/comment";
import CommentItem from "../ui/CommentItem.vue";
import { useCommentWarnDialog } from "./useCommentWarnDialog";

// What the list hands the window is the subject here, not the window itself:
// the modal keeps the attributes it was given, and opening it is a call.
const modal = vi.hoisted(() => ({
  attrs: {} as Record<string, unknown>,
  open: vi.fn(),
}));
vi.mock("vue-final-modal", () => ({
  useModal: (options: { attrs: Record<string, unknown> }) => {
    modal.attrs = options.attrs;
    return { open: modal.open, close: vi.fn() };
  },
  // The item's own question (ConfirmDialog) stands in a window too; no
  // question is asked here, so its tier draws nothing.
  VueFinalModal: { render: () => null },
}));
vi.mock("@/features/moderation-actions/@x/comment", () => ({
  WarningDialog: { template: "<div />" },
}));
// The item's editor is not opened here; the real one is a heavy tiptap tree.
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

    // The same address as the number of the comment (CommentItem, numberTo):
    // the page number of a filtered list is a page of that view.
    expect(modal.attrs.entityLink).toBe(
      permalinkOrigin() + router.resolve({ query, hash: "#comment-c1" }).href,
    );
    expect(modal.attrs.username).toBe("Experienced");
    expect(modal.open).toHaveBeenCalledOnce();
  });

  it("quotes the first page of the whole list without a query", async () => {
    await warnOn("/forum/newbies/3", "c1");

    expect(modal.attrs.entityLink).toBe(
      permalinkOrigin() + "/forum/newbies/3#comment-c1",
    );
  });

  /**
   * The window shows the violation as the comment itself, not as an address:
   * its text as the list renders it, and the number it shows in its corner,
   * which comes with the press.
   */
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

/**
 * The lists wire the press under a comment straight to the window
 * (`@warn="warnComment"` in DiscussionSection and CommentsList): what the item
 * emits is all the window is handed. So the number the item shows has to leave
 * it with the press, or the window has no number to link to the comment. The
 * item is the real one here, wired the way the lists wire it.
 */
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
    // A moderator reads, and the author is below his rank, so the item offers
    // the warning (canWarn). The auth store reads the reader from here.
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
          // The item's links (author, number) lead to routes of the site
          // that this router does not have.
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
