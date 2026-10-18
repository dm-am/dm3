<script setup lang="ts">
import {
  DATE_TIME_FORMAT,
  formatDate,
  formatSiteTime,
} from "@/shared/lib/utils/datetime";
import { computed, onMounted, ref, toRef, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQueryRaw } from "vue-router";
import { storeToRefs } from "pinia";
import dayjs from "dayjs";

import {
  useCommunityStore,
  useAuthStore,
  UserRole,
  userIsModerator,
  useProfileEdit,
  userApi,
  blacklistApi,
  accountApi,
  isRemovedAccountName,
  type Username,
  type UsernameHistoryEntry,
} from "@/entities/user";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { useModeratedProfile } from "@/entities/moderation";
import { Gender } from "@/shared/api/models/community";
import type { PersonalNote } from "@/shared/api/models/common";
import { useSubscriptionsStore } from "@/entities/subscription";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { ROLE_INFO, STAFF_ROLES } from "@/shared/config/roles";

import Button from "@/shared/ui/Button/Button.vue";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { UserSubscribeButton } from "@/features/user-subscribe";
import { Tabs, type TabItem } from "@/shared/ui/Tabs";
import { StatLine } from "@/shared/ui/StatLine";
import { FormField } from "@/shared/ui/Form";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ProfileSkeleton } from "@/shared/ui/Skeleton";
import { BlockTitle, PageTitle } from "@/shared/ui/Layout";

import { useEndorsementEligibility } from "./useEndorsementEligibility";
import ProfilePictureUpload from "./ProfilePictureUpload.vue";
import ProfileAbout from "./ProfileAbout.vue";
import ProfileRemovedAccountAbout from "./ProfileRemovedAccountAbout.vue";
import ProfilePersonalInfo from "./ProfilePersonalInfo.vue";
import ProfileGamesTable from "./ProfileGamesTable.vue";
import ProfileBlogsTable from "./ProfileBlogsTable.vue";
import ProfileBestPostSection from "./ProfileBestPostSection.vue";
import ProfileBestPublicationSection from "./ProfileBestPublicationSection.vue";
import ProfileTopicsList from "./ProfileTopicsList.vue";
import ProfileSubscribersSection from "./ProfileSubscribersSection.vue";
import ProfileAchievements from "./ProfileAchievements.vue";
import { SubscriptionSettings } from "@/shared/api/models/subscriptions";
import ProfileViolations from "./ProfileViolations.vue";
import ProfileModerationTab from "./moderation/ProfileModerationTab.vue";
import {
  ErrorPage,
  errorCodeForStatus,
  getErrorConfig,
} from "@/shared/ui/ErrorPage";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ButtonRow } from "@/shared/ui/ButtonRow";

// Tab vocabulary. Each content tab (games / blogs / topics) is the
// home for THREE pieces: the canonical listing, the user's best-of for
// the category, and the subset of subscribers who opted into that
// category's notifications. The free-standing "best-post" and
// "subscribers" tabs from the previous layout are gone — they're now
// inlined into the relevant content tab. "moderation" is the moderated
// profile, for the viewer the server serves it to.
type ProfileTab =
  | "about"
  | "games"
  | "blogs"
  | "topics"
  | "achievements"
  | "moderation";

const route = useRoute();
const router = useRouter();
const communityStore = useCommunityStore();
const subscriptionsStore = useSubscriptionsStore();
const { user: currentUser } = storeToRefs(useAuthStore());
const { selectedUser: user, loadingProfile } = storeToRefs(communityStore);

const usernameParam = computed(() => route.params.username as string);

/**
 * The profile of "Аккаунт удален": the same page over the sum of every removed
 * account, and nothing that was any one of theirs.
 *
 * Decided by the name in the address, not by the answer, so the page asks
 * nothing it would have to throw away: the server answers this name only at
 * the profile and in the lists its tabs are made of, and every other endpoint
 * that takes a name (recommendations, notes, the blacklist, violations,
 * moderation, awards) treats it as a name nobody holds. So those are not
 * asked, and what they would fill is not drawn: the dates, the actions, the
 * recommendations and reviews, the violations, the contacts, "Зал славы".
 */
const isRemovedAccount = computed(() =>
  isRemovedAccountName(usernameParam.value),
);

// HTTP status of the last profile load failure, mapped to an ErrorPage code.
// A true 404 (no such user) and a server error must look different — "не
// найден" against "попробуйте позже" — and the store hands the error over, so
// the status is read from it directly.
const errorCode = ref<number | null>(null);

async function loadProfile(name: Username) {
  errorCode.value = null;
  const error = await communityStore.trySelectProfile(name);
  // The profile endpoint answers an unknown username with 404, which the
  // shared map reads as "не найдено", and the rate limit with its own page.
  if (error) errorCode.value = errorCodeForStatus(error.status);
  else {
    seedNote();
    await loadEndorsementEligibility(name);
  }
}

/**
 * Whether the viewer may write a recommendation about this profile's owner —
 * the server's answer, not a guess assembled here. See
 * useEndorsementEligibility: the create endpoint has five conditions and only
 * two of them are knowable on the client, so the question is asked.
 */
const {
  canCreate: canEndorse,
  ask: askEndorsementEligibility,
  clear: clearEndorsementEligibility,
} = useEndorsementEligibility();

async function loadEndorsementEligibility(name: Username) {
  // The two conditions the page can answer by itself, so a guest browsing
  // profiles and an owner reading their own cost no extra request. Both are
  // refusals on the server too, so skipping the question never widens what is
  // offered. Nobody writes a recommendation to "Аккаунт удален" either.
  if (
    !currentUser.value ||
    currentUser.value.username === name ||
    isRemovedAccountName(name)
  ) {
    clearEndorsementEligibility();
    return;
  }
  await askEndorsementEligibility(name);
}

const writeEndorsementLink = computed(() => ({
  name: "write-endorsement" as const,
  params: { username: usernameParam.value },
}));

useFetchData(
  () => loadProfile(usernameParam.value as Username),
  [
    {
      param: (p) => p.username,
      callback: (name) => loadProfile(name as Username),
    },
  ],
);

/**
 * Whether the viewer may write to this user in private — the server's answer,
 * read with the profile and asked by the code that refuses the message. A
 * reader kept on this user's blacklist with private messages closed used to be
 * offered the button and told after the message that it could not be sent.
 */
