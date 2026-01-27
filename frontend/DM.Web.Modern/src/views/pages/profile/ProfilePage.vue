<script setup lang="ts">
import { useRoute } from "vue-router";
import { useCommunityStore } from "@/stores/community";
import { storeToRefs } from "pinia";
import { computed, ref, watch } from "vue";
import { type UserLogin, UserRole, Gender } from "@/api/models/community";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import ProfileStat from "@/views/pages/profile/ProfileStat.vue";
import ProfilePicture from "@/views/pages/profile/ProfilePicture.vue";
import TheButton from "@/components/inputs/TheButton.vue";
import { useUserStore } from "@/stores";
import { useFetchData } from "@/composables/useFetchData";
import defaultPicture from "@/assets/images/userpic.png";
import dayjs from "dayjs";
import { ROLE_INFO, STAFF_ROLES } from "@/constants/roles";

const route = useRoute();
const { user: currentUser } = storeToRefs(useUserStore());
const communityStore = useCommunityStore();
const { selectedUser: user } = storeToRefs(communityStore);
const { trySelectProfile, updateUser } = communityStore;

const userRoles = computed(() => {
  const roles =
    user.value?.roles
      .filter((r) => STAFF_ROLES.includes(r as UserRole))
      .map((r) => ROLE_INFO[r as UserRole].nickname)
      .filter(Boolean) || [];
  if (user.value?.isHonorary) {
    roles.push("Почётный гоблин");
  }
  return roles;
});
const isCurrentUser = computed(() => {
  const currentLogin = currentUser.value?.login;
  const profileLogin = user.value?.login;
  return !!(currentLogin && profileLogin && currentLogin === profileLogin);
});
const pictureUrl = computed(
  () => user.value?.originalPictureUrl || defaultPicture,
);

const isEditingProfile = ref(false);
const isSaving = ref(false);
const editedStatus = ref("");
const editedName = ref("");
const editedLocation = ref("");
const editedSkype = ref("");
const editedInfo = ref("");

const userSkype = computed(
  () => user.value?.contacts?.find((c) => c.title === "Skype")?.value || "",
);

const userIcq = computed(
  () => user.value?.contacts?.find((c) => c.title === "ICQ")?.value || "",
);

const userRegistration = computed(() => {
  if (!user.value?.registrationDateUtc) return "";
  return dayjs(user.value.registrationDateUtc).format("DD.MM.YYYY HH:mm");
});

const userOnline = computed(() => {
  if (!user.value?.onlineUtc) return "";
  return dayjs(user.value.onlineUtc).format("DD.MM.YYYY HH:mm");
});

const genderNames: Record<Gender, string> = {
  [Gender.Unknown]: "",
  [Gender.Male]: "Мужской",
  [Gender.Female]: "Женский",
};

const userGender = computed(() =>
  user.value?.gender ? genderNames[user.value.gender as Gender] : "",
);

const userBirthday = computed(() => {
  if (!user.value?.birthdayDate) return "";
  return dayjs(user.value.birthdayDate).format("DD.MM");
});

const startEditProfile = () => {
  if (!user.value) return;
  editedStatus.value = user.value.status || "";
  editedName.value = user.value.name || "";
  editedLocation.value = user.value.location || "";
  editedSkype.value = userSkype.value;
  editedInfo.value = user.value.info || "";
  isEditingProfile.value = true;
};

const cancelEditProfile = () => {
  isEditingProfile.value = false;
};

const saveProfile = async () => {
  if (!user.value) return;
  isSaving.value = true;
  await updateUser(user.value.login, {
    status: editedStatus.value,
    name: editedName.value,
    location: editedLocation.value,
    skype: editedSkype.value,
    info: editedInfo.value,
  });
  isSaving.value = false;
  isEditingProfile.value = false;
};

watch(
  () => user.value?.login,
  () => {
    isEditingProfile.value = false;
  },
);

useFetchData(
  () => trySelectProfile(route.params.login as UserLogin),
  [
    {
      param: (p) => p.login,
      callback: (login) => trySelectProfile(login as UserLogin),
    },
  ],
);
</script>

