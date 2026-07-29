<script setup lang="ts">
import { formatDate } from "@/shared/lib/utils/datetime";
import { computed, onMounted, reactive, ref, toRef, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { LocationQueryRaw } from "vue-router";
import { storeToRefs } from "pinia";
import { useModal } from "vue-final-modal";
import dayjs from "dayjs";

import {
  useCommunityStore,
  useAuthStore,
  UserRole,
  AvatarImg,
  useModeratedProfile,
  useProfileEdit,
  type Username,
  type UsernameHistoryEntry,
} from "@/entities/user";
import { Gender } from "@/shared/api/models/community";
import { communityApi, blacklistApi, accountApi } from "@/shared/api";
import type { BlacklistEntry } from "@/shared/api/models/personal";
import type { UserProfileNote } from "@/shared/api/models/community";
import { useSubscriptionsStore } from "@/shared/stores/subscriptions";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useToast } from "@/shared/lib/composables/useToast";
import { useExpandableSection } from "@/shared/lib/composables";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";
import { ROLE_INFO, STAFF_ROLES } from "@/shared/config/roles";

import Button from "@/shared/ui/Button/Button.vue";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { UserSubscribeButton } from "@/features/user-subscribe";
import { Tabs, type TabItem } from "@/shared/ui/Tabs";
import { StatLine } from "@/shared/ui/StatLine";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ProfileSkeleton } from "@/shared/ui/Skeleton";
import { SvgIcon } from "@/shared/ui/Icon";
import { BlockTitle, PageTitle } from "@/shared/ui/Layout";

import ProfilePictureUpload from "./ProfilePictureUpload.vue";
import ProfileAbout from "./ProfileAbout.vue";
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
import ModerationIpInfo from "./moderation/ModerationIpInfo.vue";
import ModerationLinkedProfiles from "./moderation/ModerationLinkedProfiles.vue";
import ModerationNotes from "./moderation/ModerationNotes.vue";
import ModerationViolations from "./moderation/ModerationViolations.vue";
import { BlockUserDialog } from "@/features/block-user";
import { ErrorPage } from "@/shared/ui/ErrorPage";

// Tab vocabulary. Each content tab (games / blogs / topics) is the
// home for THREE pieces: the canonical listing, the user's best-of for
// the category, and the subset of subscribers who opted into that
// category's notifications. The free-standing "best-post" and
// "subscribers" tabs from the previous layout are gone — they're now
// inlined into the relevant content tab.
type ProfileTab = "about" | "games" | "blogs" | "topics" | "achievements";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const communityStore = useCommunityStore();
const subscriptionsStore = useSubscriptionsStore();
const { user: currentUser } = storeToRefs(useAuthStore());
const { selectedUser: user, loadingProfile } = storeToRefs(communityStore);

const usernameParam = computed(() => route.params.username as string);

// HTTP status of the last profile load failure, mapped to an ErrorPage
// code. The store collapses every failure to a boolean, so on a miss we
// do one cheap follow-up call to learn whether it was a true 404 (user
// doesn't exist) or a server/network error — those must look different
// (404 "не найден" vs 500 "попробуйте позже"), never a fake "not found".
const errorCode = ref<number | null>(null);

function mapErrorStatus(status: number | undefined): number {
  if (status === 404 || status === 410) return 404;
  if (status === 403) return 403;
  return 500;
}

async function loadProfile(name: Username) {
  errorCode.value = null;
  const success = await communityStore.trySelectProfile(name);
  if (!success) {
    const { error } = await communityApi.getUserProfile(name);
    errorCode.value = mapErrorStatus(error?.status);
  }
}

useFetchData(
  () => loadProfile(usernameParam.value as Username),
  [
    {
      param: (p) => p.username,
      callback: (name) => loadProfile(name as Username),
    },
  ],
);
const isSystemUser = computed(() => user.value?.role === UserRole.System);

