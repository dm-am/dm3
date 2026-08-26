<script setup lang="ts">
/**
 * The link from the second letter: call the scheduled removal off.
 *
 * Sent on open, unlike its sibling that schedules the removal. The asymmetry is
 * deliberate: this action undoes something rather than starting it, and the
 * reader who followed this link is the one saying the request was not theirs.
 * Nothing is lost if a scanner opens it first.
 */
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import StatusIcon from "@/shared/ui/Icon/StatusIcon.vue";
import { describeFailure } from "@/shared/lib/errors";

const router = useRouter();

const loading = ref(true);
const cancelled = ref(false);
const error = ref<string | null>(null);

onMounted(async () => {
  // The value arrives in the fragment and is cleared as it is read.
  const token = readConfirmationToken();
  if (!token) {
    loading.value = false;
    error.value = "Ссылка недействительна или устарела.";
    return;
  }

  const { error: failure } = await accountApi.cancelTwoFactorRemoval(token);
  loading.value = false;

  if (failure) {
    error.value = describeFailure(
      failure,
      "Не удалось отменить снятие. Ссылка недействительна или устарела.",
    );
    return;
  }

  cancelled.value = true;
});

function goHome() {
  router.push("/");
}
</script>

<template>
  <page-title>Отмена снятия второго фактора</page-title>

  <div class="confirm-page">
    <div class="confirm-card">
      <div v-if="loading" class="confirm-loading">
        <p class="loading-text">Отменяем снятие...</p>
      </div>

      <template v-else-if="cancelled">
        <status-icon type="success" />
        <dialog-title>Снятие отменено</dialog-title>
        <p class="status-description">
          Второй фактор остается включенным. Если снятие запрашивали не вы,
          значит доступ к вашей почте есть у кого-то еще: смените пароль от
          почтового ящика.
        </p>
        <div class="status-actions">
          <Button @click="goHome">На главную</Button>
        </div>
      </template>

      <template v-else>
        <status-icon type="error" />
        <dialog-title>Ссылка не сработала</dialog-title>
        <p class="status-description error-text">{{ error }}</p>
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
