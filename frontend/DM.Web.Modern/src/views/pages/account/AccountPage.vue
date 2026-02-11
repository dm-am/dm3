<template>
  <page-title>Настройки аккаунта</page-title>

  <div v-if="user" class="account-page">
    <!-- Section 1: Profile -->
    <section class="section">
      <h2 class="section-title">Профиль</h2>

      <div class="profile-content">
        <div class="avatar-section">
          <div class="avatar-wrapper">
            <img
              :src="user.mediumPictureUrl || user.originalPictureUrl"
              :alt="user.login"
              class="avatar"
            />
            <div class="avatar-overlay">
              <span class="upload-label">{{
                uploadingAvatar ? "Загрузка..." : "Изменить"
              }}</span>
              <the-upload
                v-if="!uploadingAvatar"
                accept="image/jpeg,image/png,image/webp,image/gif"
                @uploading="handleAvatarUpload"
              />
            </div>
          </div>
        </div>

        <div class="profile-form">
          <div class="form-group">
            <label for="status" class="form-label">Статус</label>
            <input
              id="status"
              v-model="profileForm.status"
              type="text"
              class="form-input"
              maxlength="100"
            />
          </div>

          <div class="form-group">
            <label for="name" class="form-label">Имя</label>
            <input
              id="name"
              v-model="profileForm.name"
              type="text"
              class="form-input"
              maxlength="100"
            />
          </div>

          <div class="form-group">
            <label for="location" class="form-label">Местоположение</label>
            <input
              id="location"
              v-model="profileForm.location"
              type="text"
              class="form-input"
              maxlength="100"
            />
          </div>

          <div class="form-row">
            <div class="form-group">
              <label for="gender" class="form-label">Пол</label>
              <select id="gender" v-model="profileForm.gender" class="form-select">
                <option value="Unknown">Не указан</option>
                <option value="Male">Мужской</option>
                <option value="Female">Женский</option>
              </select>
            </div>

            <div class="form-group">
              <label for="birthday" class="form-label">Дата рождения</label>
              <input
                id="birthday"
                v-model="profileForm.birthdayDate"
                type="date"
                class="form-input"
              />
            </div>
          </div>

          <div class="form-group">
            <label for="info" class="form-label">О себе</label>
            <textarea
              id="info"
              v-model="profileForm.info"
              class="form-textarea"
              rows="6"
              placeholder="Поддерживается BBCode"
            ></textarea>
          </div>

          <div class="form-group">
            <label class="form-label">Контакты</label>
            <div class="contacts-list">
              <div
                v-for="(contact, index) in profileForm.contacts"
                :key="index"
                class="contact-item"
              >
                <input
                  v-model="contact.title"
                  type="text"
                  class="form-input contact-title"
                  placeholder="Название"
                  maxlength="50"
                />
                <input
                  v-model="contact.value"
                  type="text"
                  class="form-input contact-value"
                  placeholder="Значение"
                  maxlength="200"
                />
                <button
                  type="button"
                  class="remove-contact"
                  @click="removeContact(index)"
                  title="Удалить"
                >
                  ×
                </button>
              </div>
            </div>
            <button
              v-if="profileForm.contacts.length < 10"
              type="button"
              class="add-contact"
              @click="addContact"
            >
              + Добавить контакт
            </button>
          </div>

          <span v-if="saveProfileAction.error.value" class="error-text">
            {{ saveProfileAction.error.value }}
          </span>
          <TheButton
            :loading="saveProfileAction.loading.value"
            @click="saveProfile"
            class="save-button"
          >
            Сохранить профиль
          </TheButton>
        </div>
      </div>
    </section>

    <!-- Section 2: Security -->
    <section class="section">
      <h2 class="section-title">Безопасность</h2>

      <div class="security-content">
        <!-- Email Change -->
        <div class="security-block">
          <h3 class="subsection-title">Смена электронной почты</h3>
          <div class="current-value">
            Текущая почта: <strong>{{ user.email || "не указана" }}</strong>
          </div>

          <div class="form-group">
            <label for="new-email" class="form-label">Новая почта</label>
            <input
              id="new-email"
              v-model="emailForm.newEmail"
              type="email"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <label for="email-password" class="form-label"
              >Пароль для подтверждения</label
            >
            <input
              id="email-password"
              v-model="emailForm.password"
              type="password"
              class="form-input"
            />
          </div>

          <span v-if="changeEmailAction.error.value" class="error-text">
            {{ changeEmailAction.error.value }}
          </span>
          <TheButton
            :loading="changeEmailAction.loading.value"
            :disabled="!emailForm.newEmail || !emailForm.password"
            @click="changeEmail"
            class="save-button"
          >
            Изменить почту
          </TheButton>
        </div>

        <!-- Password Change -->
        <div class="security-block">
          <h3 class="subsection-title">Смена пароля</h3>

          <div class="form-group">
            <label for="old-password" class="form-label">Текущий пароль</label>
            <input
              id="old-password"
              v-model="passwordForm.oldPassword"
              type="password"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <label for="new-password" class="form-label">Новый пароль</label>
            <input
              id="new-password"
              v-model="passwordForm.newPassword"
              type="password"
              class="form-input"
              @blur="onNewPasswordBlur"
            />
            <div class="password-hint">
              <span v-if="hibpChecking" class="hibp-checking">Проверка...</span>
              <span v-else-if="isCompromised && !hibpWarningDismissed" class="hibp-warning">
                Пароль найден в утечках данных.
                <a href="#" @click.prevent="dismissHibpWarning">Использовать</a>
              </span>
              <span v-else class="hint-text">Минимум 8 символов</span>
            </div>
            <PasswordStrengthIndicator
              v-if="passwordForm.newPassword"
              :password="passwordForm.newPassword"
            />
          </div>

          <div class="form-group">
            <label for="confirm-password" class="form-label"
              >Подтвердите новый пароль</label
            >
            <input
              id="confirm-password"
              v-model="passwordForm.confirmPassword"
              type="password"
              class="form-input"
              :class="{
                'input-error':
                  passwordForm.confirmPassword &&
                  passwordForm.newPassword !== passwordForm.confirmPassword
              }"
            />
            <span
              v-if="
                passwordForm.confirmPassword &&
                passwordForm.newPassword !== passwordForm.confirmPassword
              "
              class="error-text"
            >
              Пароли не совпадают
            </span>
          </div>

          <span v-if="changePasswordAction.error.value" class="error-text">
            {{ changePasswordAction.error.value }}
          </span>
          <TheButton
            :loading="changePasswordAction.loading.value"
            :disabled="!isPasswordFormValid"
            @click="changePassword"
            class="save-button"
          >
            Изменить пароль
          </TheButton>
        </div>
      </div>
    </section>

    <!-- Section 3: Sessions -->
    <section class="section">
      <h2 class="section-title">Сессии</h2>

      <div class="sessions-content">
        <p class="description">
          Завершите все активные сессии на других устройствах. Это полезно, если вы
          подозреваете, что кто-то получил доступ к вашему аккаунту.
        </p>

        <span v-if="logoutAllAction.error.value" class="error-text">
          {{ logoutAllAction.error.value }}
        </span>
        <TheButton
          :loading="logoutAllAction.loading.value"
          @click="logoutFromAll"
        >
          Выйти со всех других устройств
        </TheButton>
      </div>
    </section>

    <!-- Section 4: Settings -->
    <section class="section">
      <h2 class="section-title">Настройки</h2>

      <div class="settings-content">
        <div class="form-group">
          <label for="color-schema" class="form-label">Цветовая схема</label>
          <select
            id="color-schema"
            v-model="settingsForm.colorSchema"
            class="form-select"
          >
            <option value="Light">Светлая</option>
            <option value="Dark">Темная</option>
          </select>
        </div>

        <div class="form-group">
          <label class="form-label">Pagination настройки</label>
          <div class="pagination-grid">
            <div class="pagination-item">
              <label for="posts-per-page" class="pagination-label"
                >Постов на странице</label
              >
              <input
                id="posts-per-page"
                v-model.number="settingsForm.pagingLimits.postsPerPage"
                type="number"
                min="10"
                max="100"
                class="form-input"
              />
            </div>

            <div class="pagination-item">
              <label for="comments-per-page" class="pagination-label"
                >Комментариев на странице</label
              >
              <input
                id="comments-per-page"
                v-model.number="settingsForm.pagingLimits.commentsPerPage"
                type="number"
                min="10"
                max="100"
                class="form-input"
              />
            </div>

            <div class="pagination-item">
              <label for="topics-per-page" class="pagination-label"
                >Тем на странице</label
              >
              <input
                id="topics-per-page"
                v-model.number="settingsForm.pagingLimits.topicsPerPage"
                type="number"
                min="10"
                max="100"
                class="form-input"
              />
            </div>

            <div class="pagination-item">
              <label for="messages-per-page" class="pagination-label"
                >Сообщений на странице</label
              >
              <input
                id="messages-per-page"
                v-model.number="settingsForm.pagingLimits.messagesPerPage"
                type="number"
                min="10"
                max="100"
                class="form-input"
              />
            </div>

            <div class="pagination-item">
              <label for="entities-per-page" class="pagination-label"
                >Сущностей на странице</label
              >
              <input
                id="entities-per-page"
                v-model.number="settingsForm.pagingLimits.entitiesPerPage"
                type="number"
                min="10"
                max="100"
                class="form-input"
              />
            </div>
          </div>
        </div>

        <div v-if="canUseMentorGreeting" class="form-group">
          <label for="mentor-greeting" class="form-label"
            >Приветственное сообщение для новичков</label
          >
          <textarea
            id="mentor-greeting"
            v-model="settingsForm.mentorGreetingsMessage"
            class="form-textarea"
            rows="4"
            placeholder="Поддерживается BBCode"
          ></textarea>
        </div>

        <span v-if="saveSettingsAction.error.value" class="error-text">
          {{ saveSettingsAction.error.value }}
        </span>
        <TheButton
          :loading="saveSettingsAction.loading.value"
          @click="saveSettings"
          class="save-button"
        >
          Сохранить настройки
        </TheButton>
      </div>
    </section>

    <!-- Section 5: Login Change -->
    <section class="section">
      <h2 class="section-title">Смена имени пользователя</h2>

      <div class="login-content">
        <div class="current-value">
          Текущее имя пользователя: <strong>{{ user.login }}</strong>
        </div>
        <p class="description muted">
          Для смены имени пользователя обратитесь к администрации
        </p>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { useUserStore } from "@/stores";