const canMessage = computed(() => user.value?.viewer?.canMessage === true);

// The answers the profile carries about the viewer (the message, the
// blacklist button) are for whoever held the session when it was read, and
// the recommendation is asked for him too. Signing in or out on the page does
// not move it, so the page reads the profile again for the new viewer: a guest
// who signs in here is offered the message the way a reload would offer it.
watch(
  () => currentUser.value?.username ?? null,
  () => loadProfile(usernameParam.value as Username),
);

const isSystemUser = computed(() => user.value?.role === UserRole.System);

// Tab title reflects the loaded profile, falling back to the URL param while
// it is still loading so the tab is never blank or stale. When the load fails
// the error owns the title: the param names a user who does not exist, and a
// tab named after him over a 404 page is the page lying about itself. Same
// rule and same source as the forum shell (ForumPage.vue).
useDocumentTitle(() =>
  errorCode.value
    ? getErrorConfig(errorCode.value).title
    : (user.value?.username ?? usernameParam.value),
);

const {
  isEditMode,
  canEdit,
  hasChanges,
  isSaving,
  saveError,
  toggleEditMode,
  setField,
  saveChanges,
  cancelEdit,
} = useProfileEdit(toRef(() => usernameParam.value));

const isOwnProfile = computed(
  () => currentUser.value?.username === usernameParam.value,
);

// "Дополнительные ссылки" block: owner-facing personal pages that have no
// other navigation entry. "Мои обращения" is owner-only; "Загруженное" is
// owner-or-admin (mirrors the ProfileUploadsPage access gate).
const isCurrentUserAdmin = computed(
  () => currentUser.value?.role === UserRole.Admin,
);
const showExtraLinks = computed(
  () =>
    !isRemovedAccount.value && (isOwnProfile.value || isCurrentUserAdmin.value),
);
const uploadsLink = computed(() => ({
  name: "profile-uploads" as const,
  params: { username: usernameParam.value },
}));

function onFieldUpdate(field: string, value: string) {
  switch (field) {
    case "birthday": {
      if (!value) return setField("birthday", null);
      const [y, m, d] = value.split("-").map((s) => parseInt(s, 10));
      if (!y || !m || !d) return;
      return setField("birthday", { day: d, month: m, year: y });
    }
    case "ratingEnabled":
      return setField("visibility", { showRating: value === "true" });
    case "showBirthday":
      return setField("visibility", { showBirthday: value === "true" });
    case "gender":
      return setField("gender", (value || undefined) as Gender);
    case "contacts": {
      try {
        return setField("contacts", JSON.parse(value));
      } catch {
        return;
      }
    }
    default:
      return setField(field as Parameters<typeof setField>[0], value as never);
  }
}

const userRoles = computed<string[]>(() => {
  if (!user.value) return [];
  const all = new Set<UserRole>();
  if (user.value.role) all.add(user.value.role as UserRole);

  const activeStaffRoles = Array.from(all).filter((r) =>
    STAFF_ROLES.includes(r),
  );
  const names = activeStaffRoles
    .map((r) => ROLE_INFO[r].nicknameSingular)
    .filter(Boolean);

  return names;
});

const usernameHistory = computed<UsernameHistoryEntry[]>(
  () => user.value?.usernameHistory ?? [],
);

/**
 * Whether the avatar slot can be reserved from the picture itself.
 *
 * This page draws the original, which keeps the aspect ratio of whatever was
 * uploaded, so its height is not a function of the 220px width. When the API
 * sends the intrinsic pair AvatarImg declares it and the browser reserves the
 * exact box before decoding; when it does not — an upload made before the
 * pipeline recorded one — the declaration falls back to a square and the slot
 * has to hold that square itself, or the picture shrinks the box on decode and
 * pulls the role line, the statistics and the tabs up with it.
 */
const avatarSizeIsKnown = computed(() => {
  const picture = user.value?.picture;
  return !!picture?.originalWidth && !!picture?.originalHeight;
});

const showChangeForm = ref(false);
watch(isEditMode, (editing) => {
  if (!editing) showChangeForm.value = false;
});

// Username-change request: real call to accountApi.createUsernameChangeRequest
// (POST account/username-change), the same endpoint the account settings
// page uses (see pages/account/sections/AccountUsernameChangeSection.vue).
// Reason must be >= 10 chars per that endpoint's contract.
const changeFormReason = ref("");
const isChangeFormSubmitting = ref(false);
const hasPendingUsernameChange = ref(false);

const canSubmitChangeForm = computed(
  () =>
    changeFormReason.value.trim().length >= 10 && !isChangeFormSubmitting.value,
);

async function checkPendingUsernameChange() {
  if (!isOwnProfile.value) return;
  const { data } = await accountApi.getUsernameChangeRequest();
  hasPendingUsernameChange.value = data?.status === "Pending";
}

/** A refused request, under the buttons of its form; the reason stays typed. */
const changeResult = useActionResult();

async function submitUsernameChangeRequest() {
  if (!canSubmitChangeForm.value) return;
  isChangeFormSubmitting.value = true;
  changeResult.clear();
  const { data, error } = await accountApi.createUsernameChangeRequest({
    reason: changeFormReason.value.trim(),
  });
  isChangeFormSubmitting.value = false;
  if (error) {
    changeResult.refuse(error, "Не удалось отправить заявку на смену имени");
    return;
  }
  if (data) {
    hasPendingUsernameChange.value = data.status === "Pending";
    changeFormReason.value = "";
    showChangeForm.value = false;
  }
}

const isOnline = computed(() => {
  const t = user.value?.lastActivityUtc;
  if (!t) return false;
  return dayjs().diff(dayjs(t), "minute", true) <= ONLINE_THRESHOLD_MINUTES;
});

const lastActivityFormatted = computed(() =>
  user.value?.lastActivityUtc
    ? formatSiteTime(user.value.lastActivityUtc, DATE_TIME_FORMAT)
    : "",
);

const registrationDate = computed(() => {
  const value = user.value?.registeredUtc ?? user.value?.registrationUtc;
  return formatDate(value, "");
});

