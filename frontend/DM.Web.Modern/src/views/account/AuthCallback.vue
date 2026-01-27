<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter, useRoute } from "vue-router";
import { useUserStore } from "@/stores";
import TheLoader from "@/components/TheLoader.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import TheButton from "@/components/inputs/TheButton.vue";

const router = useRouter();
const route = useRoute();
const userStore = useUserStore();

const processing = ref(true);
const error = ref<string | null>(null);

onMounted(async () => {
  // Get params from URL (could be query or hash)
  const params = new URLSearchParams(window.location.search || window.location.hash.slice(1));

  const errorParam = params.get("error");
  if (errorParam) {
    error.value = decodeURIComponent(errorParam);
    processing.value = false;
    return;
  }

  const accessToken = params.get("access_token");
  const refreshToken = params.get("refresh_token");
  const returnUrl = params.get("returnUrl") || "/";

  if (accessToken) {
    try {
      // Store tokens
      userStore.setOAuthTokens(accessToken, refreshToken || undefined);

      // Fetch user info
      await userStore.fetchUser();

      // Redirect to return URL
      router.push(returnUrl);
    } catch (e) {
      error.value = "Failed to complete authentication.";
      processing.value = false;
    }
  } else {
    error.value = "No authentication token received.";
    processing.value = false;
  }
});

function goToLogin() {
  router.push("/login");
}

function goHome() {
  router.push("/");
}
</script>

<template>
  <div class="auth-callback">
    <div v-if="processing" class="callback-loading">
      <the-loader :big="true" />
      <secondary-text>Завершение авторизации...</secondary-text>
    </div>

    <div v-else-if="error" class="callback-error">
      <h2>Ошибка авторизации</h2>
      <p class="error-message">{{ error }}</p>
      <div class="error-actions">
        <the-button @click="goToLogin">Войти</the-button>
        <the-button @click="goHome">На главную</the-button>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.auth-callback
  display: flex
  justify-content: center
  align-items: center
  min-height: $grid-step * 100
  padding: $big

.callback-loading
  display: flex
  flex-direction: column
  align-items: center
  gap: $medium

.callback-error
  text-align: center
  max-width: $grid-step * 100
  padding: $big
  background-color: $bg-element
  border-radius: $border-radius

  h2
    margin-bottom: $medium
    color: $accent-red

.error-message
  margin-bottom: $big
  color: $text-muted

.error-actions
  display: flex
  justify-content: center
  gap: $medium
</style>