import { useCommunityStore } from "@/stores/community";
import accountApi from "@/api/requests/accountApi";
import uploadApi from "@/api/requests/uploadApi";
import TheButton from "@/components/inputs/TheButton.vue";
import TheUpload from "@/components/inputs/TheUpload.vue";
import PasswordStrengthIndicator from "@/components/inputs/PasswordStrengthIndicator.vue";
import { useAsyncAction } from "@/composables/useAsyncAction";
import { useToast } from "@/composables/useToast";
import { useHibpCheck } from "@/composables/useHibpCheck";
import type { Gender } from "@/api/models/community/users";
import { ColorSchema } from "@/api/models/community/user-settings";
import type { UpdateUserPayload } from "@/stores/community";

const userStore = useUserStore();
const communityStore = useCommunityStore();
const toast = useToast();

const user = computed(() => userStore.user);

// Profile form
interface ContactForm {
  title: string;
  value: string;
}

const profileForm = ref({
  status: "",
  name: "",
  location: "",
  gender: "Unknown" as Gender,
  birthdayDate: "",
  info: "",
  contacts: [] as ContactForm[]
});

const uploadingAvatar = ref(false);

// Email form
const emailForm = ref({
  newEmail: "",
  password: ""
});

// Password form
const passwordForm = ref({
  oldPassword: "",
  newPassword: "",
  confirmPassword: ""
});
const hibpWarningDismissed = ref(false);

