/**
 * @vitest-environment jsdom
 */

// Where the editor puts the author down: after a save the publication itself,
// after a delete the rubric feed it lay in. Anybody but the owner and the
// assistants gets "not found" once the session is settled.
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type { Router } from "vue-router";
import { useBlogDetailsStore, blogApi, type Rubric } from "@/entities/blog";
import { createMockBlog } from "@/entities/blog/model/store.fixtures";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { UserRole, type Username } from "@/shared/api/models/common";
import { useAuthStore, type User } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { deferred } from "@/shared/lib/testing/tasks";
import FormSkeleton from "@/shared/ui/Skeleton/FormSkeleton.vue";
import { PublicationForm } from "@/features/publication";
import PublicationEdit from "./PublicationEdit.vue";

const { session } = vi.hoisted(() => ({
  session: { settled: Promise.resolve() as Promise<void> },
}));

const AUTHOR = "SolohinLex";
const BLOG = "the-blog";
const PUBLICATION = "p-1";

// The session answer, settled at once unless a test keeps it out.
vi.mock("@/entities/user", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/user")>()),
  restoreSession: () => session.settled,
}));

const EDIT = `/blogs/${BLOG}/feed/${PUBLICATION}/edit`;

let router: Router;
/** Where the page stands now: the route name and the address. */
const standing = () => ({
  name: router.currentRoute.value.name,
  fullPath: router.currentRoute.value.fullPath,
});
// The site's not found keeps the address it was opened at.
const NOT_FOUND = { name: "not-found", fullPath: EDIT };
const STAYED = { name: "blog-publication-edit", fullPath: EDIT };

// The real form pulls TipTap; here it is one button and the refusal line the page hands it.
vi.mock("@/features/publication", () => ({
  PUBLICATION_EDITOR_HEIGHT: 240,
  PublicationForm: defineComponent({
    inheritAttrs: false,
    props: { refusal: { type: String, default: null } },
    emits: ["submit", "cancel"],
    setup:
      (props, { emit }) =>
      () =>
        h("div", [
          h("button", { class: "save", onClick: () => emit("submit") }),
          props.refusal
            ? h("p", { class: "form-refusal" }, props.refusal)
            : null,
        ]),
  }),
}));

const stubs = {
  // The confirmation is the real window, drawn in place.
  Teleport: true,
  UnsavedChangesGuard: true,
};

/** The content as the author_edit audience answers it: the BBCode as typed. */
const TYPED =
  "Текст с [b]разметкой[/b]\n[spoiler]и спойлером[/spoiler], Том &amp; Джерри";

const rubric = (id: string): Rubric =>
  ({ id, title: "Хроники", sortOrder: 1 }) as Rubric;

async function render(
  publicationRubric: Rubric | null,
  viewer: string | null = AUTHOR,
  /** The answer about the publication, when a test holds it back. */
  pending?: Promise<unknown>,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  // canManage is derived: the signed-in name has to match the blog's author.
  useAuthStore().user = viewer ? ({ username: viewer } as User) : null;
  const store = useBlogDetailsStore();
  const blog = createMockBlog(BLOG, "Записки");
  store.blog = {
    ...blog,
    author: {
      id: "u-author",
      username: AUTHOR as Username,
      lastActivityUtc: null,
      role: UserRole.RegularUser,
      isNewbie: false,
    },
    assistants: [],
    rubrics: publicationRubric ? [publicationRubric] : [],
  };
  vi.spyOn(store, "loadBlog").mockResolvedValue({ ok: true });

  const getPublication = vi.spyOn(blogApi, "getPublicationForEdit");
  if (pending) getPublication.mockReturnValue(pending as never);
  else
    getPublication.mockResolvedValue({
      data: {
        resource: {
          id: PUBLICATION,
          title: "Заметка",
          content: TYPED,
          rubric: publicationRubric,
        },
      },
      error: null,
    } as never);

  const mounted = await mountPage(PublicationEdit, EDIT, {
    routes: [
      ["blog-publication-edit", "/blogs/:id/feed/:pubId/edit"],
      ["blog-publication", "/blogs/:id/feed/:pubId"],
      ["blog-feed", "/blogs/:id/feed"],
      ["not-found", "/:pathMatch(.*)*"],
    ],
    pinia,
    stubs,
  });
  router = mounted.router;
  return mounted.wrapper;
}

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const refused = { status: 400, title: "" };