// Tab title reflects the loaded profile ("{username} — Dungeon Master");
// falls back
// to the URL param while the profile is still loading so the tab is never
// blank or stale.
useDocumentTitle(() => user.value?.username ?? usernameParam.value);

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
  () => isOwnProfile.value || isCurrentUserAdmin.value,
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

async function submitUsernameChangeRequest() {
  if (!canSubmitChangeForm.value) return;
  isChangeFormSubmitting.value = true;
  const { data, error } = await accountApi.createUsernameChangeRequest({
    reason: changeFormReason.value.trim(),
  });
  isChangeFormSubmitting.value = false;
  if (error) {
    toast.error(error.message || "Не удалось отправить заявку");
    return;
  }
  if (data) {
    hasPendingUsernameChange.value = data.status === "Pending";
    changeFormReason.value = "";
    showChangeForm.value = false;
    toast.success("Заявка отправлена на рассмотрение");
  }
}

const isOnline = computed(() => {
  const t = user.value?.lastActivityUtc;
  if (!t) return false;
  return dayjs().diff(dayjs(t), "minute", true) <= ONLINE_THRESHOLD_MINUTES;
});

const lastActivityFormatted = computed(() =>
  user.value?.lastActivityUtc
    ? dayjs(user.value.lastActivityUtc).format("DD.MM.YYYY [в] HH:mm")
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

const ratingSum = computed<number | null>(() => {
  const r = user.value?.rating;
  if (!r) return null;
  return r.postReviewScoreSum ?? 0;
});

const ratingSumDisplay = computed<string>(() => {
  const v = ratingSum.value;
  if (v === null) return "n/a";
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

// Subscribe / unsubscribe / settings are owned by <UserSubscribeButton>
// — it reads/writes through the subscriptions store directly.

const isBlocked = ref(false);
const isBlockLoading = ref(false);

const { open: openBlockModal, close: closeBlockModal } = useModal({
  component: BlockUserDialog,
  attrs: reactive({
    initialUsername: usernameParam,
    onSuccess: (entry: BlacklistEntry) => {
      isBlocked.value = true;
      closeBlockModal();
      toast.success(`${entry.username} заблокирован`);
    },
    onCancel: () => closeBlockModal(),
  }),
});

async function checkIfBlocked() {
  if (!currentUser.value || isOwnProfile.value || !user.value) return;
  const { data } = await blacklistApi.getBlacklist();
  if (data?.resources) {
    isBlocked.value = data.resources.some(
      (e) => e.username === user.value!.username,
    );
  }
}

async function unblockUser() {
  if (!user.value) return;
  const confirmed = window.confirm(`Разблокировать ${user.value.username}?`);
  if (!confirmed) return;
  isBlockLoading.value = true;
  const { error } = await blacklistApi.unblockUser(user.value.username);
  isBlockLoading.value = false;
  if (error) {
    toast.error("Не удалось разблокировать пользователя");
  } else {
    isBlocked.value = false;
    toast.success(`${user.value.username} разблокирован`);
  }
}

const note = ref<UserProfileNote | null>(null);
const noteEditText = ref("");
const isEditingNote = ref(false);
const isNoteSaving = ref(false);
const noteVisible = computed(() => !!currentUser.value && !isOwnProfile.value);

async function fetchNote() {
  if (!noteVisible.value) return;
  const { data } = await communityApi.getUserProfileNote(
    usernameParam.value as Username,
  );
  note.value = data ?? null;
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
}

async function saveNote() {
  if (!noteEditText.value.trim()) return deleteNote();
  isNoteSaving.value = true;
  const { data } = await communityApi.upsertUserProfileNote(
    usernameParam.value as Username,
    noteEditText.value,
  );
  if (data) note.value = data;
  isNoteSaving.value = false;
  isEditingNote.value = false;
}

async function deleteNote() {
  isNoteSaving.value = true;
  await communityApi.deleteUserProfileNote(usernameParam.value as Username);
  note.value = null;
  noteEditText.value = "";
  isNoteSaving.value = false;
  isEditingNote.value = false;
}

const { moderatedProfile, refresh: refreshModeration } = useModeratedProfile(
  () => usernameParam.value,
);
// The moderation panel is a content expandable section: unified reveal
// animation + the page-wide "Развернуть/Свернуть все" toggle, active only
// while the viewer can actually moderate this profile.
const showModPanel = ref(false);
const modZoneRef = ref<HTMLElement | null>(null);
const { toggle: toggleModPanel, zoneBindings: modZoneBindings } =
  useExpandableSection({
    el: modZoneRef,
    model: showModPanel,
    registryEnabled: () => !!moderatedProfile.value,
    label: "ProfileModerationPanel",
  });

const modSummary = computed(() => {
  const p = moderatedProfile.value;
  if (!p) return null;
  const ips = p.ipAddresses?.length ?? 0;
  const linked = p.linkedProfiles?.length ?? 0;
  const notes = p.moderatorNotes?.length ?? 0;
  const v = (p.violations?.totalWarnings ?? 0) + (p.violations?.totalBans ?? 0);
  return `IP: ${ips}, Связанные: ${linked}, Заметки: ${notes}, Нарушений: ${v}`;
});

const DEFAULT_TAB: ProfileTab = "about";

// Pure client state: the tab is NOT reflected in the URL. F5 or a shared
// link always lands on the default tab; legacy /users/X/games URLs are
// redirected to the plain profile by the router.
const activeTab = ref<ProfileTab>(DEFAULT_TAB);
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
// "about"/"achievements" keep no query state at all.
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
};

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
  // are titled "Награды" / "Достижения".
  { value: "achievements", label: "Зал славы" },
]);