const ratingEnabled = computed(() => user.value?.rating?.isEnabled ?? false);
const reviewsGiven = computed(() => user.value?.reviewsGiven ?? 0);
const endorsementsReceived = computed(
  () => user.value?.endorsementsReceived ?? 0,
);
const endorsementsGiven = computed(() => user.value?.endorsementsGiven ?? 0);
// Reviews of whole games. Not the same datum as `reviewsGiven` above, which
// counts ratings of single posts.
const gameReviewsReceived = computed(
  () => user.value?.gameReviewsReceived ?? 0,
);
const gameReviewsGiven = computed(() => user.value?.gameReviewsGiven ?? 0);

const reviewsGivenLink = computed(() => ({
  name: "given-reviews" as const,
  params: { username: usernameParam.value },
}));

const receivedReviewsLink = computed(() => ({
  name: "received-reviews" as const,
  params: { username: usernameParam.value },
}));

const receivedEndorsementsLink = computed(() => ({
  name: "received-endorsements" as const,
  params: { username: usernameParam.value },
}));

const givenEndorsementsLink = computed(() => ({
  name: "given-endorsements" as const,
  params: { username: usernameParam.value },
}));

const receivedGameReviewsLink = computed(() => ({
  name: "received-game-reviews" as const,
  params: { username: usernameParam.value },
}));

const givenGameReviewsLink = computed(() => ({
  name: "given-game-reviews" as const,
  params: { username: usernameParam.value },
}));

const ratingSum = computed<number | null>(() => {
  const r = user.value?.rating;
  if (!r) return null;
  return r.postReviewScoreSum ?? 0;
});

const ratingSumDisplay = computed<string>(() => {
  const v = ratingSum.value;
  if (v === null) return VALUE_UNAVAILABLE;
  return v > 0 ? `+${v}` : String(v);
});

const ratingSumVariant = computed<"positive" | "negative" | "muted">(() => {
  const v = ratingSum.value;
  if (v === null || v === 0) return "muted";
  return v > 0 ? "positive" : "negative";
});

const gamePostsCount = computed<number>(
  () => user.value?.rating?.totalPosts ?? 0,
);

// Subscribers: passed raw to <ProfileSubscribersSection>, which owns the
// per-tab category filter AND the active/inactive sort (isInactive +
// ordering live there — see ProfileSubscribersSection.vue) so the logic
// has one owner instead of being duplicated here and re-sorted again
// downstream.
const subscribers = computed(() => user.value?.subscribers ?? []);

// The per-category totals. The preview above is capped and activity-ranked
// before the categories are considered, so a line drawn from it can be short or
// empty while the category is full; these say how many there really are.
const subscriberCounts = computed(
  () => user.value?.subscribersByCategory ?? { games: 0, blogs: 0, topics: 0 },
);

// Subscribe / unsubscribe / settings are owned by <UserSubscribeButton>
// — it reads/writes through the subscriptions store directly.

// Whether the viewer's own blacklist holds this user. The profile answers it
// (blockedByViewer). It used to be looked up in the first page of the
// viewer's list, so to somebody with a longer list the button offered to
// block a person already blocked. A press answers it on the spot, until the
// profile is read again. `user` is replaced on every load, navigation from one
// profile to another included, so following it follows the page.
const isBlocked = ref(false);
const isBlockLoading = ref(false);
watch(
  user,
  (loaded) => {
    isBlocked.value = loaded?.blockedByViewer === true;
  },
  { immediate: true },
);

/**
 * Puts this user on the viewer's blacklist, or takes them off it, at once.
 *
 * No window asks first: either step is taken back by the same button, which
 * turns into its opposite, and that turn is the whole answer - no toast.
 * Owner's call, 22 September 2026. A refusal is the button's, so it is said
 * under the row of buttons, the way a refused press is said anywhere on the
 * site.
 */
const blockResult = useActionResult();

async function toggleBlock() {
  if (!user.value || isBlockLoading.value) return;
  const name = user.value.username;
  const blocking = !isBlocked.value;
  isBlockLoading.value = true;
  blockResult.clear();
  const { error } = blocking
    ? await blacklistApi.blockUser({ username: name })
    : await blacklistApi.unblockUser(name);
  isBlockLoading.value = false;
  if (error) {
    blockResult.refuse(
      error,
      blocking
        ? "Пользователь не добавлен в черный список"
        : "Пользователь не убран из черного списка",
    );
    return;
  }
  // The reader may have moved on to another profile while this was on the
  // wire, and the answer is about the one they left.
  if (user.value?.username === name) isBlocked.value = blocking;
}

const note = ref<PersonalNote | null>(null);
const noteEditText = ref("");
const isEditingNote = ref(false);
const isNoteSaving = ref(false);
const noteVisible = computed(
  () => !!currentUser.value && !isOwnProfile.value && !isRemovedAccount.value,
);

// The note comes with the profile, read for whoever holds the session (the
// page reads the profile again when that changes). It used to be asked of the
// notes endpoint as well, which answers 404 when there is no note: the
// commonest case, so nearly every profile put an error in the console for a
// note the profile already said was not there.
function seedNote() {
  note.value = user.value?.personalNote ?? null;
  noteEditText.value = note.value?.text ?? "";
}

function startEditNote() {
  // Re-seed from the stored note so a prior cancelled edit never leaks in.
  noteEditText.value = note.value?.text ?? "";
  isEditingNote.value = true;
}

function cancelEditNote() {
  noteEditText.value = note.value?.text ?? "";
  isEditingNote.value = false;
  noteResult.clear();
}

// A deletion is always asked about (the owner's rule), and saving the note
// emptied is one: the server has no empty note, so that save is a delete in
// other words.
const confirmingNoteDelete = ref(false);

async function saveNote() {
  if (!noteEditText.value.trim()) {
    // With nothing stored there is nothing to lose, and nothing to ask about.
    if (note.value?.text) confirmingNoteDelete.value = true;
    else cancelEditNote();
    return;
  }
  isNoteSaving.value = true;
  noteResult.clear();
  const { data, error } = await userApi.upsertUserProfileNote(
    usernameParam.value as Username,
    noteEditText.value,
  );
  isNoteSaving.value = false;
  // A refusal keeps the editor open with the text in it, and says why under
  // its buttons. The editor used to close on any answer, so a refused note
  // read as a saved one.
  if (error) {
    noteResult.refuse(error, "Не удалось сохранить заметку");
    return;
  }
  if (data) note.value = data;
  isEditingNote.value = false;
}

