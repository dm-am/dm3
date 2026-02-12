<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useAsyncAction } from "@/composables/useAsyncAction";
import accountApi from "@/api/requests/accountApi";
import TheButton from "@/components/inputs/TheButton.vue";

const route = useRoute();
const router = useRouter();

const { loading, error, execute } = useAsyncAction();
const confirmed = ref(false);

onMounted(async () => {
  const token = route.params.token as string;
  if (!token) {
    error.value = "Токен подтверждения отсутствует";
    return;
  }

  await execute(async () => {
    const { error: apiError } = await accountApi.confirmEmailChange(token);

    if (apiError) {
      if ("invalidProperties" in apiError || "errors" in apiError) {
        const err = apiError as { invalidProperties?: Record<string, string[]>; errors?: Record<string, string[]> };
        const props = err.invalidProperties ?? err.errors ?? {};
        const errorMessages = Object.values(props).flat();
        throw new Error(errorMessages[0] || "Не удалось подтвердить смену почты");
      }
      throw new Error("Не удалось подтвердить смену почты. Ссылка недействительна или устарела.");
    }

    confirmed.value = true;
  });
});
</script>

<template>
  <div class="confirm-page">
    <div v-if="loading" class="confirm-loading">
      <p class="loading-text">Подтверждаем смену почты...</p>
    </div>

    <div v-else-if="confirmed" class="confirm-success">
      <div class="success-icon">&#x2713;</div>
      <h2>Почта изменена</h2>
      <p>Ваша почта успешно обновлена.</p>
      <the-button @click="router.push('/')">На главную</the-button>
    </div>

    <div v-else class="confirm-error">
      <div class="error-icon">&#x2717;</div>
      <h2>Ошибка подтверждения</h2>
      <p class="error-message">{{ error || "Ссылка недействительна или устарела." }}</p>
      <the-button @click="router.push('/')">На главную</the-button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.confirm-page
  max-width: 600px
  margin: $big auto
  padding: $medium

.confirm-loading
  text-align: center
  padding: $big 0

.loading-text
  margin-top: $medium
  color: $text-muted
  font-size: 1rem

.confirm-success,
.confirm-error
  text-align: center
  background: $bg-element
  border-radius: $minor
  padding: $big
  box-shadow: 0 2px 8px $shadow-color

.success-icon
  font-size: 4rem
  color: $accent-green
  margin-bottom: $medium
  font-weight: bold

.error-icon
  font-size: 4rem
  color: $accent-red
  margin-bottom: $medium
  font-weight: bold

h2
  color: $text
  margin-bottom: $medium
  font-size: 1.5rem

p
  color: $text-muted
  margin-bottom: $medium
  line-height: 1.6

.error-message
  color: $accent-red
  font-weight: 500

button
  display: block
  margin: $medium auto 0
  min-width: 200px
</style>
