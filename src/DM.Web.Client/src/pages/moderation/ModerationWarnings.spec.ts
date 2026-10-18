/**
 * @vitest-environment jsdom
 */

/**
 * The moderation list of warnings, now that it is a page rather than the whole
 * history of the website.
 *
 * It used to ask for every warning ever issued and sort the answer in the
 * browser. Both halves went: the server orders the log, and re-sorting a page
 * would only reorder that page against the others. What is checked here is the
 * page: the number in the address reaching the request, the row keeping the
 * place the server gave it, a correction of the points re-reading the page the
 * moderator is on, and the way back when that page turns out to hold nothing.
 *
 * The way back is checked by where it leads rather than by what it says, and
 * the answer to a page the moderator has left is checked to stay off the
 * screen: a link that keeps the stale number and a list that shows the page
 * nobody asked for both read as correct until the target is opened.
 *
 * The name filter is checked through the address on both sides, because that is
 * where it lives: what the address says reaches the request and the field, and
 * what the moderator types reaches the address without the page number of the
 * list they are leaving. A filter kept in a ref here would pass every one of
 * those checks written against the request alone, and still hand over a link
 * that opens the whole log. The link to the next page is checked with the real
 * control mounted, because the filter and the number share one address and the
 * control is what merges them.
 *
 * How a warning is drawn is the card's own spec; this page owns the controls it
 * puts in the card and nothing else about the block. There is no removal to
 * check any more: a warning is a record of a decision and stays, so the control
 * corrects the points instead, and whether it is offered at all is the server's
 * answer on the row rather than a role read here.
 *
 * The other control is "Удалить картинку" on a record of a profile: the avatar
 * the profile had when the warning was issued stays in both logs after the
 * person changes it, and a picture that may not be shown even as evidence is
 * taken out of the record here, after a question. It is offered where the
 * record has a picture to take out, and not on the moderator's own record: the
 * server reads a mark set by the owner of a picture as the owner's own doing,
 * so the picture would stay in the log and leave his profile instead.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import type { Mock } from "vitest";
import type { Ref } from "vue";
import * as vueRouter from "vue-router";
import ModerationWarnings from "./ModerationWarnings.vue";

const getAllWarnings = vi.hoisted(() => vi.fn());
const removeWarningProfilePicture = vi.hoisted(() => vi.fn());

vi.mock("@/features/moderation-actions", () => ({
  ChangeWarningPointsDialog: { template: "<div class='dialog-stub' />" },
}));

vi.mock("@/entities/moderation", () => ({
  moderationApi: { getAllWarnings, removeWarningProfilePicture },
  WarningCard: {
    props: ["warning"],
    template:
      "<div class='card-stub'><span class='reason-stub'>{{ warning.reason }}</span><div class='record-controls-stub'><slot name='record-controls' /></div><div class='controls-stub'><slot name='controls' /></div></div>",
  },
  RecordSkeleton: {
    props: ["count"],
    template: "<div class='skeleton-stub' />",
  },
}));

/**
 * The correction dialog is the library's, opened by hand rather than mounted in
 * the template, so the seam this page owns is the options it hands over: which
 * warning the form is about, and what it does when the form comes back. The
 * form itself has its own spec.
 */
const modalOpen = vi.hoisted(() => vi.fn());
const modalClose = vi.hoisted(() => vi.fn());
const modal = vi.hoisted(() => ({
  attrs: null as Record<string, unknown> | null,
}));

vi.mock("vue-final-modal", () => ({
  useModal: (options: { attrs: Record<string, unknown> }) => {
    modal.attrs = options.attrs;
    return { open: modalOpen, close: modalClose };
  },
}));

const toastSuccess = vi.hoisted(() => vi.fn());

/**
 * The question before the picture goes is the site's window (ConfirmDialog):
 * what it is asked to say and what it runs are what this page decides, the
 * window's wait and refusal are its own spec.
 */
vi.mock("@/shared/ui/ConfirmDialog", () => ({
  ConfirmDialog: {
    props: ["show", "title", "message", "confirmLabel", "failure", "action"],
    emits: ["update:show"],
    template:
      "<div v-if='show' class='confirm-stub' :data-title='title' :data-message='message' :data-confirm='confirmLabel' :data-failure='failure'><button class='confirm-go' @click='action()' /><button class='confirm-cancel' @click=\"$emit('update:show', false)\" /></div>",
  },
}));