// HIBP password check
const { isCompromised, isChecking: hibpChecking, checkPassword } = useHibpCheck();

const onNewPasswordBlur = async () => {
  if (passwordForm.value.newPassword && passwordForm.value.newPassword.length >= 8) {
    hibpWarningDismissed.value = false;
    await checkPassword(passwordForm.value.newPassword);
  }
};

const dismissHibpWarning = () => {
  hibpWarningDismissed.value = true;
};

// Settings form
const settingsForm = ref({
  colorSchema: ColorSchema.Light as ColorSchema,
  mentorGreetingsMessage: "",
  pagingLimits: {
    postsPerPage: 50,
    commentsPerPage: 50,
    topicsPerPage: 50,
    messagesPerPage: 50,
    entitiesPerPage: 50
  }
});

// Initialize forms from user data
watch(
  user,
  (currentUser) => {
    if (currentUser) {
      profileForm.value = {
        status: currentUser.status || "",
        name: currentUser.name || "",
        location: currentUser.location || "",
        gender: currentUser.gender || "Unknown",
        birthdayDate: currentUser.birthdayDate || "",
        info: currentUser.info || "",
        contacts: currentUser.contacts.map((c) => ({
          title: c.title,
          value: c.value
        }))
      };

      settingsForm.value = {
        colorSchema: currentUser.settings.colorSchema || "Light",
        mentorGreetingsMessage:
          currentUser.settings.mentorGreetingsMessage || "",
        pagingLimits: currentUser.settings.pagingLimits || {
          postsPerPage: 50,
          commentsPerPage: 50,
          topicsPerPage: 50,
          messagesPerPage: 50,
          entitiesPerPage: 50
        }
      };
    }
  },
  { immediate: true }
);

