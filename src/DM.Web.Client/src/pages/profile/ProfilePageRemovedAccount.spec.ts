/**
 * @vitest-environment jsdom
 */

/**
 * The profile of "Аккаунт удален": an ordinary profile page over the sum of
 * every removed account, and nothing that was any one of theirs.
 *
 * The server answers the name at the profile and in the lists its tabs are
 * made of, and treats it as a name nobody holds everywhere else. So the page
 * must not ask those other endpoints at all (each would come back refused),
 * and must not draw what they would fill: the dates, the actions, the
 * recommendations and reviews, the violations, the contacts, "Зал славы".
 * The counters it does draw are sums, and the lists behind their links are
 * not answered for this name, so they are plain numbers.
 *
 * The viewer is an administrator, the one who sees the most on anybody's
 * profile: the moderation panel, the extra links, every action. A control
 * render of an ordinary profile in front of the same viewer shows that the
 * absences below come from the name and not from a mock that answers nothing.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
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
import { moderationApi } from "@/entities/moderation";
import { supportFor } from "@/entities/ticket";
import { useSubscriptionsStore } from "@/entities/subscription";
import { UserSubscribeButton } from "@/features/user-subscribe";
import { StatLine } from "@/shared/ui/StatLine";
import { Api } from "@/shared/api";
import ProfilePage from "./ProfilePage.vue";
import ProfileAbout from "./ProfileAbout.vue";
import ProfileRemovedAccountAbout from "./ProfileRemovedAccountAbout.vue";
import ProfilePersonalInfo from "./ProfilePersonalInfo.vue";
import ProfileViolations from "./ProfileViolations.vue";
import ProfileAchievements from "./ProfileAchievements.vue";
import ProfileModerationTab from "./moderation/ProfileModerationTab.vue";

const { route } = vi.hoisted(() => ({
  route: {
    params: { username: "Аккаунт удален" },
    query: {} as Record<string, string>,
  },
}));

vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => route,
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

const ok = (data: unknown = null) => ({ data, error: null }) as never;

/**
 * The summary the server answers the name with (RemovedAccountProfileShould):
 * a zero id, no history, nothing personal, and the summed counters.
 */
const removedAccountProfile = {
  id: "00000000-0000-0000-0000-000000000000",
  username: "Аккаунт удален",
  role: UserRole.RegularUser,
  usernameHistory: [],
  picture: null,
  status: null,
  registeredUtc: null,
  lastActivityUtc: null,
  rating: { postReviewScoreSum: 12, totalPosts: 40 },
  reviewsGiven: 5,
  reviewsReceived: 9,
  postReviewsGiven: 5,
  postReviewsReceived: 9,
  gamesHosting: 2,
  gamesPlaying: 1,
  blogsHosting: 1,
};

/** An ordinary profile with the same counters, for the control render. */
const ordinaryProfile = {
  ...removedAccountProfile,
  id: "other-id",
  username: "Other",
  registeredUtc: "2020-01-01T00:00:00Z",
  lastActivityUtc: "2026-09-01T00:00:00Z",
  endorsementsReceived: 1,
  endorsementsGiven: 1,
  gameReviewsReceived: 1,
  gameReviewsGiven: 1,
  // What the reader may do with this user, as the profile answers it.
  viewer: { canMessage: true },
};

/** Whether a read went to the reader's notes. */
function askedForNote(get: { mock: { calls: unknown[][] } }): boolean {
  return get.mock.calls.some(([url]) =>
    String(url).includes("users/me/notes/"),
  );
}

function spyOnRequests() {
  return {
    eligibility: vi
      .spyOn(userApi, "getEndorsementEligibility")
      .mockResolvedValue(ok({ canCreate: true })),
    blacklist: vi
      .spyOn(blacklistApi, "getBlacklist")
      .mockResolvedValue(ok({ resources: [] })),
    // Every read, to tell whether one went to the reader's notes.
    reads: vi.spyOn(Api, "get"),
    moderation: vi
      .spyOn(moderationApi, "getModeratedProfile")
      .mockResolvedValue(ok()),
    usernameChange: vi
      .spyOn(accountApi, "getUsernameChangeRequest")
      .mockResolvedValue(ok()),
  };
}

/**
 * The profile named `username`, open in front of a signed-in administrator;
 * `counters` change the summary the server answers.
 */