/**
 * The address bar, held in a ref rather than a plain object: the page derives
 * the page number inside a computed and refetches on its change, and a plain
 * property read there is tracked by nothing. The ref leaves through the mocked
 * module because the test and the page must be looking at the same one.
 *
 * The push moves that same ref, the way a real navigation would: a filter that
 * only reaches the router is a filter the reader never sees applied, so the
 * tests below follow it all the way round to the next request.
 *
 * It also refuses to move when the address it is given is the one already open,
 * as the real router does. A page that leans on the address coming back to it
 * would pass every check against a push that always assigns, and leave the
 * reader looking at a field the address never answered.
 */
vi.mock("vue-router", async () => {
  const { shallowRef } = await import("vue");
  const query = shallowRef<Record<string, string>>({});
  const isCurrent = (next: Record<string, string>) => {
    const current = query.value;
    const keys = Object.keys(next);
    return (
      keys.length === Object.keys(current).length &&
      keys.every((key) => current[key] === next[key])
    );
  };
  const push = vi.fn((to: { query?: Record<string, string> }) => {
    const next = { ...(to.query ?? {}) };
    if (isCurrent(next)) return;
    query.value = next;
  });
  return {
    useRoute: () => ({
      get query() {
        return query.value;
      },
    }),
    useRouter: () => ({ push }),
    routeQuery: query,
    routerPush: push,
  };
});

const { routeQuery, routerPush } = vueRouter as unknown as {
  routeQuery: Ref<Record<string, string>>;
  routerPush: Mock;
};

// The gate has its own spec (roleGate); here the viewer is a moderator.
vi.mock("./lib/useRoleGate", () => ({
  useRoleGate: () => ({
    hasAccess: true,
    deniedText: "",
    user: { value: { id: "u-me", username: "Модератор" } },
  }),
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ success: toastSuccess, error: vi.fn() }),
}));

/** The page size the neighbouring moderation lists keep. */
const PAGE_SIZE = 20;

const warning = (over: Record<string, unknown> = {}) => ({
  id: "w-1",
  user: { username: "Нарушитель" },
  moderator: { username: "Модератор" },
  points: 2,
  originalPoints: 2,
  reason: "Оскорбление",
  createdUtc: "2026-05-01T12:00:00",
  // Who may correct which row is the server's answer, and the row carries it.
  canChangePoints: true,
  ...over,
});

const page = (resources: unknown[], total = resources.length, pages = 1) => ({
  data: {
    resources,
    paging: { skip: 0, take: PAGE_SIZE, total, current: 1, pages },
  },
  error: null,
});

// The address is shared by every mount in this file, so a page left standing
// would refetch when the next test moves the query under it.
enableAutoUnmount(afterEach);

/**
 * The paging control is stubbed everywhere except where the address it links to
 * is the subject: the links it builds are its own, but which address they carry
 * is what this page and its filter agree on.
 */
function render({ realPaging = false }: { realPaging?: boolean } = {}) {
  return mount(ModerationWarnings, {
    global: {
      stubs: {
        RouterLink: {
          props: ["to"],
          template:
            "<a class='router-link' :data-to='JSON.stringify(to)'><slot /></a>",
        },
        PageTitle: { template: "<h1><slot /></h1>" },
        "page-title": { template: "<h1><slot /></h1>" },
        // Registered on the app, so a mount of the page alone knows neither.
        FormField: {
          props: ["label", "name"],
          template:
            "<div class='field-stub'><label>{{ label }}</label><slot /></div>",
        },
        Button: { template: "<button><slot /></button>" },
        ...(realPaging
          ? {}
          : {
              Paging: {
                props: ["paging"],
                template: "<nav class='paging-stub' />",
              },
            }),
        ErrorState: { template: "<div class='error-stub' />" },
      },
    },
  });
}

/** The field of the search line. */
function nameField(wrapper: ReturnType<typeof render>) {
  return wrapper.find<HTMLInputElement>("form input");
}

async function rendered(options?: { realPaging?: boolean }) {
  const wrapper = render(options);
  await flushPromises();
  return wrapper;
}

