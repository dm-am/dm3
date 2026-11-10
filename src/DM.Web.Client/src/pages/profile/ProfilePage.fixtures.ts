/**
 * The profile of somebody else in front of a signed-in reader, for the specs
 * of the actions and of the note, on the real router.
 */
import { vi } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import {
  accountApi,
  blacklistApi,
  useAuthStore,
  useCommunityStore,
  userApi,
  personalApi,
  UserRole,
} from "@/entities/user";
import { useSubscriptionsStore } from "@/entities/subscription";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import { Api } from "@/shared/api";
import ProfilePage from "./ProfilePage.vue";

const toast = useToast();

/** Everything the site is currently saying in a floating message. */
export const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** The flags a request that keeps its own refusal is sent with. */
export const FLAGS = { ownsRefusal: true, ownsRateLimit: true };

export const ok = (data: unknown = null) => ({ data, error: null }) as never;
export const refused = () =>
  ({
    data: null,
    error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
  }) as never;

interface RenderOptions {
  blocked?: boolean;
  note?: string | null;
  own?: boolean;
  /** The name as the address spells it, when not as the profile does. */
  address?: string;
  /** The query of the address, with its question mark. */
  query?: string;
  /** "О себе" as the page reads it, and as the server gives it to its author. */
  about?: { display: string; source: string };
  /** The server lets the reader write a recommendation. */
  endorsable?: boolean;
  /**
   * The profile says the reader may write to this user in private; yes unless
   * a test says otherwise.
   */
  messageable?: boolean;
  /** The owner's own profile as stored, which the edit mode opens on. */
  ownProfile?: Record<string, unknown>;
  /** A request of the owner's for another name waits for a moderator. */
  nameChangePending?: boolean;
}

/** The addresses the page and the parts drawn for real link to. */
const ROUTES: Address[] = [
  ["profile", "/users/:username"],
  ["profile-uploads", "/users/:username/uploads"],
  ["write-endorsement", "/users/:username/write-endorsement"],
  ["direct-message", "/messenger/user/:username"],
  ["subscriptions", "/subscriptions"],
  ["my-tickets", "/my-tickets"],
  ["account", "/account"],
];

/** The parts of the page a spec may draw for real; the rest stands as a stub. */
const PARTS = [
  "AvatarImg",
  "FormField",
  "ProfileSkeleton",
  "ProfilePictureUpload",
  "ProfilePersonalInfo",
  "ProfileUsername",
  "ProfileStats",
  "ProfileViolations",
  "ErrorPage",
  "UserSubscribeButton",
  "BlockTitle",
  "BBCodeEditor",
  "ContentText",
  "SecondaryText",
  "Tooltip",
  "StatLine",
  "ProfileRemovedAccountAbout",
  "ProfileGamesTable",
  "ProfileBlogsTable",
  "ProfileBestPostSection",
  "ProfileBestPublicationSection",
  "ProfileTopicsList",
  "ProfileSubscribersSection",
  "ProfileAchievements",
  "ProfileModerationTab",
  "Tabs",
  "ProfileActions",
  "ProfileTabs",
  "ProfilePersonalNote",
  "ProfileEditBar",
  "ProfileAbout",
  "ButtonRow",
  "Button",
  "CaptionLine",
  "CaptionAction",
  "ConfirmDialog",
  "ActionResult",
];

/**
 * The profile page at the address of `name`, as a shallow mount draws it: the
 * parts in `real` are drawn, every other part of the page is a stub.
 */
export async function mountProfile(
  pinia: Pinia,
  name: string,
  { real, query = "" }: { real: string[]; query?: string },
) {
  const { wrapper, router } = await mountPage(
    ProfilePage,
    `/users/${encodeURIComponent(name)}${query}`,
    {
      routes: ROUTES,
      pinia,
      stubs: {
        Teleport: true,
        ...Object.fromEntries(
          PARTS.map((part) => [part, !real.includes(part)]),
        ),
      },
    },
  );
  await flushPromises();
  return { wrapper, address: () => router.currentRoute.value.fullPath };
}

