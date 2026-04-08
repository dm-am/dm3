<template>
  <section class="section">
    <h2 class="section-title">Профиль</h2>

    <div class="profile-content">
      <div class="avatar-section">
        <div class="avatar-wrapper">
          <img
            :src="user.mediumPictureUrl || user.originalPictureUrl"
            :alt="user.username"
            class="avatar"
          />
          <div class="avatar-overlay">
            <span class="upload-label">{{
              uploadingAvatar ? "Загрузка..." : "Изменить"
            }}</span>
            <Upload
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
            <select
              id="gender"
              v-model="profileForm.gender"
              class="form-select"
            >
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
              v-for="contact in profileForm.contacts"
              :key="contact.id"
              class="contact-item"
            >
              <input
                v-model="contact.contactType"
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
              <Tooltip text="Удалить">
                <button
                  type="button"
                  class="remove-contact"
                  @click="removeContact(contact.id)"
                  aria-label="Удалить контакт"
                >
                  ×
                </button>
              </Tooltip>
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
        <Button
          :loading="saveProfileAction.loading.value"
          @click="saveProfile"
          class="save-button"
        >
          Сохранить профиль
        </Button>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import { useUserStore } from "@/entities/user";
import { UploadApi, PersonalApi } from "@/shared/api";
import type { UpdateProfilePayload } from "@/shared/api";
import Button from "@/shared/ui/Button/Button.vue";
import { Upload } from "@/features/upload";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useAsyncAction } from "@/shared/lib/composables/useAsyncAction";
import { useToast } from "@/shared/lib/composables/useToast";
import { Gender, type User } from "@/shared/api/models/community/users";

const props = defineProps<{
  user: User;
}>();

const userStore = useUserStore();
const toast = useToast();

interface ContactForm {
  id: number; // Unique ID for stable v-for keys (PERFORMANCE.md)
  contactType: string;
  value: string;
}

let contactIdCounter = 0;
const createContact = (contactType = "", value = ""): ContactForm => ({
  id: ++contactIdCounter,
  contactType,
  value,
});

const profileForm = ref({
  status: "",
  name: "",
  location: "",
  gender: Gender.Unknown,
  birthdayDate: "",
  info: "",
  contacts: [] as ContactForm[],
});

const uploadingAvatar = ref(false);

// Initialize form from user data
watch(
  () => props.user,
  (currentUser) => {
    if (currentUser) {
      profileForm.value = {
        status: currentUser.status || "",
        name: currentUser.name || "",
        location: currentUser.location || "",
        gender: currentUser.gender || Gender.Unknown,
        birthdayDate: currentUser.birthdayDate || "",
        info: currentUser.info?.source || "",
        contacts: (currentUser.contacts ?? []).map((c) =>
          createContact(c.contactType, c.value)
        ),
      };
    }
  },
  { immediate: true },
);

// Contacts management
const addContact = () => {
  if (profileForm.value.contacts.length < 10) {
    profileForm.value.contacts.push(createContact());
  }
};

const removeContact = (id: number) => {
  const index = profileForm.value.contacts.findIndex((c) => c.id === id);
  if (index !== -1) profileForm.value.contacts.splice(index, 1);
};

// Avatar upload
const handleAvatarUpload = async (formData: FormData) => {
  const file = formData.get("file") as File | null;
  if (!file) return;

  uploadingAvatar.value = true;
  try {
    const { data: uploadData, error: uploadError } =
      await UploadApi.directUpload(file, "UserAvatar");
    if (uploadError || !uploadData) {
      toast.error("Не удалось загрузить аватар");
      return;
    }

    const { error: profileError } = await PersonalApi.updateMyProfile({
      avatarUploadId: uploadData.id,
    });
    if (profileError) {
      toast.error("Не удалось обновить профиль");
      return;
    }

    await userStore.fetchUser();
    toast.success("Аватар успешно обновлен");
  } catch {
    toast.error("Не удалось загрузить аватар");
  } finally {
    uploadingAvatar.value = false;
  }
};

// Save profile
const saveProfileAction = useAsyncAction();

const saveProfile = () => {
  saveProfileAction.execute(async () => {
    const payload: UpdateProfilePayload = {
      status: profileForm.value.status,
      name: profileForm.value.name,
      location: profileForm.value.location,
      info: profileForm.value.info,
      contacts: profileForm.value.contacts
        .filter((c) => c.contactType.trim() && c.value.trim())
        .map((c) => ({
          contactType: c.contactType,
          value: c.value,
        })),
    };

    const { error } = await PersonalApi.updateMyProfile(payload);
    if (error) throw new Error("Не удалось сохранить профиль");
    await userStore.fetchUser();
    toast.success("Профиль успешно обновлен");
  });
};
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"
@import "../AccountPage.styles"

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

@media (max-width: 768px)
  .profile-content
    flex-direction: column

  .avatar-section
    align-self: center

  .contact-item
    grid-template-columns: 1fr
    gap: $tiny

    .remove-contact
      justify-self: flex-start
</style>
