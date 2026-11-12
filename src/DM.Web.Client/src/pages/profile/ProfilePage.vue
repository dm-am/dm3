<script setup lang="ts">
import { computed, onMounted, ref, toRef, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";

import {
  useCommunityStore,
  useAuthStore,
  UserRole,
  useProfileEdit,
  isRemovedAccountName,
  isSameUsername,
  personalApi,
  accountApi,
  type Username,
} from "@/entities/user";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import {
  type Contact,
  Gender,
  type PersonalProfile,
  type UserPicture,
} from "@/shared/api/models/community";
import { useSubscriptionsStore } from "@/entities/subscription";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { ROLE_INFO, STAFF_ROLES } from "@/shared/config/roles";

import { FormField } from "@/shared/ui/Form";
import { ProfileSkeleton } from "@/shared/ui/Skeleton";

import { useEndorsementEligibility } from "./useEndorsementEligibility";
import { useProfileBlacklist } from "./useProfileBlacklist";
import ProfilePictureUpload from "./ProfilePictureUpload.vue";
import ProfilePersonalInfo from "./ProfilePersonalInfo.vue";
import ProfileUsername from "./ProfileUsername.vue";
import ProfileStats from "./ProfileStats.vue";
import ProfileActions from "./ProfileActions.vue";
import ProfileEditBar from "./ProfileEditBar.vue";
import ProfileTabs, { useProfileTabs } from "./ProfileTabs.vue";
import ProfilePersonalNote, { useProfileNote } from "./ProfilePersonalNote.vue";
import ProfileViolations from "./ProfileViolations.vue";
import {
  ErrorPage,
  errorCodeForStatus,
  getErrorConfig,
} from "@/shared/ui/ErrorPage";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const route = useRoute();
const communityStore = useCommunityStore();
const subscriptionsStore = useSubscriptionsStore();
const { user: currentUser } = storeToRefs(useAuthStore());
const { selectedUser: user, loadingProfile } = storeToRefs(communityStore);

const usernameParam = computed(() => route.params.username as Username);

/**
 * "Аккаунт удален" is decided by the name in the address: every endpoint but
 * the profile and the lists of its tabs treats it as a name nobody holds, so
 * none of them is asked and nothing they would fill is drawn.
 */
const isRemovedAccount = computed(() =>
  isRemovedAccountName(usernameParam.value),
);

// The note the viewer keeps about this user, seeded from every load.
const personalNote = useProfileNote(() => usernameParam.value, user);

// The status of a failed load as an ErrorPage code: "не найден" and "попробуйте позже" differ.
const errorCode = ref<number | null>(null);

async function loadProfile(name: Username) {
  errorCode.value = null;
  const error = await communityStore.trySelectProfile(name);
  if (error) errorCode.value = errorCodeForStatus(error.status);
  else {
    personalNote.seedNote();
    await loadEndorsementEligibility(name);
  }
}

/** Asked of the server: the create endpoint has five conditions, two of them knowable here. */
const {
  canCreate: canEndorse,
  ask: askEndorsementEligibility,
  clear: clearEndorsementEligibility,
} = useEndorsementEligibility();

async function loadEndorsementEligibility(name: Username) {
  // A guest, the owner and "Аккаунт удален" are refused by the server too, so they cost no request.
  if (
    !currentUser.value ||
    isSameUsername(currentUser.value.username, name) ||
    isRemovedAccountName(name)
  ) {
    clearEndorsementEligibility();
    return;
  }
  await askEndorsementEligibility(name);
}

useFetchData(
  () => loadProfile(usernameParam.value),
  [
    {
      param: (p) => p.username,
      callback: (name) => loadProfile(name as Username),
    },
  ],
);

// The viewer's answers belong to the session that read them: signing in or out reads the profile again.
watch(
  () => currentUser.value?.username ?? null,
  () => loadProfile(usernameParam.value),
);

const isSystemUser = computed(() => user.value?.role === UserRole.System);

// The loaded name, the address's while loading; a failed load is titled by its error, not by the name.
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
  avatarPreview,
  toggleEditMode,
  setField,
  setAvatar,
  removeAvatar,
  saveChanges,
  cancelEdit,
  pendingChanges,
} = useProfileEdit(toRef(() => usernameParam.value));