async function render(
  username: string,
  counters: object = {},
  moderated: object | null = null,
  /** The moderated profile's answer, held back to arrive when the test says. */
  moderationAnswer?: Promise<unknown>,
) {
  route.params.username = username;
  const pinia = createPinia();
  setActivePinia(pinia);

  useAuthStore().user = {
    id: "admin-id",
    username: "Admin",
    role: UserRole.Admin,
  } as never;

  const requests = spyOnRequests();
  if (moderated) requests.moderation.mockResolvedValue(ok(moderated));
  if (moderationAnswer) {
    requests.moderation.mockReturnValue(moderationAnswer as never);
  }
  const community = useCommunityStore();
  vi.spyOn(community, "trySelectProfile").mockImplementation(async () => {
    community.selectedUser = (
      username === "Other"
        ? ordinaryProfile
        : { ...removedAccountProfile, ...counters }
    ) as never;
    return null;
  });
  const subscriptions = vi
    .spyOn(useSubscriptionsStore(), "fetchSubscriptions")
    .mockResolvedValue(null as never);

  const wrapper = shallowMount(ProfilePage, {
    global: {
      plugins: [pinia],
      // The tab strip and the buttons are real: what they say is read.
      // The row of the actions is drawn for real, or the buttons in it are
      // not.
      stubs: {
        Tabs: false,
        ButtonRow: false,
        Button: false,
        RouterLink: RouterLinkStub,
      },
    },
  });
  await flushPromises();
  return { wrapper, requests: { ...requests, subscriptions } };
}

const tabs = (wrapper: VueWrapper) =>
  wrapper.findAll('[role="tab"]').map((tab) => tab.text());

const links = (wrapper: VueWrapper) =>
  wrapper.findAllComponents(RouterLinkStub).map((link) => link.text());

const buttons = (wrapper: VueWrapper) =>
  wrapper.findAll("button").map((button) => button.text());

/** Every stat line, as its label and where its number leads. */
const statLines = (wrapper: VueWrapper) =>
  wrapper.findAllComponents(StatLine).map((line) => ({
    label: line.props("label"),
    to: line.props("to"),
  }));

afterEach(() => {
  vi.restoreAllMocks();
  route.query = {};
});

describe("the profile of an ordinary user, in front of an administrator", () => {
  it("asks what the page draws and draws all of it", async () => {
    const { wrapper, requests } = await render("Other");

    expect(requests.eligibility).toHaveBeenCalled();
    // Whether the reader's list holds this user comes with the profile.
    expect(requests.blacklist).not.toHaveBeenCalled();
    // So does the reader's note about him.
    expect(askedForNote(requests.reads)).toBe(false);
    expect(requests.moderation).toHaveBeenCalled();
    expect(requests.subscriptions).toHaveBeenCalled();
    expect(tabs(wrapper)).toContain("Зал славы");
    expect(buttons(wrapper)).toContain("В черный список");
    expect(links(wrapper)).toContain("Написать сообщение");
    expect(links(wrapper)).toContain("Загруженное");
    expect(wrapper.findComponent(ProfileViolations).exists()).toBe(true);
    expect(wrapper.findComponent(ProfilePersonalInfo).exists()).toBe(true);
    expect(wrapper.findComponent(UserSubscribeButton).exists()).toBe(true);
  });
});

/**
 * Moderation is the last tab of the strip, drawn over the moderated profile the
 * server serves: the red bar it replaced stood between the actions and the
 * contacts. No answer, no tab, as the control render above shows for an answer
 * of nothing.
 */
describe("the moderation tab of an ordinary profile", () => {
  it("is the last tab, once the server serves the moderated profile", async () => {
    const { wrapper } = await render(
      "Other",
      {},
      {
        username: "Other",
        permissions: {},
        violations: {},
      },
    );

    expect(tabs(wrapper).at(-1)).toBe("Модерация");
  });

  it("is not there when the server serves nothing", async () => {
    const { wrapper } = await render("Other");

    expect(tabs(wrapper)).not.toContain("Модерация");
  });

  // A page of moderation can send a moderator straight into the tab.
  /** The moderated profile's answer, held until the profile is on screen. */
  function heldAnswer() {
    let give!: (value: unknown) => void;
    const answer = new Promise((resolve) => (give = resolve));
    return { answer, give };
  }

  // A page of moderation can send a moderator straight into the tab, and the
  // tab waits for the answer that decides whether it exists: the profile is
  // on screen first, and the tab must not be left while the answer is out.
  it("opens on the tab when asked to, waiting for the moderated profile", async () => {
    route.query = { tab: "moderation" };
    const { answer, give } = heldAnswer();
    const { wrapper } = await render("Other", {}, null, answer);

    give(ok({ username: "Other", permissions: {}, violations: {} }));
    await flushPromises();

    expect(tabs(wrapper)[0]).toBe("Модерация");
    expect(wrapper.findComponent(ProfileModerationTab).exists()).toBe(true);
  });

  // And leaves it once the answer is none, though no tab changes then.
  it("falls back to О себе once the server serves nothing", async () => {
    route.query = { tab: "moderation" };
    const { answer, give } = heldAnswer();
    const { wrapper } = await render("Other", {}, null, answer);

    give(ok());
    await flushPromises();

    expect(wrapper.get('[role="tab"][aria-selected="true"]').text()).toBe(
      "О себе",
    );
    expect(wrapper.findComponent(ProfileAbout).exists()).toBe(true);
  });
});

