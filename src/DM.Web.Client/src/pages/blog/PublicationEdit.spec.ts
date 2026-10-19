/**
 * @vitest-environment jsdom
 */

// Where the editor puts the author down: after a save the publication itself,
// after a delete the rubric feed it lay in. Anybody but the owner and the
// assistants gets "not found" once the session is settled.
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useBlogDetailsStore, blogApi, type Rubric } from "@/entities/blog";
import { useAuthStore, type User } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { deferred } from "@/shared/lib/testing/tasks";
import FormSkeleton from "@/shared/ui/Skeleton/FormSkeleton.vue";
import { PublicationForm } from "@/features/publication";
import PublicationEdit from "./PublicationEdit.vue";

// Hoisted with the navigation spy: the route mock below is hoisted above the body.
const { push, replace, session, AUTHOR, BLOG, PUBLICATION } = vi.hoisted(
  () => ({
    push: vi.fn(),
    replace: vi.fn(),
    session: { settled: Promise.resolve() as Promise<void> },
    AUTHOR: "SolohinLex",
    BLOG: "the-blog",
    PUBLICATION: "p-1",
  }),
);

// Only the navigation is taken over; the rest of vue-router stays real.
vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({
    params: { id: BLOG, pubId: PUBLICATION },
    path: `/blogs/${BLOG}/feed/${PUBLICATION}/edit`,
    query: {},
    hash: "",
  }),
  useRouter: () => ({ push, replace }),
}));

// The session answer, settled at once unless a test keeps it out.
vi.mock("@/entities/user", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/user")>()),
  restoreSession: () => session.settled,
}));

const NOT_FOUND = {
  name: "not-found",
  params: { pathMatch: ["blogs", BLOG, "feed", PUBLICATION, "edit"] },
  query: {},
  hash: "",
};

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

function render(
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
  store.blog = {
    id: BLOG,
    title: "Записки",
    author: { username: AUTHOR },
    assistants: [],
    rubrics: publicationRubric ? [publicationRubric] : [],
  } as never;
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

  return mount(PublicationEdit, { global: { plugins: [pinia], stubs } });
}

/** The named location the page left for; navigationTargets.spec.ts resolves the names. */
const wentTo = () => push.mock.calls.at(-1)?.[0];

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
    const wrapper = render(null);
    await flushPromises();

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(wentTo()).toEqual({
      name: "blog-publication",
      params: { id: BLOG, pubId: PUBLICATION },
    });
  });

  it("opens the editor on the text as typed and writes back the title alone", async () => {
    const update = vi
      .spyOn(blogApi, "updatePublication")
      .mockResolvedValue({ data: {}, error: null } as never);
    const wrapper = render(null);
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
    const wrapper = render(null);
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
    const wrapper = render(null);
    await flushPromises();

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(push).not.toHaveBeenCalled();
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
    const wrapper = render(publicationRubric);
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
    expect(wentTo()).toEqual({
      name: "blog-feed",
      params: { id: BLOG },
      query: { rubric: "r-7" },
    });
  });

  it("returns to the plain feed when the publication had no rubric", async () => {
    vi.spyOn(blogApi, "deletePublication").mockResolvedValue({
      error: null,
    } as never);

    await deletePublication(null);

    // An empty query, so no stale "?rubric=" survives the navigation.
    expect(wentTo()).toEqual({
      name: "blog-feed",
      params: { id: BLOG },
      query: {},
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

    expect(push).not.toHaveBeenCalled();
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
    const wrapper = render(null, "a-reader");
    await flushPromises();

    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("opens it for a guest too, with no invitation to sign in", async () => {
    const wrapper = render(null, null);
    await flushPromises();

    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("waits for the answer about the session before sending anybody away", async () => {
    // A session the browser kept no copy of: the author stays once the server names him.
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    const wrapper = render(null, null);
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();

    useAuthStore().user = { username: AUTHOR } as User;
    answer();
    await flushPromises();
    expect(replace).not.toHaveBeenCalled();
    expect(wrapper.find(".save").exists()).toBe(true);
  });

  it("keeps the author of the blog on the form", async () => {
    const wrapper = render(null);
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();
    expect(wrapper.find(".save").exists()).toBe(true);
  });
});

// Until the publication answers, the form stands as its skeleton and nothing is called missing.
describe("PublicationEdit — on its way", () => {
  beforeEach(() => vi.clearAllMocks());

  it("stands the skeleton of the form, then the form", async () => {
    const pending = deferred<unknown>();
    const wrapper = render(null, AUTHOR, pending.promise);
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