/** A refused note, under the buttons of the editor. */
const noteResult = useActionResult();

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function deleteNote() {
  const result = await userApi.deleteUserProfileNote(
    usernameParam.value as Username,
    { ownsRefusal: true, ownsRateLimit: true },
  );
  if (result.error) return result;
  note.value = null;
  noteEditText.value = "";
  isEditingNote.value = false;
  return result;
}

// An empty name asks nothing: "Аккаунт удален" has no account to moderate.
// The moderated profile is served to moderators alone, and its answer is what
// draws the "Модерация" tab: no answer, no tab.
const {
  moderatedProfile,
  loading: moderationLoading,
  refresh: refreshModeration,
} = useModeratedProfile(() =>
  isRemovedAccount.value ? "" : usernameParam.value,
);

const DEFAULT_TAB: ProfileTab = "about";

/** Every tab, so a name arriving from outside can be checked against them. */
const TABS: readonly ProfileTab[] = [
  "about",
  "games",
  "blogs",
  "topics",
  "achievements",
  "moderation",
];

/**
 * The tab the profile opens on.
 *
 * Switching tabs still writes nothing to the URL, and F5 on a profile a
 * reader has been clicking around lands on "О себе" as before. What `?tab=`
 * adds is the other direction only: a page that sends a reader here because
 * the answer to what they just did lives in one of these lists can name that
 * list. Deleting a blog is that case - the author leaves the settings of a
 * blog that no longer exists, and the only screen that shows the deletion is
 * their own list of blogs. The key is dropped by the first tab switch
 * (onTabChange keeps only the keys of the tab being opened), so it never
 * becomes a second, stale answer to "which tab is open".
 *
 * Legacy /users/X/games URLs stay redirected to the plain profile: those are
 * old links to a scheme this page no longer has, not requests from the site.
 */
function openOn(): ProfileTab {
  const asked = route.query.tab;
  const tab =
    typeof asked === "string" && TABS.includes(asked as ProfileTab)
      ? (asked as ProfileTab)
      : DEFAULT_TAB;
  // "Аккаунт удален" has no "Зал славы" to open on.
  if (tab === "achievements" && isRemovedAccount.value) return DEFAULT_TAB;
  // Nor has anybody but a moderator a "Модерация"; for a moderator the tab
  // waits for the moderated profile (the watch on the tabs below).
  if (
    tab === "moderation" &&
    (isRemovedAccount.value || !userIsModerator(currentUser.value))
  ) {
    return DEFAULT_TAB;
  }
  return tab;
}

const activeTab = ref<ProfileTab>(openOn());
// Loosely typed: <Tabs> is a generic SFC (`generic="V extends string"`), and
// `InstanceType<typeof Tabs>` doesn't carry that type parameter through a
// template ref without extra tooling. The exposed shape is small and stable
// (see Tabs.vue's defineExpose), so a manual interface is simplest here.
const tabsRef = ref<{
  tabId(v: ProfileTab): string;
  panelId(v: ProfileTab): string;
} | null>(null);

// Query keys owned by each tab's own filter state — switching tabs must
// NOT drag a foreign tab's query along (e.g. topic filters bleeding into
// "Игры"). "number" is deliberately excluded from every tab: each tab
// paginates independently starting at page 1, so a page number carried
// over from whichever tab was active before is never valid for the tab
// being switched to (games' "?number=3" is meaningless once "Топики" —
// or even a freshly reset "Игры" — renders its own page-1 list).
// "about"/"achievements"/"moderation" keep no query state at all.
const TAB_QUERY_KEYS: Record<ProfileTab, readonly string[]> = {
  about: [],
  games: [],
  blogs: [],
  topics: [
    "search",
    "authors",
    "createdFromUtc",
    "createdToUtc",
    "sortBy",
    "sortOrder",
  ],
  achievements: [],
  moderation: [],
};

/** A refused one-press subscribe, under the row of actions. */
const subscribeResult = useActionResult();

function onTabChange(value: ProfileTab) {
  activeTab.value = value;
  // The tab itself never touches the URL — only the query is cleaned so a
  // foreign tab's filter/paging state doesn't leak into the new one.
  const allowedKeys = TAB_QUERY_KEYS[value];
  const query: LocationQueryRaw = {};
  for (const key of allowedKeys) {
    if (route.query[key] !== undefined) query[key] = route.query[key];
  }
  router.replace({ query });
}

const hasGames = computed(
  () => (user.value?.gamesHosting ?? 0) + (user.value?.gamesPlaying ?? 0) > 0,
);
const hasBlogs = computed(() => (user.value?.blogsHosting ?? 0) > 0);
// Best-post / publication widgets handle their own empty state — we
// gate the WIDGET on data presence (no point rendering "no rated post"
// when we know reviewsReceived === 0), not the tab itself. The tab is
// already justified by the games / blogs list.
const hasBestPost = computed(() => (user.value?.reviewsReceived ?? 0) > 0);

// Each content category is its own first-class tab. The Games / Blogs
// tabs also fold in the user's "best of" widget (best forum post, best
// publication) at the end of the list, plus the subset of subscribers
// who opted into THAT category's notifications. The "Топики" tab swaps
// the list for a forum/newbies-style table (filters + sort + paging).
// Games / Blogs hide when the user has zero of that content. Topics
// always shows: the empty state inside the table explains the absence.
const tabs = computed<TabItem<ProfileTab>[]>(() => [
  { value: "about", label: "О себе" },
  { value: "games", label: "Игры", hidden: !hasGames.value },
  { value: "blogs", label: "Блоги", hidden: !hasBlogs.value },
  { value: "topics", label: "Топики" },
  // Tab label. The tab value stays "achievements"; the two sections inside
  // are titled "Награды" / "Достижения". Awards and achievements are a
  // person's, and "Аккаунт удален" is nobody.
  {
    value: "achievements",
    label: "Зал славы",
    hidden: isRemovedAccount.value,
  },
  // Last, and only for the viewer the server serves the moderated profile to.
  { value: "moderation", label: "Модерация", hidden: !moderatedProfile.value },
]);

// A profile opened on "Модерация" (?tab=moderation) waits for the answer that
// decides whether the tab exists at all, and leaves it when the answer is
// none: the end of the wait is watched too, since an answer of nothing
// changes no tab.
watch([tabs, moderationLoading], ([next]) => {
  if (activeTab.value === "moderation" && moderationLoading.value) return;
  const current = next.find((t) => t.value === activeTab.value);
  if (current?.hidden) onTabChange(DEFAULT_TAB);
});

