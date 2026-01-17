<script setup lang="ts">
import { ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useMessagingStore } from "@/stores";
import type { UserLogin } from "@/api/models/community";

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();

const error = ref<string | null>(null);

async function loadDirectConversation() {
  const login = route.params.login as UserLogin;
  error.value = null;

  if (!login) {
    router.replace({ name: "messenger" });
    return;
  }
  try {
    const conversation = await messagingStore.selectDirectConversation(login);
    if (conversation) {
      router.replace({ name: "conversation", params: { id: conversation.id } });
    } else {
      error.value = `Не удалось начать переписку с ${login}`;
    }
  } catch (e) {
    console.error("Failed to load direct conversation:", e);
    error.value = "Произошла ошибка при загрузке переписки";
  }
}

function goBack() {
  router.push({ name: "messenger" });
}

watch(() => route.params.login, loadDirectConversation, { immediate: true });
</script>

<template>
  <div class="loading-container">
    <template v-if="error">
      <secondary-text class="error-text">{{ error }}</secondary-text>
      <the-button @click="goBack">Назад к списку</the-button>
    </template>
    <template v-else>
      <the-loader :big="true" />
      <secondary-text>Загрузка переписки...</secondary-text>
    </template>
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
