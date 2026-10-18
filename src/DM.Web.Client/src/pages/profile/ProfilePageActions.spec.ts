/**
 * @vitest-environment jsdom
 */

/**
 * Two actions on somebody else's profile, and which of them asks first.
 *
 * The blacklist does not ask. Either step is taken back by the same button,
 * which turns into its opposite as soon as the server answers, so a window in
 * front of it would ask about something that costs one more press to undo.
 * Owner's call, 22 September 2026.
 *
 * The personal note does ask before it goes, because a deletion always does
 * (the owner's rule). Saving the note emptied is a deletion too - the server keeps no
 * empty note - and it used to take the note away with no question at all.
 *
 * The page is mounted shallow: every section around these two is a stub, and
 * only the button, the confirmation and the requests behind them are real.
 * Reaching them takes a loaded profile, so the load is answered in place and
 * every read the page fires on mount is answered with nothing.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  flushPromises,
  RouterLinkStub,
  shallowMount,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  accountApi,
  blacklistApi,
  useAuthStore,
  useCommunityStore,
  userApi,
  UserRole,
} from "@/entities/user";
import { useSubscriptionsStore } from "@/entities/subscription";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import { Api } from "@/shared/api";
import ProfilePage from "./ProfilePage.vue";

const { route } = vi.hoisted(() => ({
  route: { params: { username: "Other" }, query: {} },
}));

vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => route,
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const FLAGS = { ownsRefusal: true, ownsRateLimit: true };

const ok = (data: unknown = null) => ({ data, error: null }) as never;
const refused = () =>
  ({
    data: null,
    error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
  }) as never;

/**
 * The profile of somebody else, open in front of a signed-in reader, or with
 * `own` the reader's own page.
 */
async function render(options: {
  blocked?: boolean;
  note?: string | null;
  own?: boolean;
  /** The server lets the reader write a recommendation. */
  endorsable?: boolean;
  /**
   * The profile says the reader may write to this user in private; yes unless
   * a test says otherwise.
   */
  messageable?: boolean;
}) {
  const shown = options.own ? "Me" : "Other";
  route.params.username = shown;
  const pinia = createPinia();
  setActivePinia(pinia);

  useAuthStore().user = {
    id: "me-id",
    username: "Me",
    role: UserRole.RegularUser,
  } as never;

  const community = useCommunityStore();
  vi.spyOn(community, "trySelectProfile").mockImplementation(async () => {
    community.selectedUser = {
      id: options.own ? "me-id" : "other-id",
      username: shown,
      role: UserRole.RegularUser,
      // The profile says whether the reader's own list holds this user.
      blockedByViewer: options.blocked === true,
      // ...and what the reader may do with him.
      viewer: { canMessage: options.messageable !== false },
      // ...and the note the reader keeps about him, when there is one.
      personalNote: options.note
        ? {
            id: "note-id",
            text: options.note,
            createdUtc: "2026-09-01T00:00:00Z",
          }
        : undefined,
    } as never;
    return null;
  });
  vi.spyOn(useSubscriptionsStore(), "fetchSubscriptions").mockResolvedValue(
    null as never,
  );

  vi.spyOn(userApi, "getEndorsementEligibility").mockResolvedValue(
    ok(options.endorsable ? { resource: { canCreate: true } } : null),
  );
  // The reader's list, which the page must not read for this: it answers
  // with its first page, and there the name is never found.
  const list = vi
    .spyOn(blacklistApi, "getBlacklist")
    .mockResolvedValue(ok({ resources: [] }));
  // Every read the page makes, to tell whether it asked the notes endpoint:
  // the server answers it 404 for a reader who has written nothing, and the
  // page has no business asking it, the profile says.
  const noteRequest = vi.spyOn(Api, "get");
  // The own page asks whether a change of name is pending.
  vi.spyOn(accountApi, "getUsernameChangeRequest").mockResolvedValue(ok());

  const wrapper = shallowMount(ProfilePage, {
    global: {
      plugins: [pinia],
      // The confirmation is the real window, drawn in place. The tab strip is
      // real too: the panel under it takes its id from the strip's methods.
      // So are the caption line of the note and the actions in it.
      stubs: {
        Teleport: true,
        Button: false,
        ConfirmDialog: false,
        Tabs: false,
        // The row the actions stand in, drawn for real: a stub would not
        // draw what stands in it.
        ButtonRow: false,
        CaptionLine: false,
        CaptionAction: false,
        // The refusal line under a press, drawn for real: its words are read.
        ActionResult: false,
        RouterLink: RouterLinkStub,
      },
    },
  });
  await flushPromises();
  return Object.assign(wrapper, { list, noteRequest });
}