watch(tabs, (next) => {
  const current = next.find((t) => t.value === activeTab.value);
  if (current?.hidden) onTabChange(DEFAULT_TAB);
});

onMounted(async () => {
  await checkIfBlocked();
  await fetchNote();
  await checkPendingUsernameChange();
  if (currentUser.value && !isOwnProfile.value) {
    subscriptionsStore.fetchSubscriptions();
  }
});

watch(usernameParam, async () => {
  // Another profile is a fresh page — the tab choice does not carry over.
  activeTab.value = DEFAULT_TAB;
  await fetchNote();
  await checkIfBlocked();
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
          rows="3"
          :disabled="isChangeFormSubmitting"
        />
        <div class="change-form-actions">
          <Button
            :disabled="isChangeFormSubmitting"
            @click="showChangeForm = false"
          >
            Отмена
          </Button>
          <Button
            :disabled="!canSubmitChangeForm"
            @click="submitUsernameChangeRequest"
          >
            {{ isChangeFormSubmitting ? "Отправка…" : "Отправить" }}
          </Button>
        </div>
      </div>
    </template>

    <section class="identity">
      <div class="col col-identity">
        <div class="avatar-wrapper">
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

        <div class="status-row">
          <span class="status-label">Статус:</span>{{ " "
          }}<input
            v-if="isEditMode && canEdit"
            type="text"
            class="status-input"
            :value="user.status ?? ''"
            placeholder="Введите статус"
            @input="
              onFieldUpdate('status', ($event.target as HTMLInputElement).value)
            "
          /><span v-else-if="user.status" class="status-value">{{
            user.status
          }}</span
          ><span v-else class="status-value">не указан</span>
        </div>

        <div class="stats-group">
          <StatLine label="Дата регистрации" :value="registrationDate" />
          <StatLine
            label="Рейтинг"
            :value="ratingSumDisplay"
            :variant="ratingSumVariant"
            :to="ratingSum !== null ? receivedReviewsLink : undefined"
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
            :to="reviewsGivenLink"
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
            label="Последняя активность"
            :value="isOnline ? 'online' : lastActivityFormatted"
            :variant="isOnline ? 'positive' : 'default'"
            :emphasized="false"
          />
          <!-- Endorsements are a separate semantic subgroup: set apart from
               the other stat lines by visual spacing, but use the
               same StatLine idiom (number = link) as the
               "Рейтинг/Оценено чужих постов" pair. -->
          <div class="endorsement-stats">
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
        </div>

        <ProfileViolations :username="usernameParam as Username" />

        <div class="actions">
          <template v-if="canEdit">
            <template v-if="isEditMode">
              <Button
                v-if="hasChanges"
                :disabled="isSaving"
                @click="saveChanges"
              >
                {{ isSaving ? "Сохранение…" : "Сохранить" }}
              </Button>
              <Button :disabled="isSaving" @click="cancelEdit">Отмена</Button>
            </template>
            <Button v-else @click="toggleEditMode">Редактировать</Button>
          </template>
          <template v-else-if="currentUser && !isOwnProfile">
            <router-link
              :to="{
                name: 'direct-message',
                params: { username: user.username },
              }"
              class="action-link"
            >
              <Button>Написать сообщение</Button>
            </router-link>
            <UserSubscribeButton :user-id="user.id" :username="user.username" />
            <Button
              v-if="isBlocked"
              :disabled="isBlockLoading"
              @click="unblockUser"
            >
              {{ isBlockLoading ? "…" : "Разблокировать" }}
            </Button>
            <Button v-else @click="() => openBlockModal()"
              >Заблокировать</Button
            >
          </template>
        </div>

        <div v-if="showExtraLinks" class="extra-links">
          <span class="extra-links-title">Дополнительные ссылки:</span>
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

    <section v-if="moderatedProfile" class="mod-bar">
      <button
        type="button"
        class="mod-header"
        :aria-expanded="showModPanel"
        @click="toggleModPanel()"
      >
        <SvgIcon
          :name="showModPanel ? 'chevronDown' : 'chevronRight'"
          class="mod-chevron"
        />
        <span class="mod-title">ПАНЕЛЬ МОДЕРАЦИИ</span>
        <span class="mod-summary">{{ modSummary }}</span>
      </button>
      <div ref="modZoneRef" class="expand-zone" v-bind="modZoneBindings">
        <div v-if="showModPanel" class="mod-body">
          <ModerationIpInfo
            v-if="moderatedProfile.permissions.canViewIpAddresses"
            :email="moderatedProfile.email"
            :ip-addresses="moderatedProfile.ipAddresses"
            :login-history="moderatedProfile.loginHistory"
          />
          <ModerationLinkedProfiles
            v-if="moderatedProfile.permissions.canViewLinkedProfiles"
            :profiles="moderatedProfile.linkedProfiles"
          />
          <ModerationNotes
            v-if="moderatedProfile.permissions.canViewModNotes"
            :notes="moderatedProfile.moderatorNotes"
            :can-create="moderatedProfile.permissions.canCreateModNote"
            :target-username="usernameParam"
            @updated="refreshModeration"
          />
          <ModerationViolations
            :violations="moderatedProfile.violations"
            :permissions="moderatedProfile.permissions"
            :target-username="usernameParam"
            @updated="refreshModeration"
          />
        </div>
      </div>
    </section>

    <ProfilePersonalInfo
      :user="user"
      :is-edit-mode="isEditMode"
      @update-field="onFieldUpdate"
    />

    <!-- Section navigation. No separate heading — the tabs themselves are
         the section header (large, active shown in brown), so an
         "Информация" title above them would just be redundant chrome. -->
    <section class="info-section">
      <Tabs
        ref="tabsRef"
        :model-value="activeTab"
        :tabs="tabs"
        variant="headings"
        aria-label="Разделы профиля"
        @update:model-value="onTabChange"
      />
    </section>

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
            <div class="note-actions">
              <button type="button" class="note-toggle" @click="startEditNote">
                {{ note?.text ? "Редактировать" : "Добавить" }}
              </button>
              <button
                v-if="note?.text"
                type="button"
                class="note-toggle note-delete"
                :disabled="isNoteSaving"
                @click="deleteNote"
              >
                Удалить
              </button>
            </div>
          </template>
          <div v-else class="note-expanded">
            <BBCodeEditor
              v-model="noteEditText"
              context="common"
              placeholder="Напишите заметку об этом пользователе…"
              :min-height="100"
              :max-height="300"
            />
            <div class="note-actions">
              <Button :disabled="isNoteSaving" @click="saveNote">
                {{ isNoteSaving ? "Сохранение…" : "Сохранить" }}
              </Button>
              <Button :disabled="isNoteSaving" @click="cancelEditNote">
                Отмена
              </Button>
            </div>
          </div>
        </section>

        <ProfileAbout
          :user="user"
          :is-edit-mode="isEditMode"
          @update-field="onFieldUpdate"
        />
      </template>
      <template v-else-if="activeTab === 'games'">
        <section v-if="hasBestPost" class="featured-section">
          <BlockTitle>Лучший игровой пост</BlockTitle>
          <ProfileBestPostSection :username="usernameParam as Username" />
        </section>
        <section class="list-section">
          <BlockTitle>Игры пользователя</BlockTitle>
          <ProfileGamesTable :username="usernameParam" />
        </section>
        <ProfileSubscribersSection
          :subscribers="subscribers"
          label="Подписаны на игры"
          :flag="SubscriptionSettings.AuthorGameEvents"
        />
      </template>

      <template v-else-if="activeTab === 'blogs'">
        <section class="featured-section">
          <BlockTitle>Самая популярная публикация</BlockTitle>
          <ProfileBestPublicationSection :username="usernameParam" />
        </section>
        <section class="list-section">
          <BlockTitle>Блоги пользователя</BlockTitle>
          <ProfileBlogsTable :username="usernameParam" />
        </section>
        <ProfileSubscribersSection
          :subscribers="subscribers"
          label="Подписаны на блоги"
          :flag="SubscriptionSettings.AuthorBlogEvents"
        />
      </template>

      <template v-else-if="activeTab === 'topics'">
        <section class="list-section">
          <BlockTitle>Топики пользователя</BlockTitle>
          <ProfileTopicsList :username="usernameParam" />
        </section>
        <ProfileSubscribersSection
          :subscribers="subscribers"
          label="Подписаны на топики"
          :flag="SubscriptionSettings.AuthorTopicEvents"
        />
      </template>

      <ProfileAchievements
        v-else-if="activeTab === 'achievements'"
        :username="usernameParam"
        :user="user"
      />
    </div>

    <Transition name="save-bar">
      <div v-if="isEditMode && canEdit" class="save-bar">
        <span class="save-bar-status">
          {{ hasChanges ? "Несохраненные изменения" : "Режим редактирования" }}
        </span>
        <Button v-if="hasChanges" :disabled="isSaving" @click="saveChanges">
          {{ isSaving ? "Сохранение…" : "Сохранить" }}
        </Button>
        <Button :disabled="isSaving" @click="cancelEdit">
          {{ hasChanges ? "Отмена" : "Завершить" }}
        </Button>
      </div>
    </Transition>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/_ZIndex"

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

.col-identity
  display: inline-flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  max-width: 100%

// The within-group row-gap is unified across all profile stat groups
// (.stats-group, .endorsement-stats in ProfilePage; .info-grid,
// .contacts-subgroup in ProfilePersonalInfo) — $minor (4px) on top of
// each line's line-height 1.25 gives 7-8px of visual air.
// Less — the lines stick together; more — the group's logical unity breaks.
// Inter-group spacing is controlled by the $medium margin on .endorsement-stats
// / .info-grid-break — intentionally larger than the within-group one so
// it reads as a context switch.
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

// Subscribers list styling now lives in ProfileSubscribersSection — the
// page-level rules are gone because nothing on ProfilePage renders
// `.subscribers-list` / `.subscriber-link` directly anymore.

// `.violations-inline` — the root div of ProfileViolations with the inline prop:
// a sibling of `.stats-group` inside `.col-identity { gap: $small }`.
// margin-top $small adds up with the parent's $small flex gap giving
// a total $medium gap between "Написано рекомендаций" (the last
// endorsement-stats line) and "Нарушения". This is symmetric with
// `.endorsement-stats { margin-top: $medium }` above — both subgroups
// sit at the same $medium distance from the preceding block.
:deep(.violations-inline)
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: 0
  margin-top: $small

:deep(.profile-personal-info),
:deep(.profile-about),
:deep(.profile-best-post),
.profile-note,
.mod-bar
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
  margin: $medium 0 0
  padding: 0

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
  font-family: inherit
  font-size: $font-size
  min-width: 0

.change-name-link
  align-self: flex-start
  padding: 0
  background: none
  border: none
  font-family: inherit
  font-size: $font-size
  color: $link
  cursor: pointer

  &:hover
    color: $link-hover

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

.actions
  display: flex
  flex-wrap: wrap
  gap: $small
  margin-top: $small

.action-link
  text-decoration: none

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
  color: $link
  text-decoration: none
  &:hover
    color: $link-hover
    text-decoration: underline

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

.mod-header
  display: flex
  align-items: center
  gap: $small
  width: 100%
  padding: 0
  background: none
  border: none
  text-align: left
  cursor: pointer
  font-family: inherit
  transition: background-color $transition-fast

  &:hover
    background-color: $hover-overlay

.mod-chevron
  width: 14px
  height: 14px
  flex-shrink: 0
  color: $accent-red

.mod-title
  font-weight: 600
  letter-spacing: 0.5px
  text-transform: uppercase
  color: $accent-red

.mod-summary
  font-size: $secondary-font-size
  color: $text-muted

.mod-body
  display: flex
  flex-direction: column
  gap: $medium
  padding: $medium 0 0

.note-text
  margin: 0 0 $small
  color: $text
  word-break: break-word
  white-space: pre-wrap

.note-empty
  margin: 0 0 $small
  color: $text-muted
  font-style: italic

.note-toggle
  padding: 0
  background: none
  border: none
  font-family: inherit
  font-size: $font-size
  color: $link
  cursor: pointer

  &:hover
    color: $link-hover

  &.note-delete
    color: $text-muted

    &:hover
      color: $accent-red

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

// The subscribers caption belongs to the table above it, so it sits at
// $small (8px) from it instead of the tab-content's base $medium gap —
// the negative margin eats the difference for this one pair only; the
// gap below the line (to the "best of" section) stays $medium.
.tab-content > :deep(.subscribers-line)
  margin-top: -$small

// Featured "best of" block (best post / best publication): a full
// BlockTitle heading labels the spotlight, same rank as the other profile
// section headings ("Контакты", "Личная заметка").
.featured-section,
.list-section
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

.save-bar
  position: sticky
  bottom: 0
  left: 0
  right: 0
  z-index: $z-sticky
  display: flex
  align-items: center
  gap: $medium
  padding: $small $medium
  margin-top: $big
  background-color: $bg-element
  border-top: 1px solid $border
  box-shadow: 0 -2px 8px $shadow-color

.save-bar-status
  flex: 1
  font-size: $secondary-font-size
  color: $text-muted

.save-bar-enter-active,
.save-bar-leave-active
  transition: transform $transition-normal, opacity $transition-normal

.save-bar-enter-from,
.save-bar-leave-to
  transform: translateY(100%)
  opacity: 0

@media (max-width: 768px)
  .avatar-wrapper
    width: 100%
    max-width: 280px
</style>