// Computed
const canUseMentorGreeting = computed(() => {
  if (!user.value) return false;
  const mentorRoles = ["Admin", "SeniorModerator", "Mentor"];
  return user.value.roles.some((role) => mentorRoles.includes(role));
});

const isPasswordFormValid = computed(() => {
  return (
    passwordForm.value.oldPassword &&
    passwordForm.value.newPassword &&
    passwordForm.value.newPassword.length >= 8 &&
    passwordForm.value.newPassword === passwordForm.value.confirmPassword
  );
});

// Contacts management
const addContact = () => {
  if (profileForm.value.contacts.length < 10) {
    profileForm.value.contacts.push({ title: "", value: "" });
  }
};

const removeContact = (index: number) => {
  profileForm.value.contacts.splice(index, 1);
};

// Avatar upload
const handleAvatarUpload = async (formData: FormData) => {
  const file = formData.get("file") as File | null;
  if (!file || !user.value) return;

  uploadingAvatar.value = true;
  try {
    const { data: uploadData, error: uploadError } = await uploadApi.directUpload(file, "UserAvatar");
    if (uploadError || !uploadData) {
      toast.error("Не удалось загрузить аватар");
      return;
    }

    const { error: profileError } = await communityStore.updateUser(user.value.login, {
      avatarUploadId: uploadData.resource.id
    });
    if (profileError) {
      toast.error("Не удалось обновить профиль");
      return;
    }

    await userStore.fetchUser();
    toast.success("Аватар успешно обновлен");
  } catch (e) {
    toast.error("Не удалось загрузить аватар");
  } finally {
    uploadingAvatar.value = false;
  }
};

// Save profile
const saveProfileAction = useAsyncAction();

const saveProfile = () => {
  saveProfileAction.execute(async () => {
    if (!user.value) return;

    const payload: UpdateUserPayload = {
      status: profileForm.value.status,
      name: profileForm.value.name,
      location: profileForm.value.location,
      info: profileForm.value.info,
      contacts: profileForm.value.contacts
        .filter((c) => c.title.trim() && c.value.trim())
        .map((c, index) => ({
          contactType: c.title,
          contactValue: c.value,
          sortOrder: index
        }))
    };

    const { error } = await communityStore.updateUser(user.value.login, payload);
    if (error) throw new Error("Не удалось сохранить профиль");
    await userStore.fetchUser();
    toast.success("Профиль успешно обновлен");
  });
};

// Change email
const changeEmailAction = useAsyncAction();

const changeEmail = () => {
  changeEmailAction.execute(async () => {
    if (!user.value || !emailForm.value.newEmail || !emailForm.value.password)
      return;

    const { error } = await accountApi.changeEmail({
      login: user.value.login,
      password: emailForm.value.password,
      email: emailForm.value.newEmail
    });
    if (error) throw new Error("Не удалось изменить почту");

    emailForm.value = { newEmail: "", password: "" };
    await userStore.fetchUser();
    toast.success(
      "На новую почту отправлено письмо с подтверждением."
    );
  });
};

// Change password
const changePasswordAction = useAsyncAction();

const changePassword = () => {
  changePasswordAction.execute(async () => {
    if (!user.value || !isPasswordFormValid.value) return;

    const { error } = await accountApi.changePassword({
      login: user.value.login,
      oldPassword: passwordForm.value.oldPassword,
      newPassword: passwordForm.value.newPassword
    });
    if (error) throw new Error("Не удалось изменить пароль");

    passwordForm.value = {
      oldPassword: "",
      newPassword: "",
      confirmPassword: ""
    };
    toast.success("Пароль успешно изменен");
  });
};

// Logout from all devices
const logoutAllAction = useAsyncAction();

const logoutFromAll = () => {
  logoutAllAction.execute(async () => {
    const { error } = await accountApi.logoutAll();
    if (error) throw new Error("Не удалось завершить сессии");
    toast.success("Вы вышли со всех других устройств");
  });
};

// Save settings
const saveSettingsAction = useAsyncAction();

