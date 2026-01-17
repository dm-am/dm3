<script setup lang="ts">
import { ref, computed, onUnmounted } from "vue";
import type { AxiosProgressEvent } from "axios";
import type { UserLogin } from "@/api/models/community";
import { useCommunityStore } from "@/stores/community";
import TheUpload from "@/components/inputs/TheUpload.vue";

const props = defineProps<{
  login: UserLogin;
}>();

const communityStore = useCommunityStore();
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
  }
});

const onProgress = (e: AxiosProgressEvent) => {
  if (e.total) {
    progress.value = Math.round((e.loaded / e.total) * 100);
  }
};

const onUploading = async (formData: FormData) => {
  uploadState.value = "uploading";
  progress.value = 0;

  const { error } = await communityStore.uploadPicture(
    props.login,
    formData,
    onProgress
  );

  if (error) {
    uploadState.value = "error";
    errorMessage.value = "Ошибка загрузки";
    resetTimeout = setTimeout(() => {
      uploadState.value = "idle";
    }, 2000);
  } else {
    uploadState.value = "success";
    resetTimeout = setTimeout(() => {
      uploadState.value = "idle";
    }, 1500);
  }
};
</script>

<template>
  <div class="profile-picture-upload">
    <div class="upload-overlay">
      <span class="upload-label">{{ stateLabel }}</span>
      <the-upload v-if="uploadState === 'idle'" @uploading="onUploading" />
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

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
  background: rgba(0, 0, 0, 0.6)
  display: flex
  align-items: center
  justify-content: center
  border-radius: $border-radius
  cursor: pointer

.upload-label
  color: white
  font-weight: bold
  text-transform: uppercase
  pointer-events: none
</style>