describe("the moderation warnings page", () => {
  beforeEach(() => {
    getAllWarnings.mockReset();
    modalOpen.mockReset();
    modalClose.mockReset();
    toastSuccess.mockReset();
    routerPush.mockClear();
    routeQuery.value = {};
  });

  it("asks for the page named in the address, at the size of the list", async () => {
    routeQuery.value = { number: "3" };
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();

    expect(getAllWarnings).toHaveBeenCalledWith({
      skip: 2 * PAGE_SIZE,
      take: PAGE_SIZE,
    });
    expect(wrapper.find(".paging-stub").exists()).toBe(true);
  });

  it("keeps the order the server sent, oldest row included", async () => {
    getAllWarnings.mockResolvedValue(
      page([
        warning({
          id: "w-1",
          reason: "Старое",
          createdUtc: "2026-01-01T00:00:00",
        }),
        warning({
          id: "w-2",
          reason: "Новое",
          createdUtc: "2026-05-01T00:00:00",
        }),
      ]),
    );

    const wrapper = await rendered();

    expect(wrapper.findAll(".reason-stub").map((row) => row.text())).toEqual([
      "Старое",
      "Новое",
    ]);
  });

  it("puts the control that changes the points inside the card", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();

    expect(wrapper.find(".controls-stub .caption-action").text()).toBe(
      "Изменить баллы",
    );
  });

  /**
   * A moderator may correct his own warnings and a senior moderator anybody's,
   * and the server has already decided which of the two this reader is for this
   * row. A control offered anyway would be a request refused with 403 after the
   * form was filled in.
   */
  it("offers no control on a row the server did not open", async () => {
    getAllWarnings.mockResolvedValue(
      page([warning({ canChangePoints: false })]),
    );

    const wrapper = await rendered();

    expect(wrapper.find(".caption-action").exists()).toBe(false);
  });

  it("opens the form on the warning the control belongs to", async () => {
    getAllWarnings.mockResolvedValue(page([warning({ id: "w-7", points: 4 })]));

    const wrapper = await rendered();
    await wrapper.find(".caption-action").trigger("click");

    expect(modalOpen).toHaveBeenCalled();
    expect(modal.attrs?.warningId).toBe("w-7");
    expect(modal.attrs?.currentPoints).toBe(4);
  });

  /**
   * The row keeps its place in the log whatever its points become — a warning
   * is never withdrawn — so the page to re-read is the one the moderator is on,
   * not the first.
   */
  it("re-reads the page the moderator is on after a correction", async () => {
    routeQuery.value = { number: "2" };
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();
    await wrapper.find(".caption-action").trigger("click");
    await (modal.attrs?.onSuccess as () => Promise<void>)();
    await flushPromises();

    expect(modalClose).toHaveBeenCalled();
    expect(toastSuccess).not.toHaveBeenCalled();
    expect(getAllWarnings).toHaveBeenCalledTimes(2);
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      skip: PAGE_SIZE,
      take: PAGE_SIZE,
    });
  });

  /** A form closed without saving changes nothing and says nothing. */
  it("re-reads nothing when the form is closed unsaved", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();
    await wrapper.find(".caption-action").trigger("click");
    (modal.attrs?.onCancel as () => void)();
    await flushPromises();

    expect(modalClose).toHaveBeenCalled();
    expect(toastSuccess).not.toHaveBeenCalled();
    expect(getAllWarnings).toHaveBeenCalledTimes(1);
  });

  /**
   * The last row of the last page removed, or a stale link. The page says so
   * and offers the way back instead of moving the reader on its own.
   */
  it("offers the first page when this one is past the end of a filled log", async () => {
    routeQuery.value = { number: "4" };
    getAllWarnings.mockResolvedValue(page([], 3));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain("На этой странице предупреждений нет");
    const back = wrapper.find(".warnings-empty a");
    expect(back.text()).toBe("Вернуться на первую страницу");
    expect(JSON.parse(back.attributes("data-to") ?? "null")).toEqual({
      name: "moderation-warnings",
      query: {},
    });
  });

  /**
   * Two page numbers on the wire at once, which a second click of the paging
   * control is enough to produce. What the moderator asked for last is what
   * stays, however late the answer to the page they left arrives.
   */
  it("keeps the page the address names when a later answer belongs to an older one", async () => {
    const answers = new Map<number, (value: unknown) => void>();
    getAllWarnings.mockImplementation(
      ({ skip }: { skip: number }) =>
        new Promise((resolve) => {
          answers.set(skip, resolve);
        }),
    );

    const wrapper = render();
    await flushPromises();

    routeQuery.value = { number: "2" };
    await flushPromises();

    answers.get(PAGE_SIZE)?.(page([warning({ id: "w-2", reason: "Новое" })]));
    await flushPromises();
    answers.get(0)?.(page([warning({ reason: "Старое" })]));
    await flushPromises();

    expect(wrapper.findAll(".reason-stub").map((row) => row.text())).toEqual([
      "Новое",
    ]);
  });

  it("says the log is empty only when it really is", async () => {
    getAllWarnings.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain("Предупреждений пока нет");
  });

  /**
   * The name filter: the search line the sitewide uploads list carries, the
   * site's search field and "Найти" beside it with no caption over them,
   * because a moderator moving between the two lists narrows both the same
   * way. The server finds the user by his exact name, and the field says so.
   */
  it("draws the search line the neighbouring moderation lists draw", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();

    expect(wrapper.find("label").exists()).toBe(false);
    expect(wrapper.find("form .search-container").exists()).toBe(true);
    expect(nameField(wrapper).attributes("placeholder")).toBe(
      "Точное имя пользователя",
    );
    expect(wrapper.find("form button[type=submit]").text()).toBe("Найти");
  });

  /**
   * An emptied field, by its cross or by hand, drops the filter without
   * "Найти": a list narrowed to a name under a field that names nobody would
   * say two things at once.
   */
  it("drops the name at once when the field is emptied", async () => {
    routeQuery.value = { username: "Нарушитель", number: "2" };
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();
    await wrapper.find("form .clear-input-btn").trigger("click");
    await flushPromises();

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-warnings",
      query: {},
    });
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      username: undefined,
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  /** Typing a name is no search yet: the server looks up whole names. */
  it("asks for nothing while the name is being typed", async () => {
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();
    await nameField(wrapper).setValue("Нару");
    await flushPromises();

    expect(routerPush).not.toHaveBeenCalled();
    expect(getAllWarnings).toHaveBeenCalledTimes(1);
  });

  it("asks for the name the address names", async () => {
    routeQuery.value = { username: "Нарушитель" };
    getAllWarnings.mockResolvedValue(page([warning()]));

    await rendered();

    expect(getAllWarnings).toHaveBeenCalledWith({
      username: "Нарушитель",
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  /** A link handed to another moderator opens with the filter already applied. */
  it("shows the name the address names in the field", async () => {
    routeQuery.value = { username: "Нарушитель" };
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();

    expect(nameField(wrapper).element.value).toBe("Нарушитель");
  });

  /**
   * The page number counted pages of the list being left, so it does not travel
   * with the new name: kept, it would open the narrowed list at a page that may
   * not exist and the moderator would read an empty screen for a name that has
   * warnings.
   */
  it("puts the name in the address without the page number of the previous list", async () => {
    routeQuery.value = { number: "3" };
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();
    await nameField(wrapper).setValue("  Нарушитель  ");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-warnings",
      query: { username: "Нарушитель" },
    });
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      username: "Нарушитель",
      skip: 0,
      take: PAGE_SIZE,
    });
    // The address is what the field reads back, padding and all gone: the two
    // must not be able to disagree about which name the list is showing.
    expect(nameField(wrapper).element.value).toBe("Нарушитель");
  });

  it("takes the name out of the address when the field is emptied", async () => {
    routeQuery.value = { username: "Нарушитель", number: "2" };
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();
    await nameField(wrapper).setValue("");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-warnings",
      query: {},
    });
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      username: undefined,
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  /**
   * The same name again, padded: the address is already where the filter would
   * send it, so the router stays put and hands nothing back. The field still
   * has to end up showing the name the list is showing.
   */
  it("shows the trimmed name when the address is already the one asked for", async () => {
    routeQuery.value = { username: "Нарушитель" };
    getAllWarnings.mockResolvedValue(page([warning()]));

    const wrapper = await rendered();
    await nameField(wrapper).setValue("  Нарушитель  ");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(nameField(wrapper).element.value).toBe("Нарушитель");
  });

  /**
   * The filter and the page number share one address, and the link to the next
   * page has to carry both. A second page of the whole log under a field with a
   * name in it reads as correct until its rows are compared with the name.
   */
  it("keeps the name in the address the paging control links to", async () => {
    routeQuery.value = { username: "Нарушитель" };
    getAllWarnings.mockResolvedValue(page([warning()], 25, 2));

    const wrapper = await rendered({ realPaging: true });

    const second = wrapper
      .findAll(".paging a")
      .find((link) => link.text() === "2");
    expect(JSON.parse(second?.attributes("data-to") ?? "null")).toEqual({
      name: "moderation-warnings",
      query: { username: "Нарушитель", number: "2" },
    });
  });

  /**
   * An empty list under a filter is not an empty log, and the moderator has to
   * be able to tell one from the other before deciding whether the name was
   * mistyped.
   */
  it("says nothing was found when the filter matches nobody", async () => {
    routeQuery.value = { username: "Нарушитель" };
    getAllWarnings.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain(
      "Предупреждений по заданным фильтрам не найдено",
    );
    expect(wrapper.text()).not.toContain("Предупреждений пока нет");
  });
});

