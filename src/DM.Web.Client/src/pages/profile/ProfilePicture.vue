<script setup lang="ts">
import { ref, computed, onUnmounted } from "vue";
import type { AxiosProgressEvent } from "axios";
import { personalApi } from "@/shared/api";
import { uploadApi } from "@/shared/api";
import { TheUpload } from "@/features/upload";
let resetTimeout: ReturnType<typeof setTimeout> | null = null;

onUnmounted(() => {
  if (resetTimeout) clearTimeout(resetTimeout);
});

type UploadState = "idle" | "uploading" | "success" | "error";
const uploadState = ref<UploadState>("idle");
const progress = ref(0);
const errorMessage = ref("");

const stateLabel = computed(() => {
  switch (uploadState.value) {
    case "idle":
      return "Загрузить фото";
    case "uploading":
      return `${progress.value}%`;
    case "success":
      return "Готово!";
    case "error":
      return errorMessage.value || "Ошибка";
    default:
      return "Загрузить фото";
  }
});

const onProgress = (e: AxiosProgressEvent) => {
  if (e.total) {
    progress.value = Math.round((e.loaded / e.total) * 100);
  }
};

const onUploading = async (formData: FormData) => {
  const file = formData.get("file") as File | null;
  if (!file) return;

  // Client-side validation
  const maxSize = 10 * 1024 * 1024; // 10 MB
  if (file.size > maxSize) {
    uploadState.value = "error";
    errorMessage.value = "Макс. 10 МБ";
    resetTimeout = setTimeout(() => {
      uploadState.value = "idle";
    }, 3000);
    return;
  }

  uploadState.value = "uploading";
  progress.value = 0;

  // Step 1: Upload through Common Upload system
  const { data: uploadData, error: uploadError } = await uploadApi.directUpload(
    file,
    "UserAvatar",
    { onProgress },
  );

  if (uploadError || !uploadData) {
    uploadState.value = "error";
    errorMessage.value = "Ошибка загрузки";
    resetTimeout = setTimeout(() => {
      uploadState.value = "idle";
    }, 2000);
    return;
  }

  // Step 2: Attach to profile
  const { error: profileError } = await personalApi.updateMyProfile({
    avatarUploadId: uploadData.id,
  });

  if (profileError) {
    uploadState.value = "error";
    errorMessage.value = "Ошибка профиля";
    resetTimeout = setTimeout(() => {
      uploadState.value = "idle";
    }, 2000);
    return;
  }

  uploadState.value = "success";
  resetTimeout = setTimeout(() => {
    uploadState.value = "idle";
  }, 1500);
};
</script>

<template>
  <div class="profile-picture-upload">
    <div class="upload-overlay">
      <span class="upload-label">{{ stateLabel }}</span>
      <the-upload
        v-if="uploadState === 'idle'"
        accept="image/jpeg,image/png,image/webp,image/gif"
        @uploading="onUploading"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-picture-upload
  position: absolute
  top: 0
  left: 0
  right: 0
  bottom: 0
  opacity: 0
  transition: opacity 0.2s ease

  &:hover
    opacity: 1

.upload-overlay
  position: absolute
  top: 0
  left: 0
  right: 0
  bottom: 0
  background-color: $shade-bg
  display: flex
  align-items: center
  justify-content: center
  border-radius: $border-radius
  cursor: pointer

.upload-label
  color: $shade-text
  font-weight: bold
  text-transform: uppercase
  pointer-events: none
</style>