onMounted(async () => {
  await checkPendingUsernameChange();
  // The viewer's own subscriptions, read for the subscribe button, which the
  // profile of "Аккаунт удален" does not have.
  if (currentUser.value && !isOwnProfile.value && !isRemovedAccount.value) {
    subscriptionsStore.fetchSubscriptions();
  }
});

watch(usernameParam, () => {
  // Another profile is a fresh page — the tab choice does not carry over.
  activeTab.value = DEFAULT_TAB;
});
</script>

<template>
  <div v-if="loadingProfile" class="profile-page">
    <ProfileSkeleton />
  </div>

  <ErrorPage v-else-if="errorCode" :code="errorCode" />

  <div v-else-if="isSystemUser && user" class="profile-page system">
    <h1 class="system-title">{{ user.username }}</h1>
    <p class="system-description">
      Системный пользователь для автоматических действий. Выдает баны за
      нарушения и выполняет служебные операции. Этот профиль не принадлежит
      реальному человеку.
    </p>
  </div>

  <div v-else-if="user" class="profile-page">
    <div v-if="saveError" class="save-error">{{ saveError }}</div>

    <PageTitle>
      Профиль: {{ user.username }}{{ " "
      }}<Tooltip v-if="usernameHistory.length" placement="bottom" focusable>
        <template #content>
          <div class="history">
            <div class="history-title">Прошлые имена:</div>
            <div
              v-for="entry in usernameHistory"
              :key="entry.id"
              class="history-entry"
            >
              <span class="history-old">{{ entry.oldUsername }}</span>
              <span class="history-when">{{
                formatDate(entry.changedUtc)
              }}</span>
            </div>
          </div>
        </template>
        <span class="history-hint" aria-label="Прошлые имена">[...]</span>
      </Tooltip>
    </PageTitle>

    <template v-if="canEdit && isEditMode">
      <p v-if="hasPendingUsernameChange" class="change-name-pending">
        Заявка на смену имени уже на рассмотрении
      </p>
      <button
        v-else-if="!showChangeForm"
        type="button"
        class="change-name-link"
        @click="showChangeForm = true"
      >
        (запросить смену имени профиля)
      </button>
      <div v-else class="change-form">
        <textarea
          v-model="changeFormReason"
          class="change-form-input"
          placeholder="Причина смены (минимум 10 символов)"
          aria-label="Причина смены имени"
          rows="3"
          :disabled="isChangeFormSubmitting"
        />
        <div class="change-form-actions">
          <Button
            variant="primary"
            :loading="isChangeFormSubmitting"
            :disabled="!canSubmitChangeForm"
            @click="submitUsernameChangeRequest"
          >
            Отправить
          </Button>
          <Button
            :disabled="isChangeFormSubmitting"
            @click="showChangeForm = false"
          >
            Отмена
          </Button>
        </div>
        <ActionResult :result="changeResult" class="change-refusal" />
      </div>
    </template>

    <section class="identity">
      <div class="col col-identity">
        <div
          class="avatar-wrapper"
          :class="{ 'avatar-wrapper-unsized': !avatarSizeIsKnown }"
        >
          <AvatarImg
            :picture="user.picture"
            :alt="user.username"
            :size="220"
            img-class="avatar"
            prefer-original
            eager
          />
          <ProfilePictureUpload
            v-if="isEditMode && canEdit"
            :username="user.username as Username"
          />
        </div>

        <div v-if="userRoles.length" class="role-line">
          {{ userRoles.join(", ") }}
        </div>

        <!-- In edit mode the status is a field of the page, captioned the
             way every field of a page is (FormField), as the fields of the
             contacts below it. -->
        <FormField
          v-if="isEditMode && canEdit"
          label="Статус"
          name="profile-status"
        >
          <input
            id="profile-status"
            type="text"
            class="status-input"
            :value="user.status ?? ''"
            placeholder="Введите статус"
            @input="
              onFieldUpdate('status', ($event.target as HTMLInputElement).value)
            "
          />
        </FormField>
        <div v-else class="status-row">
          <span class="status-label">Статус:</span>{{ " "
          }}<span v-if="user.status" class="status-value">{{
            user.status
          }}</span
          ><span v-else class="status-value">не указан</span>
        </div>

        <div class="stats-group">
          <StatLine
            v-if="!isRemovedAccount"
            label="Дата регистрации"
            :value="registrationDate"
          />
          <!-- The counters of "Аккаунт удален" are sums over every removed
               account, and the lists behind the links are asked by a name
               the server does not answer there: plain numbers. -->
          <StatLine
            label="Рейтинг"
            :value="ratingSumDisplay"
            :variant="ratingSumVariant"
            :to="
              ratingSum !== null && !isRemovedAccount
                ? receivedReviewsLink
                : undefined
            "
          />
          <!--
            The stat-rating subgroup: "Рейтинг" (received reviews)
            sits right next to "Оценено чужих постов" (given reviews) —
            both are about review activity, convenient to compare "given / received".
            "Написано постов" is a separate output counter, hence below
            the review pair, next to the rating-opt-in checkbox (which
            controls the visibility of that very rating above).
          -->
          <StatLine
            label="Оценено чужих постов"
            :value="reviewsGiven"
            :to="isRemovedAccount ? undefined : reviewsGivenLink"
          />
          <label v-if="isEditMode && canEdit" class="rating-opt-in">
            <input
              type="checkbox"
              :checked="ratingEnabled"
              @change="
                (e) =>
                  onFieldUpdate(
                    'ratingEnabled',
                    String((e.target as HTMLInputElement).checked),
                  )
              "
            />
            Участие в рейтинге
          </label>
          <StatLine label="Написано постов" :value="gamePostsCount" />
          <StatLine
            v-if="!isRemovedAccount"
            label="Последняя активность"
            :value="isOnline ? 'online' : lastActivityFormatted"
            :variant="isOnline ? 'positive' : 'default'"
            :emphasized="false"
          />
          <!-- Endorsements are a separate semantic subgroup: set apart from
               the other stat lines by visual spacing, but use the
               same StatLine idiom (number = link) as the
               "Рейтинг/Оценено чужих постов" pair. -->
          <div v-if="!isRemovedAccount" class="endorsement-stats">
            <StatLine
              label="Получено рекомендаций"
              :value="endorsementsReceived"
              :to="
                endorsementsReceived > 0 ? receivedEndorsementsLink : undefined
              "
            />
            <StatLine
              label="Написано рекомендаций"
              :value="endorsementsGiven"
              :to="endorsementsGiven > 0 ? givenEndorsementsLink : undefined"
            />
          </div>
          <!-- Reviews of whole games: their own subgroup beside the
               recommendations, on the same idiom and the same spacing. Worded
               apart from the post pair above on purpose, because "Рейтинг" and
               "Оценено чужих постов" count ratings of single posts and these
               two count reviews of games. -->
          <div v-if="!isRemovedAccount" class="game-review-stats">
            <StatLine
              label="Получено рецензий на игры"
              :value="gameReviewsReceived"
              :to="
                gameReviewsReceived > 0 ? receivedGameReviewsLink : undefined
              "
            />
            <StatLine
              label="Написано рецензий на игры"
              :value="gameReviewsGiven"
              :to="gameReviewsGiven > 0 ? givenGameReviewsLink : undefined"
            />
          </div>
        </div>

        <ProfileViolations
          v-if="!isRemovedAccount"
          :username="usernameParam as Username"
        />

        <!-- A row of buttons (ButtonRow): one line at every width, each at its
             own width. Every action stands at every width, none put away. -->
        <ButtonRow class="actions">
          <template v-if="canEdit">
            <template v-if="isEditMode">
              <Button
                v-if="hasChanges"
                variant="primary"
                :loading="isSaving"
                @click="saveChanges"
              >
                Сохранить
              </Button>
              <Button :disabled="isSaving" @click="cancelEdit">Отмена</Button>
            </template>
            <Button v-else @click="toggleEditMode">Редактировать</Button>
          </template>
          <!-- A navigation, so a link, drawn as the button beside it: a
               <button> inside an <a> is two controls in one, and the keyboard
               stopped on both. -->
          <template
            v-else-if="currentUser && !isOwnProfile && !isRemovedAccount"
          >
            <!-- The subscription stands right after the message: the
                 recommendation after it comes and goes with the server's
                 answer, and in front of it that moved the subscription
                 between the second and the third place from one profile to
                 the next. The message is offered only where the server says
                 it would be taken (canMessage, read with the profile); without
                 it the subscription is the first action. -->
            <router-link
              v-if="canMessage"
              :to="{
                name: 'direct-message',
                params: { username: user.username },
              }"
              class="action-link"
              >Написать сообщение</router-link
            >
            <UserSubscribeButton
              :user-id="user.id"
              :username="user.username"
              :result="subscribeResult"
            />
            <!-- Offered only where the server says the POST would be
                 accepted: canEndorse is its answer, asked on load. -->
            <router-link
              v-if="canEndorse"
              :to="writeEndorsementLink"
              class="action-link"
              >Написать рекомендацию</router-link
            >
            <Button :loading="isBlockLoading" @click="toggleBlock">
              {{ isBlocked ? "Убрать из черного списка" : "В черный список" }}
            </Button>
          </template>
          <!-- The account settings, reached from the owner's own page and
               from nowhere else: the header no longer carries them. They stand
               where a visitor finds the actions of somebody else's page, in
               the same dress. -->
          <router-link
            v-if="isOwnProfile && !isEditMode"
            :to="{ name: 'account' }"
            class="action-link"
            >Настройки</router-link
          >
        </ButtonRow>
        <ActionResult :result="blockResult" />
        <ActionResult :result="subscribeResult" />

        <div v-if="showExtraLinks" class="extra-links">
          <span class="extra-links-title">Дополнительные ссылки:</span>
          <!-- The reader's own subscriptions, which no other link of the
               site leads to. -->
          <router-link
            v-if="isOwnProfile"
            :to="{ name: 'subscriptions' }"
            class="extra-link"
            >Подписки</router-link
          >
          <router-link
            v-if="isOwnProfile"
            :to="{ name: 'my-tickets' }"
            class="extra-link"
            >Мои обращения</router-link
          >
          <router-link :to="uploadsLink" class="extra-link"
            >Загруженное</router-link
          >
        </div>
      </div>
    </section>

    <ProfilePersonalInfo
      v-if="!isRemovedAccount"
      :user="user"
      :is-edit-mode="isEditMode"
      @update-field="onFieldUpdate"
    />

    <!-- Section navigation. No separate heading — the tabs themselves are
         the section header (large, active shown in brown), so an
         "Информация" title above them would just be redundant chrome. -->
    <!-- Every tab at every width: where the column is narrow the strip takes
         the lines it needs, breaking after a bar (Tabs). The open tab goes
         first. -->
    <Tabs
      ref="tabsRef"
      class="info-section"
      :model-value="activeTab"
      :tabs="tabs"
      variant="headings"
      aria-label="Разделы профиля"
      @update:model-value="onTabChange"
    />

    <div
      class="tab-content"
      role="tabpanel"
      :id="tabsRef?.panelId(activeTab)"
      :aria-labelledby="tabsRef?.tabId(activeTab)"
    >
      <template v-if="activeTab === 'about'">
        <section v-if="noteVisible" class="profile-note">
          <BlockTitle>Личная заметка</BlockTitle>
          <template v-if="!isEditingNote">
            <p v-if="note?.text" class="note-text">{{ note.text }}</p>
            <p v-else class="note-empty">Заметки об этом пользователе нет</p>
            <!-- A caption line of actions alone: the note is the viewer's
                 own and signs nothing, so there is no author or date. -->
            <CaptionLine class="note-caption">
              <CaptionAction @click="startEditNote">{{
                note?.text ? "Редактировать" : "Добавить"
              }}</CaptionAction>
              <CaptionAction
                v-if="note?.text"
                @click="confirmingNoteDelete = true"
                >Удалить</CaptionAction
              >
            </CaptionLine>
          </template>
          <div v-else class="note-expanded">
            <BBCodeEditor
              v-model="noteEditText"
              context="common"
              placeholder="Напишите заметку об этом пользователе..."
              :min-height="100"
              :max-height="300"
            />
            <div class="note-actions">
              <Button
                variant="primary"
                :loading="isNoteSaving"
                @click="saveNote"
              >
                Сохранить
              </Button>
              <Button :disabled="isNoteSaving" @click="cancelEditNote">
                Отмена
              </Button>
            </div>
            <ActionResult :result="noteResult" class="note-refusal" />
          </div>
        </section>

        <ProfileRemovedAccountAbout v-if="isRemovedAccount" />
        <ProfileAbout
          v-else
          :user="user"
          :is-edit-mode="isEditMode"
          @update-field="onFieldUpdate"
        />
      </template>
      <template v-else-if="activeTab === 'games'">
        <ProfileGamesTable :username="usernameParam" />
        <section v-if="hasBestPost" class="featured-section">
          <BlockTitle>Лучший игровой пост</BlockTitle>
          <ProfileBestPostSection :username="usernameParam as Username" />
        </section>
        <ProfileSubscribersSection
          v-if="!isRemovedAccount"
          :subscribers="subscribers"
          label="Подписаны на игры"
          :flag="SubscriptionSettings.AuthorGameEvents"
          :total="subscriberCounts.games"
        />
      </template>

      <template v-else-if="activeTab === 'blogs'">
        <ProfileBlogsTable :username="usernameParam" />
        <section class="featured-section">
          <BlockTitle>Самая популярная публикация</BlockTitle>
          <ProfileBestPublicationSection :username="usernameParam" />
        </section>
        <ProfileSubscribersSection
          v-if="!isRemovedAccount"
          :subscribers="subscribers"
          label="Подписаны на блоги"
          :flag="SubscriptionSettings.AuthorBlogEvents"
          :total="subscriberCounts.blogs"
        />
      </template>

      <template v-else-if="activeTab === 'topics'">
        <ProfileTopicsList :username="usernameParam" />
        <ProfileSubscribersSection
          v-if="!isRemovedAccount"
          :subscribers="subscribers"
          label="Подписаны на топики"
          :flag="SubscriptionSettings.AuthorTopicEvents"
          :total="subscriberCounts.topics"
        />
      </template>

      <ProfileAchievements
        v-else-if="activeTab === 'achievements'"
        :username="usernameParam"
        :user="user"
      />

      <ProfileModerationTab
        v-else-if="activeTab === 'moderation' && moderatedProfile"
        :profile="moderatedProfile"
        :target-username="usernameParam"
        @updated="refreshModeration"
      />
    </div>

    <Transition name="save-bar">
      <div v-if="isEditMode && canEdit" class="save-bar">
        <span class="save-bar-status">
          {{ hasChanges ? "Несохраненные изменения" : "Режим редактирования" }}
        </span>
        <div class="save-bar-actions">
          <Button
            v-if="hasChanges"
            variant="primary"
            :loading="isSaving"
            @click="saveChanges"
          >
            Сохранить
          </Button>
          <Button :disabled="isSaving" @click="cancelEdit">
            {{ hasChanges ? "Отмена" : "Завершить" }}
          </Button>
        </div>
      </div>
    </Transition>

    <ConfirmDialog
      v-model:show="confirmingNoteDelete"
      title="Удалить заметку?"
      message="Заметка будет удалена без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить заметку"
      :action="deleteNote"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/ZIndex" as *

