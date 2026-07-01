<script setup lang="ts">
import { computed, onMounted, reactive, ref, toRef, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useModal } from "vue-final-modal";
import dayjs from "dayjs";

import {
  useCommunityStore,
  useUserStore,
  UserRole,
  AvatarImg,
  type Username,
  type UsernameHistoryEntry,
} from "@/entities/user";
import { Gender } from "@/shared/api/models/community";
import { communityApi, blacklistApi } from "@/shared/api";
import type { BlacklistEntry } from "@/shared/api/models/personal";
import type { UserProfileNote } from "@/shared/api/models/community";
import { useSubscriptionsStore } from "@/shared/stores/subscriptions";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useModeratedProfile } from "@/shared/lib/composables/useModeratedProfile";
import { useProfileEdit } from "@/shared/lib/composables/useProfileEdit";
import { useToast } from "@/shared/lib/composables/useToast";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";
import { ROLE_INFO, STAFF_ROLES } from "@/shared/config/roles";

import Button from "@/shared/ui/Button/Button.vue";
import { UserSubscribeButton } from "@/features/user-subscribe";
import { Tabs, type TabItem } from "@/shared/ui/Tabs";
import { StatLine } from "@/shared/ui/StatLine";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ProfileSkeleton } from "@/shared/ui/Skeleton";
import { SvgIcon } from "@/shared/ui/Icon";
import { BlockTitle, PageTitle } from "@/shared/ui/Layout";

import ProfilePicture from "./ProfilePicture.vue";
import ProfileAbout from "./ProfileAbout.vue";
import ProfilePersonalInfo from "./ProfilePersonalInfo.vue";
import ProfileGamesTable from "./ProfileGamesTable.vue";
import ProfileBlogsTable from "./ProfileBlogsTable.vue";
import ProfileBestPost from "./ProfileBestPost.vue";
import ProfileBestPublication from "./ProfileBestPublication.vue";
import UserTopicsList from "./UserTopicsList.vue";
import ProfileSubscribersSection from "./ProfileSubscribersSection.vue";
import ProfileAchievements from "./ProfileAchievements.vue";
import { SubscriptionSettings } from "@/shared/api/models/subscriptions";
import ProfileViolations from "./ProfileViolations.vue";
import ModerationIpInfo from "./moderation/ModerationIpInfo.vue";
import ModerationLinkedProfiles from "./moderation/ModerationLinkedProfiles.vue";
import ModerationNotes from "./moderation/ModerationNotes.vue";
import ModerationViolations from "./moderation/ModerationViolations.vue";
import BlockUserLightbox from "@/pages/account/BlockUserLightbox.vue";
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
const { user: currentUser } = storeToRefs(useUserStore());
const { selectedUser: user, loadingProfile } = storeToRefs(communityStore);

const usernameParam = computed(() => route.params.username as string);

// HTTP status of the last profile load failure, mapped to an ErrorPage
// code. The store collapses every failure to a boolean, so on a miss we
// do one cheap follow-up call to learn whether it was a true 404 (user
// doesn't exist) or a server/network error — those must look different
// (404 «не найден» vs 500 «попробуйте позже»), never a fake "not found".
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
    return;
  }
  if (canEdit.value) {
    await communityStore.fetchEditableUser(name);
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

// Tab title reflects the loaded profile («{username} — DM.AM»); falls back
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
  for (const r of user.value.roles ?? []) all.add(r as UserRole);

  const activeStaffRoles = Array.from(all).filter((r) =>
    STAFF_ROLES.includes(r),
  );
  const names = activeStaffRoles
    .map((r) => ROLE_INFO[r].nicknameSingular)
    .filter(Boolean);

  if (user.value.isHonorary && activeStaffRoles.length === 0) {
    names.push("Почетный гоблин");
  }
  return names;
});

const usernameHistory = computed<UsernameHistoryEntry[]>(
  () => user.value?.usernameHistory ?? [],
);

const showChangeForm = ref(false);
watch(isEditMode, (editing) => {
  if (!editing) showChangeForm.value = false;
});

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
  return value ? dayjs(value).format("DD.MM.YYYY") : "";
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