const saveSettings = () => {
  saveSettingsAction.execute(async () => {
    if (!user.value) return;

    const payload: UpdateUserPayload = {
      settings: {
        colorSchema: settingsForm.value.colorSchema,
        mentorGreetingsMessage: settingsForm.value.mentorGreetingsMessage,
        pagingLimits: settingsForm.value.pagingLimits
      }
    };

    const { error } = await communityStore.updateUser(user.value.login, payload);
    if (error) throw new Error("Не удалось сохранить настройки");
    await userStore.fetchUser();
    toast.success("Настройки успешно сохранены");
  });
};
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.account-page
  max-width: 800px
  margin: 0 auto

.section
  padding: $medium 0
  border-bottom: 1px solid $border

  &:last-child
    border-bottom: none

.section-title
  margin: 0 0 $medium 0
  font-size: 1.5rem
  color: $text

.subsection-title
  margin: 0 0 $small 0
  font-size: 1.1rem
  color: $text

.profile-content
  display: flex
  gap: $medium
  flex-wrap: wrap

.avatar-section
  flex-shrink: 0

.avatar-wrapper
  position: relative
  width: 160px
  height: 160px
  border-radius: $border-radius
  overflow: hidden

  &:hover .avatar-overlay
    opacity: 1

.avatar
  width: 100%
  height: 100%
  object-fit: cover
  display: block

.avatar-overlay
  position: absolute
  top: 0
  left: 0
  right: 0
  bottom: 0
  background-color: $shade-bg
  display: flex
  align-items: center
  justify-content: center
  opacity: 0
  transition: opacity 0.2s

.upload-label
  color: $shade-text
  font-weight: 500
  cursor: pointer

.profile-form
  flex: 1
  min-width: 300px

.form-group
  margin-bottom: $medium

.form-label
  display: block
  margin-bottom: $minor
  color: $text
  font-weight: 500

.pagination-label
  display: block
  margin-bottom: $tiny
  color: $text-muted
  font-size: $secondary-font-size

.form-input,
.form-select
  @include input-base
  width: 100%
  border-radius: $border-radius
  font-size: 1rem

  &:focus
    outline: none
    border-color: $link

.form-textarea
  @include input-base
  width: 100%
  border-radius: $border-radius
  font-size: 1rem
  resize: vertical
  font-family: inherit

  &:focus
    outline: none
    border-color: $link

.input-error
  border-color: $accent-red !important

.error-text
  display: block
  margin-top: $tiny
  color: $accent-red
  font-size: $secondary-font-size

.password-hint
  margin-top: $tiny
  font-size: $secondary-font-size

  .hint-text
    color: $text-muted

.hibp-checking
  color: $text-muted
  font-style: italic

.hibp-warning
  color: $text-muted
  a
    color: $link
    margin-left: $tiny
    &:hover
      text-decoration: underline

.form-row
  display: grid
  grid-template-columns: 1fr 1fr
  gap: $small

.contacts-list
  display: flex
  flex-direction: column
  gap: $small

.contact-item
  display: grid
  grid-template-columns: 1fr 2fr auto
  gap: $small
  align-items: center

.contact-title,
.contact-value
  margin: 0

.remove-contact
  padding: $minor $small
  background-color: $accent-red
  color: white
  border: none
  border-radius: $border-radius
  cursor: pointer
  font-size: 1.2rem
  line-height: 1
  width: 32px
  height: 32px
  display: flex
  align-items: center
  justify-content: center

  &:hover
    opacity: 0.8

.add-contact
  padding: $minor $small
  background-color: $bg-element-accent
  color: $text
  border: 1px dashed $border
  border-radius: $border-radius
  cursor: pointer
  font-size: 0.9rem

  &:hover
    background-color: $bg-element-hover

.save-button
  margin-top: $small

.security-content
  display: flex
  flex-direction: column
  gap: $big

.security-block
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.current-value
  margin-bottom: $medium
  color: $text-muted

  strong
    color: $text

.sessions-content,
.login-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.description
  margin: 0 0 $medium 0
  color: $text
  line-height: 1.5

  &.muted
    color: $text-muted

.settings-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.pagination-grid
  display: grid
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr))
  gap: $small

.pagination-item
  display: flex
  flex-direction: column

@media (max-width: 768px)
  .profile-content
    flex-direction: column

  .avatar-section
    align-self: center

  .form-row
    grid-template-columns: 1fr

  .contact-item
    grid-template-columns: 1fr
    gap: $tiny

    .remove-contact
      justify-self: flex-start

  .pagination-grid
    grid-template-columns: 1fr
</style>