// gap=$small (8) is the base — for the H1→identity and identity→"Контакты" pairs,
// which perceptually work better tighter. Between "Контакты" and Tabs,
// and between Tabs and tab-content, $medium (16) is needed for separation,
// added explicitly via margin-top on those blocks below.
.profile-page
  display: flex
  flex-direction: column
  gap: $small
  padding-bottom: 80px

.save-error
  color: $accent-red
  padding: $small 0
  font-size: $font-size

.history-hint
  color: $text-muted
  cursor: help
  font-weight: normal

.identity
  display: block
  // Tuck the avatar up to the H1 "Профиль: …": subtract h1.margin-bottom
  // ($small=8), leaving an 8px visual gap (page-gap) instead of 16px.
  margin: -$small 0 0

// As wide as the column: the row of actions stretches across it, its buttons
// in one line (ButtonRow). Everything else in it stands at its own width at
// the left.
.col-identity
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small

// The within-group row-gap is unified across all profile stat groups
// (.stats-group, .endorsement-stats, .game-review-stats in ProfilePage;
// .info-grid, .contacts-subgroup in ProfilePersonalInfo) — $minor (4px) on top
// of each line's line-height 1.25 gives 7-8px of visual air.
// Less — the lines stick together; more — the group's logical unity breaks.
// Inter-group spacing is controlled by the $medium margin on .endorsement-stats
// / .game-review-stats / .info-grid-break — intentionally larger than the
// within-group one so it reads as a context switch.
.stats-group
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $minor