/** The owner edits the open profile: another one opened from the edit mode is read. */
const editing = computed(() => canEdit.value && isEditMode.value);

// The edit mode opens on what is stored: the owner's own profile, with the
// visibility and the birthday the public one leaves out and "О себе" as typed.
const ownProfile = ref<PersonalProfile | null>(null);
const aboutSource = ref("");
/** A request of the owner's for another name waits: the profile says so instead of the link. */
const nameChangePending = ref(false);
const editLoading = ref(false);
const editResult = useActionResult();

async function startEdit() {
  if (editLoading.value) return;
  editResult.clear();
  editLoading.value = true;
  const [own, nameChange] = await Promise.all([
    personalApi.getMyProfileForEdit(),
    accountApi.getUsernameChangeRequest(),
  ]);
  editLoading.value = false;
  if (own.error) {
    editResult.refuse(own.error, "Не удалось загрузить профиль");
    return;
  }
  ownProfile.value = own.data;
  aboutSource.value = own.data?.info ?? "";
  // Unknown is no request: the account says what there is.
  nameChangePending.value = nameChange.data?.status === "Pending";
  toggleEditMode();
}

/** The picture drawn: in the edit mode, the one picked or none, until the save. */
const shownPicture = computed<UserPicture | undefined>(() => {
  const change = pendingChanges.value.avatarUploadId;
  if (!editing.value || change === undefined) return user.value?.picture;
  if (change === null) return {};
  return avatarPreview.value ?? {};
});

const hasShownPicture = computed(() => {
  const p = shownPicture.value;
  return !!(p?.originalUrl || p?.mediumUrl || p?.smallUrl);
});

/** Whether a picture is stored, which a reset in the edit mode would take off. */
const hasSavedPicture = computed(() => {
  const p = user.value?.picture;
  return !!(p?.originalUrl || p?.mediumUrl || p?.smallUrl);
});

// What was typed survives a switch of tabs, which draws the editor anew.
const aboutText = computed(
  () => pendingChanges.value.info ?? aboutSource.value,
);

// The address may spell the name in another case, and the server finds the same account by it.
const isOwnProfile = computed(() =>
  isSameUsername(currentUser.value?.username, usernameParam.value),
);

// "Мои обращения" is the owner's; "Загруженное" the owner's or an admin's, as ProfileUploadsPage gates it.
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
    case "showBirthday":
      // Merged: the visibility is one object, and a second flag must not drop the first.
      return setField("visibility", {
        ...pendingChanges.value.visibility,
        showBirthday: value === "true",
      });
    case "showRating":
      return setField("visibility", {
        ...pendingChanges.value.visibility,
        showRating: value === "true",
      });
    case "gender":
      return setField("gender", (value || undefined) as Gender);
    default:
      return setField(field as Parameters<typeof setField>[0], value as never);
  }
}

function onContactsUpdate(contacts: Contact[]) {
  setField("contacts", contacts);
}

const userRoles = computed<string[]>(() => {
  const role = user.value?.role as UserRole | undefined;
  return role && STAFF_ROLES.includes(role)
    ? [ROLE_INFO[role].nicknameSingular].filter(Boolean)
    : [];
});

/** The side of the picture, as $profile-avatar-size in the stylesheet. */
const AVATAR_SIZE = 220;

/** Whether the API sent the picture's own size; without it the slot holds a square. */
const avatarSizeIsKnown = computed(() => {
  const picture = shownPicture.value;
  return !!picture?.originalWidth && !!picture?.originalHeight;
});

const blacklist = useProfileBlacklist(user);
const { blockResult } = blacklist;

/** A signed-in reader on somebody else's profile. */
const viewerOfOther = computed(
  () => !!currentUser.value && !isOwnProfile.value && !isRemovedAccount.value,
);

const profileTabs = useProfileTabs({
  user,
  currentUser,
  usernameParam,
  isRemovedAccount,
});

/** A refused one-press subscribe, under the row of actions. */
const subscribeResult = useActionResult();

