<script setup lang="ts">
import { ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useMessagingStore } from "@/entities/message";
import type { Username } from "@/shared/api/models/community";

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();

const error = ref<string | null>(null);

async function loadDirectChat() {
  const username = route.params.username as Username;
  error.value = null;

  if (!username) {
    router.replace({ name: "messenger" });
    return;
  }
  try {
    const chat = await messagingStore.selectDirectChat(username);
    if (chat) {
      router.replace({ name: "chat", params: { id: chat.id } });
    } else {
      error.value = `Не удалось начать переписку с ${username}`;
    }
  } catch {
    error.value = "Произошла ошибка при загрузке переписки";
  }
}

function goBack() {
  router.push({ name: "messenger" });
}

watch(() => route.params.username, loadDirectChat, { immediate: true });
</script>

<template>
  <div class="loading-container">
    <template v-if="error">
      <secondary-text class="error-text">{{ error }}</secondary-text>
      <the-button @click="goBack">Назад к списку</the-button>
    </template>
    <secondary-text v-else>Загрузка переписки...</secondary-text>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

.loading-container
  display: flex
  flex-direction: column
  align-items: center
  justify-content: center
  padding: $big
  gap: $medium
</style>