// Endorsements are a separate semantic subgroup of stat lines. The top
// margin of $medium is intentionally larger than the gaps inside stats-group
// (which are squeezed down to line-height 1.25) to visually separate
// the subgroup. Symmetric with `.violations-inline` (see below): both
// "subgroups" sit at a $medium distance from the previous block.
.endorsement-stats
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $minor
  margin-top: $medium

// Game reviews are the next subgroup down, on the same terms: recommendations
// are about a person, reviews are about a game, and the two pairs read as two
// units rather than as one list of four. Same $minor rhythm inside and the
// same $medium step away from the block above.
.game-review-stats
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $minor
  margin-top: $medium

// Subscribers list styling now lives in ProfileSubscribersSection — the
// page-level rules are gone because nothing on ProfilePage renders
// `.subscribers-list` / `.subscriber-link` directly anymore.

// `.violations-inline` — the root div of ProfileViolations with the inline prop:
// a sibling of `.stats-group` inside `.col-identity { gap: $small }`.
// margin-top $small adds up with the parent's $small flex gap giving
// a total $medium gap between "Написано рецензий на игры" (the last
// game-review-stats line) and "Нарушения". This is symmetric with
// `.endorsement-stats { margin-top: $medium }` above — every subgroup
// sits at the same $medium distance from the preceding block.
:deep(.violations-inline)
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: 0
  margin-top: $small

:deep(.profile-personal-info),
:deep(.profile-about),
:deep(.profile-best-post),
.profile-note
  padding: 0
  border: none
  background: none
  margin: 0

// Inter-section gap for tab-content — $medium (16px), added on top of
// the base $small page gap via margin-top: $small. The H1→identity and
// identity→"Контакты" pairs stay at the base $small — tighter, as the user
// asked. The Tabs strip lives inside .info-section under the "Информация"
// heading and carries no outer margins of its own.
.tab-content
  margin-top: $small