describe("PublicationEdit — after a save", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  it("opens the publication it has just saved", async () => {
    vi.spyOn(blogApi, "updatePublication").mockResolvedValue({
      data: {},
      error: null,
    } as never);
    const wrapper = await render(null);
    await flushPromises();

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(standing()).toEqual({
      name: "blog-publication",
      fullPath: `/blogs/${BLOG}/feed/${PUBLICATION}`,
    });
  });

  it("opens the editor on the text as typed and writes back the title alone", async () => {
    const update = vi
      .spyOn(blogApi, "updatePublication")
      .mockResolvedValue({ data: {}, error: null } as never);
    const wrapper = await render(null);
    await flushPromises();

    expect(blogApi.getPublicationForEdit).toHaveBeenCalledWith(PUBLICATION);
    expect(wrapper.getComponent(PublicationForm).vm.$attrs.content).toBe(TYPED);
    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(update).toHaveBeenCalledWith(PUBLICATION, { title: "Заметка" });
  });

  it("says nothing on top of it: the publication itself is the answer", async () => {
    vi.spyOn(blogApi, "updatePublication").mockResolvedValue({
      data: {},
      error: null,
    } as never);
    const wrapper = await render(null);
    await flushPromises();

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps the author on the form and names the refusal", async () => {
    vi.spyOn(blogApi, "updatePublication").mockResolvedValue({
      data: null,
      error: refused,
    } as never);
    const wrapper = await render(null);
    await flushPromises();

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(standing()).toEqual(STAYED);
    // Where the press was, and in no plate.
    expect(wrapper.get(".form-refusal").text()).toBe(
      "Не удалось сохранить публикацию",
    );
    expect(saidOutLoud()).toEqual([]);
    expect(wrapper.find(".save").exists()).toBe(true);
  });
});

describe("PublicationEdit — after a delete", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  async function deletePublication(publicationRubric: Rubric | null) {
    const wrapper = await render(publicationRubric);
    await flushPromises();

    await wrapper.find(".delete-publication").trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return wrapper;
  }

  it("returns to the feed of the rubric the publication lay in", async () => {
    const remove = vi
      .spyOn(blogApi, "deletePublication")
      .mockResolvedValue({ error: null } as never);

    await deletePublication(rubric("r-7"));

    expect(remove).toHaveBeenCalledWith(PUBLICATION, {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(standing()).toEqual({
      name: "blog-feed",
      fullPath: `/blogs/${BLOG}/feed?rubric=r-7`,
    });
  });

  it("returns to the plain feed when the publication had no rubric", async () => {
    vi.spyOn(blogApi, "deletePublication").mockResolvedValue({
      error: null,
    } as never);

    await deletePublication(null);

    // An empty query, so no stale "?rubric=" survives the navigation.
    expect(standing()).toEqual({
      name: "blog-feed",
      fullPath: `/blogs/${BLOG}/feed`,
    });
  });

  it("says nothing on top of it: the feed comes back without the card", async () => {
    vi.spyOn(blogApi, "deletePublication").mockResolvedValue({
      error: null,
    } as never);

    await deletePublication(rubric("r-7"));

    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps the author on the form and names the refusal in the window", async () => {
    vi.spyOn(blogApi, "deletePublication").mockResolvedValue({
      error: refused,
    } as never);

    const wrapper = await deletePublication(rubric("r-7"));

    expect(standing()).toEqual(STAYED);
    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось удалить публикацию",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
    expect(wrapper.find(".delete-publication").exists()).toBe(true);
  });
});

describe("PublicationEdit — who has the page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    session.settled = Promise.resolve();
  });

  it("opens the site's not found for a reader of the blog, with no refusal drawn", async () => {
    const wrapper = await render(null, "a-reader");
    await flushPromises();

    expect(standing()).toEqual(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("opens it for a guest too, with no invitation to sign in", async () => {
    const wrapper = await render(null, null);
    await flushPromises();

    expect(standing()).toEqual(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("waits for the answer about the session before sending anybody away", async () => {
    // A session the browser kept no copy of: the author stays once the server names him.
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    const wrapper = await render(null, null);
    await flushPromises();

    expect(standing()).toEqual(STAYED);

    useAuthStore().user = { username: AUTHOR } as User;
    answer();
    await flushPromises();
    expect(standing()).toEqual(STAYED);
    expect(wrapper.find(".save").exists()).toBe(true);
  });

  it("keeps the author of the blog on the form", async () => {
    const wrapper = await render(null);
    await flushPromises();

    expect(standing()).toEqual(STAYED);
    expect(wrapper.find(".save").exists()).toBe(true);
  });
});

// Until the publication answers, the form stands as its skeleton and nothing is called missing.
describe("PublicationEdit — on its way", () => {
  beforeEach(() => vi.clearAllMocks());

  it("stands the skeleton of the form, then the form", async () => {
    const pending = deferred<unknown>();
    const wrapper = await render(null, AUTHOR, pending.promise);
    await flushPromises();

    expect(wrapper.findComponent(FormSkeleton).exists()).toBe(true);
    // Its last field is the editor at the editor's height, so the form moves nothing.
    expect(wrapper.findComponent(FormSkeleton).props("editorHeight")).toBe(240);
    expect(wrapper.text()).not.toContain("Публикация не найдена");
    expect(wrapper.text()).not.toContain("Загрузка");

    pending.resolve({
      data: { resource: { id: PUBLICATION, title: "Заметка", content: "" } },
      error: null,
    });
    await flushPromises();

    expect(wrapper.findComponent(FormSkeleton).exists()).toBe(false);
    expect(wrapper.find(".save").exists()).toBe(true);
  });
});