/** Whether a read went to the reader's notes. */
function askedForNote(get: { mock: { calls: unknown[][] } }): boolean {
  return get.mock.calls.some(([url]) =>
    String(url).includes("users/me/notes/"),
  );
}

/** The button of the page that reads `label`, which must be there. */
function button(wrapper: VueWrapper, label: string) {
  const found = wrapper.findAll("button").find((b) => b.text() === label);
  if (!found) throw new Error(`no button reads "${label}"`);
  return found;
}

const hasButton = (wrapper: VueWrapper, label: string) =>
  wrapper.findAll("button").some((b) => b.text() === label);

const confirmation = (wrapper: VueWrapper) => wrapper.find(".dialog-container");

beforeEach(() => {
  [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
});

afterEach(() => {
  vi.restoreAllMocks();
});

/**
 * The account settings are reached from the reader's own page and from nowhere
 * else (owner, 27.09): the header no longer carries them. They stand in the
 * actions row, where somebody else's page has its actions, as a link.
 */
describe("ProfilePage settings link", () => {
  const settings = (wrapper: VueWrapper) =>
    wrapper
      .findAllComponents(RouterLinkStub)
      .filter((link) => link.text() === "Настройки");

  it("leads the owner from the actions row to the account settings", async () => {
    const wrapper = await render({ own: true });

    const [link] = settings(wrapper);
    expect(link.props("to")).toEqual({ name: "account" });
    expect(link.element.closest(".actions")).not.toBeNull();
  });

  it("is not there on somebody else's page", async () => {
    const wrapper = await render({});

    expect(settings(wrapper)).toHaveLength(0);
  });
});

/**
 * The row of actions on somebody else's page. The subscription stands second
 * on every profile: the recommendation comes and goes with the server's answer,
 * and in front of the subscription it moved it between the second and the third
 * place from one profile to the next. Nothing held the order before.
 */
describe("ProfilePage row of actions", () => {
  /** The row as it reads, the subscribe control named by what it is. */
  const row = (wrapper: VueWrapper) =>
    Array.from(wrapper.get(".actions").element.children).map((child) =>
      child.tagName.toLowerCase().startsWith("user-subscribe")
        ? "[подписка]"
        : (child.textContent ?? "").trim(),
    );

  it("puts the subscription second and the recommendation after it", async () => {
    const wrapper = await render({ endorsable: true });

    expect(row(wrapper)).toEqual([
      "Написать сообщение",
      "[подписка]",
      "Написать рекомендацию",
      "В черный список",
    ]);
  });

  it("keeps the subscription second where there is no recommendation to write", async () => {
    const wrapper = await render({});

    expect(row(wrapper)).toEqual([
      "Написать сообщение",
      "[подписка]",
      "В черный список",
    ]);
  });
});

/**
 * "Написать сообщение" is offered only where the server says the message would
 * be taken (owner, 05.10: what may not be done is not offered). The profile
 * answers it as `viewer.canMessage`, asked by the code that refuses the message.
 * A reader kept on this user's blacklist with private messages closed used to
 * be offered the button and told after the message that it could not be sent.
 */
describe("ProfilePage message to the user", () => {
  const row = (wrapper: VueWrapper) =>
    Array.from(wrapper.get(".actions").element.children).map((child) =>
      child.tagName.toLowerCase().startsWith("user-subscribe")
        ? "[подписка]"
        : (child.textContent ?? "").trim(),
    );

  const messageLinks = (wrapper: VueWrapper) =>
    wrapper
      .findAllComponents(RouterLinkStub)
      .filter((link) => link.text() === "Написать сообщение");

  it("leads to the correspondence with the user where the message is taken", async () => {
    const wrapper = await render({});

    const [link] = messageLinks(wrapper);
    expect(link.props("to")).toEqual({
      name: "direct-message",
      params: { username: "Other" },
    });
  });

  it("is not offered where the profile says the message would be refused", async () => {
    const wrapper = await render({ messageable: false, endorsable: true });

    expect(messageLinks(wrapper)).toHaveLength(0);
    expect(row(wrapper)).toEqual([
      "[подписка]",
      "Написать рекомендацию",
      "В черный список",
    ]);
  });

  /**
   * The answer is for whoever held the session when the profile was read. A
   * guest who signs in on the page, or a reader who signs out, gets it read
   * again for himself.
   */
  it("reads the profile again for a reader who signs in or out on it", async () => {
    const options = { messageable: false };
    const wrapper = await render(options);
    const load = vi.mocked(useCommunityStore().trySelectProfile);
    expect(load).toHaveBeenCalledTimes(1);
    expect(messageLinks(wrapper)).toHaveLength(0);

    options.messageable = true;
    useAuthStore().user = {
      id: "someone-id",
      username: "Someone",
      role: UserRole.RegularUser,
    } as never;
    await flushPromises();

    expect(load).toHaveBeenCalledTimes(2);
    expect(messageLinks(wrapper)).toHaveLength(1);

    useAuthStore().user = null;
    await flushPromises();

    expect(load).toHaveBeenCalledTimes(3);
  });
});

/**
 * The row of actions and the strip of the tabs stand whole at every width, as
 * on the full version: nothing in "…", nothing scrolled sideways. The actions
 * stand in one line (ButtonRow), every one of them drawn. The wave of 5
 * October kept the row one line by sending what did not fit into a square
 * "…", and the strip by sending its tabs into "ЕЩЕ", so a phone showed one
 * action of three and three tabs of five.
 */
describe("ProfilePage rows whole", () => {
  /** The words of the row's items, none of them held or put away. */
  const items = (wrapper: VueWrapper) =>
    Array.from(wrapper.get(".actions").element.children);

  const fitMarks = (el: Element) =>
    el.getAttributeNames().filter((name) => name.startsWith("data-fit-"));

  it("stands every action of a reader in a row of buttons", async () => {
    const wrapper = await render({ endorsable: true });
    const row = wrapper.get(".actions");

    expect(row.classes()).toContain("button-row");
    expect(fitMarks(row.element)).toEqual([]);
    expect(items(wrapper).flatMap(fitMarks)).toEqual([]);
    expect(items(wrapper).some((el) => (el as HTMLElement).hidden)).toBe(false);
    expect(items(wrapper)).toHaveLength(4);
    expect(row.text()).not.toMatch(/…|Еще/);
  });

  it("stands the owner's actions in the same row, none held", async () => {
    const wrapper = await render({ own: true });

    expect(wrapper.get(".actions").classes()).toContain("button-row");
    expect(items(wrapper).map((el) => (el.textContent ?? "").trim())).toEqual([
      "Редактировать",
      "Настройки",
    ]);
    expect(items(wrapper).flatMap(fitMarks)).toEqual([]);
  });

  it("scrolls neither the actions nor the tabs sideways, and keeps every tab", async () => {
    const wrapper = await render({});

    expect(wrapper.find(".scroll-row").exists()).toBe(false);
    expect(wrapper.find(".more-menu").exists()).toBe(false);
    // The strip is the section's line itself, not a strip inside a scroller.
    const strip = wrapper.get(".info-section");
    expect(strip.classes()).toContain("tabs");
    expect(strip.findAll('[role="tab"]').map((tab) => tab.text())).toEqual([
      "О себе",
      "Топики",
      "Зал славы",
    ]);
    expect(strip.text()).not.toMatch(/ЕЩЕ|Еще|…/);
  });
});

/**
 * The reader's own subscriptions had no link on the site at all. They are the
 * first of the extra links of the reader's own page, and not on anybody else's:
 * the page lists the subscriptions of the one who opens it.
 */
/** "Модерация" is a moderator's tab, and an address does not open it for others. */
describe("ProfilePage asked for the moderation tab", () => {
  afterEach(() => {
    delete (route.query as Record<string, string>).tab;
  });

  it("opens on О себе for a reader who is not a moderator", async () => {
    (route.query as Record<string, string>).tab = "moderation";
    const wrapper = await render({});

    const tabs = wrapper.findAll('[role="tab"]').map((tab) => tab.text());
    expect(tabs).not.toContain("Модерация");
    expect(wrapper.get('[role="tab"][aria-selected="true"]').text()).toBe(
      "О себе",
    );
  });
});

describe("ProfilePage link to the subscriptions", () => {
  const extraLinks = (wrapper: VueWrapper): string[] => {
    const block = wrapper.find(".extra-links");
    if (!block.exists()) return [];
    return block.findAll("a").map((link) => link.text());
  };

  it("is the first of the extra links on the reader's own page", async () => {
    const wrapper = await render({ own: true });

    expect(extraLinks(wrapper)[0]).toBe("Подписки");
    const link = wrapper
      .findAllComponents(RouterLinkStub)
      .find((l) => l.text() === "Подписки")!;
    expect(link.props("to")).toEqual({ name: "subscriptions" });
  });

  it("is not on somebody else's page", async () => {
    const wrapper = await render({});

    expect(extraLinks(wrapper)).not.toContain("Подписки");
  });
});

describe("ProfilePage blacklist button", () => {
  /**
   * The state comes from the profile (blockedByViewer), not from the reader's
   * list: the list answers a page at a time, and to somebody with a longer one
   * the button offered to block a person already blocked.
   */
  it("reads its state from the profile, not from the reader's list", async () => {
    const wrapper = await render({ blocked: true });

    expect(hasButton(wrapper, "Убрать из черного списка")).toBe(true);
    expect(hasButton(wrapper, "В черный список")).toBe(false);
    expect(wrapper.list).not.toHaveBeenCalled();
  });

  it("blocks at once and turns into the way back", async () => {
    const block = vi.spyOn(blacklistApi, "blockUser").mockResolvedValue(ok());
    const wrapper = await render({ blocked: false });

    await button(wrapper, "В черный список").trigger("click");
    await flushPromises();

    expect(block).toHaveBeenCalledWith({ username: "Other" });
    expect(confirmation(wrapper).exists()).toBe(false);
    expect(hasButton(wrapper, "Убрать из черного списка")).toBe(true);
    expect(hasButton(wrapper, "В черный список")).toBe(false);
    expect(saidOutLoud()).toEqual([]);
  });

  it("unblocks at once, asking nothing, and turns back", async () => {
    const unblock = vi
      .spyOn(blacklistApi, "unblockUser")
      .mockResolvedValue(ok());
    const wrapper = await render({ blocked: true });

    await button(wrapper, "Убрать из черного списка").trigger("click");
    await flushPromises();

    expect(unblock).toHaveBeenCalledWith("Other");
    expect(confirmation(wrapper).exists()).toBe(false);
    expect(hasButton(wrapper, "В черный список")).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps its word after a refusal and says why", async () => {
    vi.spyOn(blacklistApi, "blockUser").mockResolvedValue(refused());
    const wrapper = await render({ blocked: false });

    await button(wrapper, "В черный список").trigger("click");
    await flushPromises();

    expect(hasButton(wrapper, "В черный список")).toBe(true);
    // Under the row of buttons of the profile; no plate in the corner.
    expect(wrapper.get(".actions + .action-result").text()).toBe(
      "Пользователь не добавлен в черный список",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

describe("ProfilePage personal note", () => {
  // The notes endpoint answers 404 for a reader who has written nothing, the
  // commonest case, and every such profile put an error in the console while
  // the profile it had just read already carried the note or its absence.
  it("takes the note from the profile and asks the notes endpoint nothing", async () => {
    const wrapper = await render({ note: "Хороший мастер" });

    expect(wrapper.get(".note-text").text()).toBe("Хороший мастер");
    expect(askedForNote(wrapper.noteRequest)).toBe(false);
  });

  it("says there is no note when the profile carries none", async () => {
    const wrapper = await render({});

    expect(wrapper.find(".note-empty").exists()).toBe(true);
    expect(askedForNote(wrapper.noteRequest)).toBe(false);
  });

  it("asks before the note is deleted", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Удалить").trigger("click");

    expect(confirmation(wrapper).find(".dialog-title").text()).toBe(
      "Удалить заметку?",
    );
    expect(remove).not.toHaveBeenCalled();

    await confirmation(wrapper).get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(remove).toHaveBeenCalledWith("Other", FLAGS);
    expect(confirmation(wrapper).exists()).toBe(false);
    expect(wrapper.find(".note-empty").exists()).toBe(true);
  });

  it("keeps the note when the question is turned down", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Удалить").trigger("click");
    await confirmation(wrapper).get(".dialog-btn-cancel").trigger("click");

    expect(remove).not.toHaveBeenCalled();
    expect(confirmation(wrapper).exists()).toBe(false);
    expect(wrapper.get(".note-text").text()).toBe("Хороший мастер");
  });

  it("prints a refused delete in the window and keeps the note", async () => {
    vi.spyOn(userApi, "deleteUserProfileNote").mockResolvedValue(refused());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Удалить").trigger("click");
    await confirmation(wrapper).get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(confirmation(wrapper).get(".dialog-refusal").text()).toBe(
      "Не удалось удалить заметку",
    );
    expect(wrapper.get(".note-text").text()).toBe("Хороший мастер");
    expect(saidOutLoud()).toEqual([]);
  });

  // The editor used to close on any answer, so a refused note read as a saved
  // one, and the refusal went to a plate or nowhere. It stays open with the
  // text in it and says why under its buttons.
  it("keeps the editor open with the text when a save is refused, and says why under it", async () => {
    vi.spyOn(userApi, "upsertUserProfileNote").mockResolvedValue(refused());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Редактировать").trigger("click");
    wrapper
      .findComponent(BBCodeEditor)
      .vm.$emit("update:modelValue", "Очень хороший мастер");
    await button(wrapper, "Сохранить").trigger("click");
    await flushPromises();

    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(true);
    expect(wrapper.get(".note-actions + .action-result").text()).toBe(
      "Не удалось сохранить заметку",
    );
    expect(saidOutLoud()).toEqual([]);
  });

  it("asks the same question when the note is saved emptied", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const upsert = vi
      .spyOn(userApi, "upsertUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Редактировать").trigger("click");
    wrapper.findComponent(BBCodeEditor).vm.$emit("update:modelValue", "  ");
    await button(wrapper, "Сохранить").trigger("click");

    expect(confirmation(wrapper).find(".dialog-title").text()).toBe(
      "Удалить заметку?",
    );
    expect(remove).not.toHaveBeenCalled();
    expect(upsert).not.toHaveBeenCalled();

    await confirmation(wrapper).get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(remove).toHaveBeenCalledWith("Other", FLAGS);
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
    expect(wrapper.find(".note-empty").exists()).toBe(true);
  });

  it("leaves the editor open with the emptied text when the question is turned down", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: "Хороший мастер" });

    await button(wrapper, "Редактировать").trigger("click");
    wrapper.findComponent(BBCodeEditor).vm.$emit("update:modelValue", "");
    await button(wrapper, "Сохранить").trigger("click");
    await confirmation(wrapper).get(".dialog-btn-cancel").trigger("click");

    expect(remove).not.toHaveBeenCalled();
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(true);
  });

  it("closes an empty editor quietly when there is no note to lose", async () => {
    const remove = vi
      .spyOn(userApi, "deleteUserProfileNote")
      .mockResolvedValue(ok());
    const upsert = vi
      .spyOn(userApi, "upsertUserProfileNote")
      .mockResolvedValue(ok());
    const wrapper = await render({ note: null });

    await button(wrapper, "Добавить").trigger("click");
    await button(wrapper, "Сохранить").trigger("click");
    await flushPromises();

    expect(confirmation(wrapper).exists()).toBe(false);
    expect(remove).not.toHaveBeenCalled();
    expect(upsert).not.toHaveBeenCalled();
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
  });
});