describe("the profile of Аккаунт удален", () => {
  it("asks nothing the server answers as a name nobody holds", async () => {
    const { requests } = await render("Аккаунт удален");

    expect(requests.eligibility).not.toHaveBeenCalled();
    expect(requests.blacklist).not.toHaveBeenCalled();
    expect(askedForNote(requests.reads)).toBe(false);
    expect(requests.moderation).not.toHaveBeenCalled();
    expect(requests.subscriptions).not.toHaveBeenCalled();
  });

  it("takes the name the way a reader types it", async () => {
    const { requests } = await render("  АККАУНТ Удал\u0451н ");

    expect(requests.blacklist).not.toHaveBeenCalled();
    expect(requests.moderation).not.toHaveBeenCalled();
  });

  it("draws no actions, no extra links and no moderation panel", async () => {
    const { wrapper } = await render("Аккаунт удален");

    expect(buttons(wrapper)).not.toContain("В черный список");
    expect(buttons(wrapper)).not.toContain("Убрать из черного списка");
    expect(links(wrapper)).not.toContain("Написать сообщение");
    expect(links(wrapper)).not.toContain("Написать рекомендацию");
    expect(links(wrapper)).not.toContain("Загруженное");
    expect(wrapper.findComponent(UserSubscribeButton).exists()).toBe(false);
    expect(wrapper.find(".extra-links").exists()).toBe(false);
    expect(tabs(wrapper)).not.toContain("Модерация");
  });

  it("draws no violations and no contacts", async () => {
    const { wrapper } = await render("Аккаунт удален");

    expect(wrapper.findComponent(ProfileViolations).exists()).toBe(false);
    expect(wrapper.findComponent(ProfilePersonalInfo).exists()).toBe(false);
  });

  it("has no Зал славы", async () => {
    const { wrapper } = await render("Аккаунт удален");

    expect(tabs(wrapper)).toEqual(["О себе", "Игры", "Блоги", "Топики"]);
  });

  // With no games and no blogs the strip never changes after the load, so
  // nothing but the page's first choice keeps it off the tab it lacks.
  it("does not open on Зал славы when asked to", async () => {
    route.query = { tab: "achievements" };
    const { wrapper } = await render("Аккаунт удален", {
      gamesHosting: 0,
      gamesPlaying: 0,
      blogsHosting: 0,
    });

    expect(tabs(wrapper)).toEqual(["О себе", "Топики"]);
    expect(wrapper.findComponent(ProfileAchievements).exists()).toBe(false);
    expect(wrapper.findComponent(ProfileRemovedAccountAbout).exists()).toBe(
      true,
    );
  });

  it("counts without links, and without dates, recommendations or reviews", async () => {
    const { wrapper } = await render("Аккаунт удален");

    expect(statLines(wrapper)).toEqual([
      { label: "Рейтинг", to: undefined },
      { label: "Оценено чужих постов", to: undefined },
      { label: "Написано постов", to: undefined },
    ]);
  });

  it("says in О себе what the name gathers, with no note and no text of anyone's", async () => {
    const { wrapper } = await render("Аккаунт удален");

    expect(wrapper.findComponent(ProfileRemovedAccountAbout).exists()).toBe(
      true,
    );
    expect(wrapper.findComponent(ProfileAbout).exists()).toBe(false);
    expect(wrapper.find(".profile-note").exists()).toBe(false);

    const about = shallowMount(ProfileRemovedAccountAbout, {
      global: { stubs: { RouterLink: RouterLinkStub } },
    });
    expect(about.text()).toContain(
      "Здесь собрано все, что оставили удаленные аккаунты",
    );
    const support = about.getComponent(RouterLinkStub);
    expect(support.text()).toBe("поддержку");
    expect(support.props("to")).toEqual(supportFor("AccountRestoration"));
  });
});