// Subscribers: render under "Последняя активность" as inline list of links.
// Inactivity threshold matches the backend's UserActivityFilter rule —
// no activity in the last 30 days → "Inactive" → muted-gray styling on
// the profile (still a working link, hover restores link color).
const SUBSCRIBER_INACTIVITY_DAYS = 30;
function isSubscriberInactive(lastActivityUtc: string | null): boolean {
  if (!lastActivityUtc) return true;
  const last = new Date(lastActivityUtc).getTime();
  if (Number.isNaN(last)) return true;
  const ageMs = Date.now() - last;
  return ageMs > SUBSCRIBER_INACTIVITY_DAYS * 24 * 60 * 60 * 1000;
}
// Order: active subscribers first, then inactive. Within each bucket,
// most-recently-active first — gives the reader a stable "freshness"
// gradient instead of arbitrary insertion order from the backend.
const subscribers = computed(() => {
  const list = [...(user.value?.subscribers ?? [])];
  list.sort((a, b) => {
    const aInactive = isSubscriberInactive(a.lastActivityUtc) ? 1 : 0;
    const bInactive = isSubscriberInactive(b.lastActivityUtc) ? 1 : 0;
    if (aInactive !== bInactive) return aInactive - bInactive;
    const ta = a.lastActivityUtc ? new Date(a.lastActivityUtc).getTime() : 0;
    const tb = b.lastActivityUtc ? new Date(b.lastActivityUtc).getTime() : 0;
    return tb - ta;
  });
  return list;
});

// Subscribe / unsubscribe / settings are owned by <UserSubscribeButton>
// — it reads/writes through the subscriptions store directly.

const isBlocked = ref(false);
const isBlockLoading = ref(false);