<template>
  <template v-if="user">
    <page-title class="profile_title">{{ route.params.login }}</page-title>
    <secondary-text class="profile_roles">{{
      userRoles!.join(", ")
    }}</secondary-text>

    <div class="profile_container">
      <div class="profile_short-info">
        <div class="profile_short-info_picture-wrapper">
          <img
            :src="pictureUrl"
            :alt="user.login"
            class="profile_short-info_picture"
          />
          <profile-picture v-if="isCurrentUser" :login="user.login" />
        </div>

        <template v-if="isEditingProfile">
          <profile-stat
            title="Статус"
            empty="Не указан"
            v-model="editedStatus"
            :editable="true"
          />
          <profile-stat
            title="Имя"
            empty="Не указано"
            v-model="editedName"
            :editable="true"
          />
          <profile-stat
            title="Местоположение"
            empty="Не указано"
            v-model="editedLocation"
            :editable="true"
          />
          <profile-stat
            title="Skype"
            empty="Не указан"
            v-model="editedSkype"
            :editable="true"
          />
          <div class="profile-edit-section">
            <h4>О себе</h4>
            <textarea
              v-model="editedInfo"
              class="profile-info-edit"
              placeholder="Расскажите о себе..."
            />
          </div>
          <div class="profile-edit-actions">
            <the-button :loading="isSaving" @click="saveProfile">
              Сохранить
            </the-button>
            <the-button :disabled="isSaving" @click="cancelEditProfile">
              Отмена
            </the-button>
          </div>
        </template>

        <template v-else>
          <profile-stat
            title="Статус"
            empty="Не указан"
            v-model="user.status"
          />
          <profile-stat title="Имя" empty="Не указано" v-model="user.name" />
          <profile-stat
            title="Местоположение"
            empty="Не указано"
            v-model="user.location"
          />
          <profile-stat
            title="Пол"
            empty="Не указан"
            :modelValue="userGender"
          />
          <profile-stat
            title="День рождения"
            empty="Не указан"
            :modelValue="userBirthday"
          />
          <profile-stat
            title="Skype"
            empty="Не указан"
            :modelValue="userSkype"
          />
          <profile-stat v-if="userIcq" title="ICQ" :modelValue="userIcq" />
          <profile-stat title="Регистрация" :modelValue="userRegistration" />
          <profile-stat title="Был(а) онлайн" :modelValue="userOnline" />
          <the-button
            v-if="isCurrentUser"
            @click="startEditProfile"
            class="edit-profile-btn"
          >
            Редактировать профиль
          </the-button>
          <router-link
            v-else-if="currentUser"
            :to="{ name: 'direct-message', params: { login: user.login } }"
            class="message-link"
          >
            <the-button>Написать сообщение</the-button>
          </router-link>
        </template>
      </div>
      <div class="profile_content">
        <nav>
          <router-link
            class="tabs-link"
            :to="{ name: 'profile', params: route.params }"
            >Информация</router-link
          >
          <router-link
            class="tabs-link"
            :to="{ name: 'user-games', params: route.params }"
            >Игры</router-link
          >
          <router-link
            class="tabs-link"
            :to="{ name: 'user-characters', params: route.params }"
            >Персонажи</router-link
          >
          <router-link
            v-if="isCurrentUser"
            class="tabs-link"
            :to="{ name: 'user-settings', params: route.params }"
            >Настройки</router-link
          >
        </nav>
        <router-view />
      </div>
    </div>
  </template>

  <the-loader v-else :big="true" />
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile_title
  display: inline-block
.profile_roles
  display: inline-block
  margin-left: $small

.profile_container
  display: flex

.profile_short-info
  width: $grid-step * 40

.profile_short-info_picture-wrapper
  position: relative
  display: inline-block

.profile_short-info_picture
  width: 100%
  max-height: $grid-step * 200
  border-radius: $border-radius
  display: block

.profile_content
  margin-left: $big

nav
  margin-bottom: $small
  & a
    display: inline-block
    margin-right: $medium
    text-transform: uppercase
    font-weight: bold
    color: $link-nav
    text-decoration: none

    &:hover
      color: $link-nav-hover
      text-decoration: underline

    &.router-link-exact-active
      color: $text
      text-decoration: none
      cursor: default

.profile-edit-actions
  display: flex
  gap: $small
  margin-top: $small

.edit-profile-btn
  margin-top: $small

.message-link
  display: inline-block
  margin-top: $small
  text-decoration: none

.profile-edit-section
  margin-top: $small

  h4
    margin-bottom: $tiny
    font-size: $secondary-font-size
    color: $text-muted

.profile-info-edit
  width: 100%
  min-height: $grid-step * 25
  padding: $small
  box-sizing: border-box
  border-radius: $border-radius
  font-family: inherit
  font-size: inherit
  resize: vertical
  background-color: $input-bg
  border: 1px dashed $border
  color: $text

  &:focus
    outline: none
    border-style: solid
    border-color: $button-border-hover
</style>