// The "Информация" section (heading + Tabs strip). The top margin is aligned
// to the reference pair "last stat line → Контакты" (24px between
// text lines: col-identity gap $small + an empty .actions with
// margin-top $small + page-gap $small): the same typography on both
// sides here, so margin-top $medium on top of the $small page gap gives exactly
// the same 24px from the last contacts line to "Информация".
.info-section
  // Every tab at every width, the strip wrapping after a bar (Tabs): $medium
  // under the block above, flush with the column.
  margin-top: $medium

  // The "Информация → tabs strip" pair is measured against the reference pair
  // "Контакты → first contacts line": with the heading's stock margin-bottom
  // $small both gaps are equal by line box (8.0 == 8.0) and by
  // baseline rhythm (28.8 == 28.8) — the tabs strip carries a line with the same
  // strut (line-height 1.3) as the stat lines, no extra compensation
  // is needed.
  :deep(h2)
    margin-top: 0

:deep(.profile-personal-info > h2:first-child),
:deep(.profile-about > h2:first-child),
:deep(.profile-best-post > h2:first-child),
.profile-note > :deep(h2:first-child)
  margin-top: 0

.avatar-wrapper
  position: relative
  width: 220px
  max-width: 100%
  margin-bottom: $small

// The floor for a picture whose shape nobody knows. The original is
// aspect-preserving (<=1024 on the long side), so once the DTO carries its
// intrinsic pair AvatarImg declares it and the browser reserves the exact box
// before decoding: no floor, and no blank strip under a landscape avatar.
// An upload made before the pipeline recorded that pair leaves AvatarImg
// declaring a square, and then the square has to be held here — without it the
// box shrank on decode and pulled the role line, the statistics and the tabs
// up with it.
.avatar-wrapper-unsized
  min-height: 220px

.avatar
  display: block
  width: 100%
  height: auto
  border: 1px solid $border
  background-color: $bg-element

.role-line
  color: $accent-green

.status-row
  display: block
  font-size: $font-size
  line-height: 1.5
  color: $text

.status-label
  color: $text

.status-value
  color: $text
  word-break: break-word

.status-input
  min-width: 0

.change-name-link
  align-self: flex-start
  +inline-link-button
  &
    font-size: $font-size

.change-name-pending
  margin: 0
  font-size: $font-size
  color: $text-muted

.change-form
  display: flex
  flex-direction: column
  gap: $small
  margin: $small 0

.change-form-input
  font-family: inherit
  font-size: $font-size
  resize: vertical

.change-form-actions
  display: flex
  gap: $small

// Spaced by the gap of the column, as the note editor below.
.change-refusal,
.note-refusal
  --action-result-margin: 0

// A row of buttons as wide as the column, in one line (ButtonRow).
.actions
  align-self: stretch
  margin-top: $small

// A navigation of the actions row, drawn as the buttons beside it: the
// secondary button, the underline of a link left off.
.action-link
  text-decoration: none
  +button

// Owner-facing link subgroup: same $medium separation from the previous
// block as .endorsement-stats, same $minor within-group rhythm.
.extra-links
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $minor
  margin-top: $medium

.extra-links-title
  color: $text-muted

.extra-link
  +link-tone($link)

.rating-opt-in
  display: inline-flex
  align-items: center
  gap: $small
  font-size: $font-size
  color: $text
  margin-top: $tiny
  cursor: pointer

  input
    cursor: pointer

.history
  display: flex
  flex-direction: column
  gap: $tiny
  text-align: left

.history-title
  margin-bottom: $tiny

.history-entry
  display: flex
  justify-content: space-between
  gap: $medium

.note-text
  margin: 0 0 $small
  color: $text
  word-break: break-word
  white-space: pre-wrap

.note-empty
  margin: 0 0 $small
  color: $text-muted
  font-style: italic

.note-caption
  display: block
  margin-top: $minor

.note-expanded
  display: flex
  flex-direction: column
  gap: $small
  margin-top: $small

.note-textarea
  width: 100%
  padding: $small
  font-family: inherit
  font-size: $font-size
  resize: vertical
  min-height: 80px

.note-actions
  display: flex
  gap: $small

.tab-content
  display: flex
  flex-direction: column
  gap: $medium
  min-height: 100px

// The subscribers caption belongs to the block above it, so it sits at
// $small (8px) from it instead of the tab-content's base $medium gap —
// the negative margin eats the difference for this one pair only.
.tab-content > :deep(.subscribers-line)
  margin-top: -$small

// Featured "best of" block (best post / best publication): a full
// BlockTitle heading labels the spotlight, same rank as the other profile
// section headings ("Контакты", "Личная заметка"). The category listings
// carry no heading of their own — the active tab already names them — so
// they sit in .tab-content directly and need no wrapper section.
.featured-section
  display: flex
  flex-direction: column
  gap: $small

  :deep(h2)
    margin: 0

// Pull the tab content panel up tight against the tab strip — no double
// gap from the strip's own margin-bottom plus the .profile-page flex gap.
:deep(.tabs)
  margin-bottom: 0


.profile-page.system
  text-align: center
  padding: $big * 2

  .system-title
    margin: 0 0 $medium
    color: $heading
    font-size: 24px

  .system-description
    color: $text-muted
    line-height: 1.6
    max-width: 500px
    margin: 0 auto

// The state in words, then the buttons at the right, in one line. Where the
// column is narrower than the words and the buttons together (the middle
// column of a window of 1024), the two buttons go down a line together, still
// at the right, rather than standing past the edge of the bar.
.save-bar
  position: sticky
  bottom: 0
  left: 0
  right: 0
  z-index: $z-sticky
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small $medium
  padding: $small $medium
  margin-top: $big
  background-color: $bg-element
  border-top: 1px solid $border
  box-shadow: 0 -2px 8px $shadow-color

.save-bar-status
  flex: 1
  font-size: $secondary-font-size
  color: $text-muted

.save-bar-actions
  display: flex
  gap: $medium
  margin-left: auto

.save-bar-enter-active,
.save-bar-leave-active
  transition: transform $transition-normal, opacity $transition-normal

.save-bar-enter-from,
.save-bar-leave-to
  transform: translateY(100%)
  opacity: 0
</style>