onMounted(() => {
  // For the subscribe button, which the profile of "Аккаунт удален" does not have.
  if (viewerOfOther.value) {
    subscriptionsStore.fetchSubscriptions();
  }
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

    <ProfileUsername
      :user="user"
      :editing="editing"
      :name-change-pending="nameChangePending"
    />

    <section class="identity">
      <div class="col col-identity">
        <div
          class="avatar-wrapper"
          :class="{ 'avatar-wrapper-unsized': !avatarSizeIsKnown }"
        >
          <AvatarImg
            :picture="shownPicture"
            :alt="user.username"
            :size="AVATAR_SIZE"
            img-class="avatar"
            prefer-original
            eager
          />
          <ProfilePictureUpload
            v-if="editing"
            :user-id="user.id"
            :has-avatar="hasShownPicture"
            @uploaded="setAvatar"
            @removed="removeAvatar(hasSavedPicture)"
          />
        </div>

        <div v-if="userRoles.length" class="role-line">
          {{ userRoles.join(", ") }}
        </div>

        <!-- In edit mode the status is a captioned field, as the contacts below it. -->
        <FormField v-if="editing" label="Статус" name="profile-status">
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

        <ProfileStats
          :user="user"
          :username="usernameParam"
          :is-removed-account="isRemovedAccount"
        />

        <ProfileViolations v-if="!isRemovedAccount" :username="usernameParam" />

        <ProfileActions
          :user="user"
          :username="usernameParam"
          :can-edit="canEdit"
          :is-edit-mode="editing"
          :is-own-profile="isOwnProfile"
          :viewer-of-other="viewerOfOther"
          :can-endorse="canEndorse"
          :blacklist="blacklist"
          :subscribe-result="subscribeResult"
          @edit="startEdit"
        />
        <ActionResult :result="editResult" />
        <ActionResult :result="blockResult" />
        <ActionResult :result="subscribeResult" />

        <div v-if="showExtraLinks" class="extra-links">
          <span class="extra-links-title">Дополнительные ссылки:</span>
          <!-- The reader's own subscriptions: no other link leads there. -->
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
      :is-edit-mode="editing"
      :own="ownProfile"
      @update-field="onFieldUpdate"
      @update-contacts="onContactsUpdate"
    />

    <ProfileTabs
      :model="profileTabs"
      :user="user"
      :username-param="usernameParam"
      :is-edit-mode="editing"
      :about-source="aboutText"
      :is-removed-account="isRemovedAccount"
      @update-field="onFieldUpdate"
    >
      <template #note>
        <ProfilePersonalNote v-if="viewerOfOther" :model="personalNote" />
      </template>
    </ProfileTabs>

    <ProfileEditBar
      :shown="editing"
      :has-changes="hasChanges"
      :is-saving="isSaving"
      @save="saveChanges"
      @cancel="cancelEdit"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// The room under the last block of the profile.
$room-below: 80px
$system-title-size: 24px
$system-description-width: 500px

// $small between the heading, the identity and "Контакты"; the blocks below add their own $medium.
.profile-page
  display: flex
  flex-direction: column
  gap: $small
  padding-bottom: $room-below

.save-error
  color: $accent-red
  padding: $small 0
  font-size: $font-size

.identity
  display: block
  // Up to the heading: its margin-bottom taken back, the page gap left.
  margin: -$small 0 0

// As wide as the column, so the row of actions stretches across it.
.col-identity
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small

// ProfileViolations inline: $small plus the column gap is the $medium every subgroup stands at.
:deep(.violations-inline)
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: 0
  margin-top: $small

:deep(.profile-personal-info > h2:first-child)
  margin-top: 0

.avatar-wrapper
  position: relative
  width: $profile-avatar-size
  max-width: 100%
  margin-bottom: $small

// A square floor for an upload with no recorded size, so the box does not shrink on decode.
.avatar-wrapper-unsized
  min-height: $profile-avatar-size

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

.status-value
  overflow-wrap: anywhere

.status-input
  min-width: 0

// $medium from the block above and $minor inside, as .endorsement-stats.
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

// No double gap between the tab strip and its panel.
:deep(.tabs)
  margin-bottom: 0

.profile-page.system
  text-align: center
  padding: $big * 2

  .system-title
    margin: 0 0 $medium
    color: $heading
    font-size: $system-title-size

  .system-description
    color: $text-muted
    line-height: 1.6
    max-width: $system-description-width
    margin: 0 auto
</style>
