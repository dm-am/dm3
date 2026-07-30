<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useAsyncAction } from "@/shared/lib/composables/useAsyncAction";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import StatusIcon from "@/shared/ui/Icon/StatusIcon.vue";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";

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
      const errors = parseApiErrors(apiError);
      const errorMessages = Object.values(errors).flat();
      if (errorMessages.length > 0) {
        throw new Error(errorMessages[0]);
      }
      throw new Error(
        "Не удалось подтвердить смену почты. Ссылка недействительна или устарела.",
      );
    }

    confirmed.value = true;
  });
});

function goHome() {
  router.push("/");
}
</script>

<template>
  <div class="confirm-page">
    <div class="confirm-card">
      <!-- Loading state -->
      <div v-if="loading" class="confirm-loading">
        <p class="loading-text">Подтверждаем смену почты...</p>
      </div>

      <!-- Success state -->
      <template v-else-if="confirmed">
        <status-icon type="success" />
        <dialog-title>Почта изменена</dialog-title>
        <p class="status-description">Ваша почта успешно обновлена.</p>
        <div class="status-actions">
          <Button @click="goHome">На главную</Button>
        </div>
      </template>

      <!-- Error state -->
      <template v-else>
        <status-icon type="error" />
        <dialog-title>Ошибка подтверждения</dialog-title>
        <p class="status-description error-text">
          {{ error || "Ссылка недействительна или устарела." }}
        </p>
        <div class="status-actions">
          <Button @click="goHome">На главную</Button>
        </div>
      </template>

      <div class="help-section">
        Нужна помощь? Обратитесь в
        <router-link to="/support?reason=access">поддержку</router-link>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
.confirm-page
  max-width: 380px
  margin: $major auto
  padding: 0 $medium

.confirm-card
  text-align: center
  background: $bg-element
  border-radius: $border-radius
  padding: $big
  box-shadow: 0 2px 12px $shadow-color

.confirm-loading
  padding: $big 0

.loading-text
  color: $text-muted
  font-size: $font-size

.status-description
  margin: 0 0 $medium
  font-size: $font-size
  line-height: 1.6
  color: $text

.error-text
  color: $accent-red

.status-actions
  margin-top: $big

  :deep(.button)
    min-width: 200px

.help-section
  margin-top: $big
  padding-top: $medium
  border-top: 1px solid $border
  font-size: $secondary-font-size
  color: $text-muted

  a
    font-weight: bold
</style>