/** A record of a profile, with the avatar of the moment or without one. */
const onProfile = (over: Record<string, unknown> = {}) =>
  warning({
    id: "w-p",
    entityType: "Profile",
    profile: {
      status: "Максимальная длина",
      info: "О себе",
      picture: { originalUrl: "/u/original.png" },
    },
    canChangePoints: false,
    ...over,
  });

describe("the picture of a record of a profile", () => {
  beforeEach(() => {
    getAllWarnings.mockReset();
    removeWarningProfilePicture.mockReset();
    routerPush.mockClear();
    routeQuery.value = {};
  });

  const actionOf = (wrapper: ReturnType<typeof render>, index: number) =>
    wrapper
      .findAll(".card-stub")
      [index].find(".record-controls-stub .caption-action");

  it("offers to take the picture out only where the record has one", async () => {
    getAllWarnings.mockResolvedValue(
      page([
        onProfile(),
        onProfile({
          id: "w-2",
          profile: { status: "Без картинки", info: null, picture: null },
        }),
        onProfile({ id: "w-3", profile: null }),
        warning({ id: "w-4", entityType: "Comment" }),
      ]),
    );

    const wrapper = await rendered();

    expect(actionOf(wrapper, 0).text()).toBe("Удалить картинку");
    for (const index of [1, 2, 3])
      expect(actionOf(wrapper, index).exists()).toBe(false);
  });

  /**
   * The moderator's own record, issued to him before he became one: the
   * server would read his mark as the owner's own and keep the picture in the
   * log, taking it off his profile instead. What would not happen is not
   * offered.
   */
  it("offers nothing on the moderator's own record", async () => {
    getAllWarnings.mockResolvedValue(
      page([onProfile({ user: { id: "u-me", username: "Модератор" } })]),
    );

    const wrapper = await rendered();

    expect(actionOf(wrapper, 0).exists()).toBe(false);
  });

  it("asks before the picture goes, in the site's window", async () => {
    getAllWarnings.mockResolvedValue(page([onProfile()]));

    const wrapper = await rendered();
    expect(wrapper.find(".confirm-stub").exists()).toBe(false);
    await actionOf(wrapper, 0).trigger("click");

    const confirm = wrapper.get(".confirm-stub");
    expect(confirm.attributes("data-title")).toBe("Удалить картинку?");
    expect(confirm.attributes("data-message")).toBe(
      "Картинка пропадет из записи в обоих логах, вернуть ее будет нельзя",
    );
    expect(confirm.attributes("data-confirm")).toBe("Удалить");
    expect(confirm.attributes("data-failure")).toBe(
      "Не удалось удалить картинку",
    );
    expect(removeWarningProfilePicture).not.toHaveBeenCalled();
  });

  /**
   * The window prints a refusal in itself, so the request carries the flags
   * that keep the corner quiet; after the server agrees the page the
   * moderator is on is read again, and the record comes back without the
   * picture, its status and "О себе" in place.
   */
  it("takes the picture out of the record and re-reads the page", async () => {
    routeQuery.value = { number: "2" };
    getAllWarnings.mockResolvedValue(page([onProfile()]));
    removeWarningProfilePicture.mockResolvedValue({ data: null, error: null });

    const wrapper = await rendered();
    await actionOf(wrapper, 0).trigger("click");
    await wrapper.get(".confirm-go").trigger("click");
    await flushPromises();

    expect(removeWarningProfilePicture).toHaveBeenCalledWith("w-p", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(getAllWarnings).toHaveBeenCalledTimes(2);
    expect(getAllWarnings).toHaveBeenLastCalledWith({
      skip: PAGE_SIZE,
      take: PAGE_SIZE,
    });
  });

  it("re-reads nothing when the server refuses", async () => {
    getAllWarnings.mockResolvedValue(page([onProfile()]));
    removeWarningProfilePicture.mockResolvedValue({
      data: null,
      error: { status: 403, message: "Нет" },
    });

    const wrapper = await rendered();
    await actionOf(wrapper, 0).trigger("click");
    await wrapper.get(".confirm-go").trigger("click");
    await flushPromises();

    expect(getAllWarnings).toHaveBeenCalledTimes(1);
  });

  it("sends nothing when the question is closed", async () => {
    getAllWarnings.mockResolvedValue(page([onProfile()]));

    const wrapper = await rendered();
    await actionOf(wrapper, 0).trigger("click");
    await wrapper.get(".confirm-cancel").trigger("click");

    expect(wrapper.find(".confirm-stub").exists()).toBe(false);
    expect(removeWarningProfilePicture).not.toHaveBeenCalled();
  });
});