// The window, the tab strip, the caption line of the note and its actions are real.
const REAL = [
  "Button",
  "ConfirmDialog",
  "Tabs",
  // The parts of the page these tests read, drawn for real.
  "ProfileActions",
  "ProfileTabs",
  "ProfilePersonalNote",
  "ProfileEditBar",
  "ProfileAbout",
  // Real, or the actions in it are not drawn.
  "ButtonRow",
  "CaptionLine",
  "CaptionAction",
  // Real: its words are read.
  "ActionResult",
];

/** The load of the profile, answered in place with the user the test asks for. */
function answerTheProfile(options: RenderOptions, shown: string) {
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
      info: options.about?.display,
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
}

/**
 * The profile of somebody else in front of a signed-in reader, or with `own`
 * his own page: mounted shallow, the load answered in place and every other
 * read the page fires answered with nothing.
 */
export async function render(options: RenderOptions) {
  const shown = options.own ? "Me" : "Other";
  const pinia = createPinia();
  setActivePinia(pinia);

  useAuthStore(pinia).user = {
    id: "me-id",
    username: "Me",
    role: UserRole.RegularUser,
  } as never;

  answerTheProfile(options, shown);
  vi.spyOn(
    useSubscriptionsStore(pinia),
    "fetchSubscriptions",
  ).mockResolvedValue(null as never);

  vi.spyOn(userApi, "getEndorsementEligibility").mockResolvedValue(
    ok(options.endorsable ? { resource: { canCreate: true } } : null),
  );
  // The reader's list, which the page must not read for this.
  const list = vi
    .spyOn(blacklistApi, "getBlacklist")
    .mockResolvedValue(ok({ resources: [] }));
  // Every read, to tell whether one went to the notes endpoint.
  const noteRequest = vi.spyOn(Api, "get");
  // The edit mode asks whether a change of name is pending...
  const nameRequest = vi
    .spyOn(accountApi, "getUsernameChangeRequest")
    .mockResolvedValue(
      ok(options.nameChangePending ? { status: "Pending" } : null),
    );
  // ...and reads the owner's own profile, "О себе" as typed, before it opens.
  const ownRead = vi
    .spyOn(personalApi, "getMyProfileForEdit")
    .mockResolvedValue(
      ok({
        ...(options.ownProfile ?? { visibility: { showBirthday: false } }),
        info: options.about?.source,
      }),
    );
  // A save reads the profile back; it answers with the one on the page.
  vi.spyOn(userApi, "getUserProfile").mockImplementation(async () =>
    ok({ resource: useCommunityStore().selectedUser }),
  );
  // A changed picture reads the session again, for the one in the header:
  // the session answers with the viewer it holds.
  vi.spyOn(personalApi, "getMyProfile").mockImplementation(async () =>
    ok(useAuthStore().user),
  );
  vi.spyOn(personalApi, "getMyPreferences").mockImplementation(async () =>
    ok(useAuthStore().user?.settings),
  );

  const { wrapper, address } = await mountProfile(
    pinia,
    options.address ?? shown,
    { real: REAL, query: options.query },
  );
  return Object.assign(wrapper, {
    address,
    list,
    noteRequest,
    ownRead,
    nameRequest,
  });
}

/** Whether a read went to the reader's notes. */
export function askedForNote(get: { mock: { calls: unknown[][] } }): boolean {
  return get.mock.calls.some(([url]) =>
    String(url).includes("users/me/notes/"),
  );
}

/** The button of the page that reads `label`, which must be there. */
export function button(wrapper: VueWrapper, label: string) {
  const found = wrapper.findAll("button").find((b) => b.text() === label);
  if (!found) throw new Error(`no button reads "${label}"`);
  return found;
}

export const hasButton = (wrapper: VueWrapper, label: string) =>
  wrapper.findAll("button").some((b) => b.text() === label);

/** The window of a confirmation, open on the page. */
export const confirmation = (wrapper: VueWrapper) =>
  wrapper.find(".dialog-container");

/** Takes down every floating message left by the test before. */
export function dismissToasts() {
  [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
}