const { open: openBlockModal, close: closeBlockModal } = useModal({
  component: BlockUserLightbox,
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

const notePreview = computed(() => {
  const t = note.value?.text?.trim();
  if (!t) return null;
  const oneLine = t.replace(/\s+/g, " ");
  return oneLine.length > 90 ? oneLine.slice(0, 90) + "…" : oneLine;
});

const { moderatedProfile, refresh: refreshModeration } = useModeratedProfile(
  () => usernameParam.value,
);
const showModPanel = ref(false);

const modSummary = computed(() => {
  const p = moderatedProfile.value;
  if (!p) return null;
  const ips = p.ipAddresses?.length ?? 0;
  const linked = p.linkedProfiles?.length ?? 0;
  const notes = p.moderatorNotes?.length ?? 0;
  const v = (p.violations?.totalWarnings ?? 0) + (p.violations?.totalBans ?? 0);
  return `IP: ${ips} · Связанные: ${linked} · Заметки: ${notes} · Нарушений: ${v}`;
});

const DEFAULT_TAB: ProfileTab = "about";
const VALID_TABS: readonly ProfileTab[] = [
  "about",
  "games",
  "blogs",
  "topics",
  "achievements",
];

function parseTab(value: unknown): ProfileTab {
  return VALID_TABS.includes(value as ProfileTab)
    ? (value as ProfileTab)
    : DEFAULT_TAB;
}

const activeTab = ref<ProfileTab>(parseTab(route.params.tab));

watch(
  () => route.params.tab,
  (next) => {
    const parsed = parseTab(next);
    if (parsed !== activeTab.value) activeTab.value = parsed;
  },
);

function onTabChange(value: ProfileTab) {
  activeTab.value = value;
  router.replace({
    name: "profile",
    params: {
      username: usernameParam.value,
      tab: value === DEFAULT_TAB ? "" : value,
    },
    query: route.query,
  });
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
  { value: "achievements", label: "Достижения" },
]);

watch(tabs, (next) => {
  const current = next.find((t) => t.value === activeTab.value);
  if (current?.hidden) onTabChange(DEFAULT_TAB);
});

onMounted(async () => {
  await checkIfBlocked();
  await fetchNote();
  if (currentUser.value && !isOwnProfile.value) {
    subscriptionsStore.fetchSubscriptions();
  }
});

watch(usernameParam, async () => {
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
      Личный кабинет:
      <span class="username-preserve-case">{{ user.username }}</span
      >{{ " "
      }}<Tooltip v-if="usernameHistory.length" placement="bottom">
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
                dayjs(entry.changedUtc).format("DD.MM.YYYY")
              }}</span>
            </div>
          </div>
        </template>
        <span class="history-hint" aria-label="Прошлые имена">[...]</span>
      </Tooltip>
    </PageTitle>

    <template v-if="canEdit && isEditMode">
      <button
        v-if="!showChangeForm"
        type="button"
        class="change-name-link"
        @click="showChangeForm = true"
      >
        (запросить смену имени профиля)
      </button>
      <div v-else class="change-form">
        <textarea
          class="change-form-input"
          placeholder="Желаемое имя и причина (минимум 10 символов)"
          rows="3"
        />
        <div class="change-form-actions">
          <Button @click="showChangeForm = false">Отмена</Button>
          <Button
            @click="
              () => {
                toast.success('Запрос отправлен администратору');
                showChangeForm = false;
              }
            "
          >
            Отправить
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
          <ProfilePicture
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
            Стат-рейтинговая подгруппа: «Рейтинг» (полученные оценки)
            идет впритык с «Оценено чужих постов» (выданные оценки) —
            обе про review-активность, удобно сравнивать «дано / получено».
            «Написано игровых постов» — отдельный счетчик output'а,
            поэтому ниже review-пары, рядом с rating-opt-in чекбоксом
            (который и управляет видимостью того самого рейтинга выше).
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
          <StatLine label="Написано игровых постов" :value="gamePostsCount" />
          <StatLine
            label="Последняя активность"
            :value="isOnline ? 'онлайн' : lastActivityFormatted"
            :variant="isOnline ? 'positive' : 'default'"
            :emphasized="false"
          />
          <!-- Эндорсменты — отдельная смысловая подгруппа: отделены от
               остальных stat-строк визуальным отступом, но используют ту
               же StatLine идиому (число = ссылка), что и пара
               «Рейтинг/Оценено чужих постов». -->
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

        <ProfileViolations :username="usernameParam as Username" inline />

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
      </div>
    </section>

    <section v-if="moderatedProfile" class="mod-bar">
      <button
        type="button"
        class="mod-header"
        :aria-expanded="showModPanel"
        @click="showModPanel = !showModPanel"
      >
        <SvgIcon
          :name="showModPanel ? 'chevronDown' : 'chevronRight'"
          class="mod-chevron"
        />
        <span class="mod-title">ПАНЕЛЬ МОДЕРАЦИИ</span>
        <span class="mod-summary">{{ modSummary }}</span>
      </button>
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
    </section>

    <ProfilePersonalInfo
      :user="user"
      :is-edit-mode="isEditMode"
      @update-field="onFieldUpdate"
    />

    <Tabs
      :model-value="activeTab"
      :tabs="tabs"
      aria-label="Разделы профиля"
      @update:model-value="onTabChange"
    />

    <div class="tab-content">
      <template v-if="activeTab === 'about'">
        <section v-if="noteVisible" class="profile-note">
          <BlockTitle>Личная заметка</BlockTitle>
          <template v-if="!isEditingNote">
            <p v-if="notePreview" class="note-text">{{ notePreview }}</p>
            <button
              type="button"
              class="note-toggle"
              @click="isEditingNote = true"
            >
              {{ notePreview ? "редактировать" : "добавить" }}
            </button>
          </template>
          <div v-else class="note-expanded">
            <textarea
              v-model="noteEditText"
              class="note-textarea"
              placeholder="Напишите заметку об этом пользователе…"
              rows="4"
            />
            <div class="note-actions">
              <Button :disabled="isNoteSaving" @click="saveNote">
                {{ isNoteSaving ? "Сохранение…" : "Сохранить" }}
              </Button>
              <Button :disabled="isNoteSaving" @click="isEditingNote = false">
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
        <ProfileSubscribersSection
          :subscribers="subscribers"
          label="Подписаны на игры"
          :flag="SubscriptionSettings.AuthorGameEvents"
        />
        <ProfileGamesTable :username="usernameParam" />
        <section v-if="hasBestPost" class="featured-section">
          <BlockTitle>Лучший пост</BlockTitle>
          <ProfileBestPost :username="usernameParam as Username" />
        </section>
      </template>

      <template v-else-if="activeTab === 'blogs'">
        <ProfileSubscribersSection
          :subscribers="subscribers"
          label="Подписаны на блоги"
          :flag="SubscriptionSettings.AuthorBlogEvents"
        />
        <ProfileBlogsTable :username="usernameParam" />
        <section class="featured-section">
          <BlockTitle>Лучшая публикация</BlockTitle>
          <ProfileBestPublication :username="usernameParam" />
        </section>
      </template>

      <template v-else-if="activeTab === 'topics'">
        <ProfileSubscribersSection
          :subscribers="subscribers"
          label="Подписаны на топики"
          :flag="SubscriptionSettings.AuthorTopicEvents"
        />
        <UserTopicsList :username="usernameParam" />
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
@import "src/assets/styles/Inputs"
@import "src/assets/styles/_ZIndex"

// gap=$small (8) базовый — для пары H1→identity и identity→Контакты,
// которые перцептивно лучше работают плотнее. Между «Контакты» и Tabs,
// а также между Tabs и tab-content — нужно $medium (16) для разделения,
// добавляем явно через margin-top на этих блоках ниже.
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
  // Avatar поджимаем к H1 «Личный кабинет: …»: вычитаем h1.margin-bottom
  // ($small=8), оставляя визуальный зазор 8px (page-gap), а не 16px.
  margin: -$small 0 0

.col-identity
  display: inline-flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  max-width: 100%

// Within-group row-gap унифицирован между всеми stat-группами профиля
// (.stats-group, .endorsement-stats в ProfilePage; .info-grid,
// .contacts-subgroup в ProfilePersonalInfo) — $minor (4px) поверх
// line-height 1.25 у каждой строки дает 7-8px визуального воздуха.
// Меньше — строки слипаются; больше — рвется логическое единство группы.
// Inter-group spacing управляется $medium-margin'ом у .endorsement-stats
// / .info-grid-break — намеренно больше внутри-группового, чтобы
// читалось как смена контекста.
.stats-group
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $minor

// Эндорсменты — отдельная смысловая подгруппа stat-строк. Отступ
// сверху $medium — намеренно больше, чем зазоры внутри stats-group
// (которые ужаты до line-height 1.25), чтобы визуально отделить
// подгруппу. Симметрично к `.violations-inline` (см. ниже): обе
// «подгруппы» сидят на $medium-расстоянии от предыдущего блока.
.endorsement-stats
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $minor
  margin-top: $medium

// Subscribers list styling now lives in ProfileSubscribersSection — the
// page-level rules are gone because nothing on ProfilePage renders
// `.subscribers-list` / `.subscriber-link` directly anymore.

// `.violations-inline` — корневой div ProfileViolations с inline-prop:
// сиблинг `.stats-group` внутри `.col-identity { gap: $small }`.
// margin-top $small суммируется с flex-gap'ом $small родителя и дает
// итоговый зазор $medium между «Написано рекомендаций» (последняя
// строка endorsement-stats) и «Нарушения». Это симметрично с
// `.endorsement-stats { margin-top: $medium }` сверху — обе подгруппы
// сидят на одинаковом $medium-расстоянии от предыдущей.
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

// Inter-section gap для «Контакты»/Tabs/tab-content — $medium (16px),
// добавляется поверх базового $small page-gap'а через margin-top: $small.
// Пары H1→identity и identity→«Контакты» остаются на базовых $small —
// плотнее, как просил юзер.
.tab-content,
:deep(.tabs)
  margin-top: $small

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

.info-stack
  display: contents

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

.rating-opt-in
  display: inline-flex
  align-items: center
  gap: $small
  font-size: $font-size
  color: $text
  margin-top: $tiny
  cursor: pointer
  user-select: none

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

// Featured "best of" block (best post / best publication): its BlockTitle
// labels the spotlight so it reads as a distinct sub-part below the table,
// not heading soup. Compress the BlockTitle's intrinsic margins to keep
// it tight against its widget.
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
  .identity
    grid-template-columns: 1fr

  .avatar-wrapper
    width: 100%
    max-width: 280px
</style>
