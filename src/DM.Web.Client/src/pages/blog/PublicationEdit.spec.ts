/**
 * @vitest-environment jsdom
 */

/**
 * Where the editor puts the author down when it is finished.
 *
 * Both exits led to "/blogs/{blog}/feed" — the first page of the unfiltered
 * feed, which names neither the publication nor the rubric it lives in. After
 * a save the author was carried past their own text to a list where it is one
 * card among twenty, and after a delete to a list the publication was never in
 * if it had a rubric, so the one place the deletion is visible stayed unseen.
 * The only thing that said anything had happened was a toast, and a toast is
 * not the result.
 *
 * So the addresses themselves are what this spec asserts, resolved through the
 * real route table rather than compared as objects: what the author sees is a
 * page at a URL, and a named location with the wrong params resolves to the
 * wrong page while still looking plausible in a diff.
 *
 * Nobody but the owner and the assistants has this page: anybody else, a guest
 * included, gets the site's own "not found" at its address, with no refusal
 * drawn, once the session is settled.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useBlogDetailsStore, blogApi, type Rubric } from "@/entities/blog";
import { useAuthStore, type User } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import PublicationEdit from "./PublicationEdit.vue";

// Hoisted with the navigation spy: the route mock below is hoisted above the
// module body, so anything it names has to exist by then.
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

// Only the navigation is taken over; everything else in vue-router stays real,
// the route table included — the assertions below resolve against it.
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

// The answer about the session the start of the page asks for, held by the
// test: settled at once unless a test keeps it out.
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

// The real form pulls TipTap in; this page's business is what happens after
// "Сохранить", so the form is one button that says it was pressed, and the
// refusal line the page hands it (the real form says it over its footer).
vi.mock("@/features/publication", () => ({
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
  // The confirmation is the real window, drawn in place: the deletion is its
  // request, and a refusal is printed in it.
  Teleport: true,
  UnsavedChangesGuard: true,
};

const rubric = (id: string): Rubric =>
  ({ id, title: "Хроники", sortOrder: 1 }) as Rubric;

function render(
  publicationRubric: Rubric | null,
  viewer: string | null = AUTHOR,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  // canManage is derived, not served: the signed-in name has to match the
  // blog's author for the page to draw the form at all.
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

  vi.spyOn(blogApi, "getPublication").mockResolvedValue({
    data: {
      resource: {
        id: PUBLICATION,
        title: "Заметка",
        content: "<p>Текст</p>",
        rubric: publicationRubric,
      },
    },
    error: null,
  } as never);

  return mount(PublicationEdit, { global: { plugins: [pinia], stubs } });
}

/**
 * The location the page left for.
 *
 * Asserted as the named location rather than as a resolved URL: a page may
 * not reach into the app layer for the route table (FSD, enforced by the
 * boundaries rule). That these names and params resolve to the addresses
 * meant here is the other half of the proof, and it lives in
 * src/navigationTargets.spec.ts, where importing the real table is allowed.
 */
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
    // Where the press was, and in no plate (the owner's rule of 26 September
    // 2026, UI_STANDARDS "Всплывающие сообщения").
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

    // The window prints a refusal in itself, so the interceptor keeps quiet.
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

    // An empty query, not a missing one: the page always names the key and
    // leaves it empty, so no stale "?rubric=" survives the navigation.
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
    // A session the browser kept no copy of: the store holds nobody until the
    // server names the viewer, and the author then stays and gets the form.
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
