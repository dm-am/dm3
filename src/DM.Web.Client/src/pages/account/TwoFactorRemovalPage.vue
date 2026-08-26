<script setup lang="ts">
/**
 * The link from the first letter: schedule the removal of the second factor.
 *
 * A button and not a request fired on open, unlike the email-change landing
 * next door. There the consequence is small; here following the link ends every
 * session of the account and starts a seven-day countdown, and links in letters
 * are opened by scanners and previews as well as by people. One press costs
 * nothing and takes a whole class of accidents off the table.
 *
 * There is no "check this token" call on the server, so a dead or refused link
 * is learnt from the answer to this one. The server answers in finished
 * sentences - a rank the mailed path is closed for, an expired link, a factor
 * that is not on - and they are shown as they are.
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

const token = ref("");
const loading = ref(false);
const scheduled = ref(false);
const error = ref<string | null>(null);

onMounted(() => {
  // The value arrives in the fragment and is cleared as it is read.
  token.value = readConfirmationToken();
  if (!token.value) error.value = "Ссылка недействительна или устарела.";
});

async function schedule() {
  if (!token.value || loading.value) return;

  loading.value = true;
  error.value = null;
  const { error: failure } = await accountApi.scheduleTwoFactorRemoval(
    token.value,
  );
  loading.value = false;

  if (failure) {
    error.value = describeFailure(
      failure,
      "Не удалось назначить снятие. Ссылка недействительна или устарела.",
    );
    return;
  }

  scheduled.value = true;
}

function goHome() {
  router.push("/");
}
</script>

<template>
  <page-title>Снятие второго фактора</page-title>

  <div class="confirm-page">
    <div class="confirm-card">
      <template v-if="scheduled">
        <status-icon type="success" />
        <dialog-title>Снятие назначено</dialog-title>
        <p class="status-description">
          Второй фактор будет снят через семь дней. Все сессии аккаунта
          завершены, а на почту ушло письмо со ссылкой для отмены.
        </p>
        <p class="status-description status-secondary">
          Если телефон найдется раньше, войдите со вторым фактором: любой такой
          вход отменяет снятие.
        </p>
        <div class="status-actions">
          <Button @click="goHome">На главную</Button>
        </div>
      </template>

      <template v-else-if="error">
        <status-icon type="error" />
        <dialog-title>Ссылка не сработала</dialog-title>
        <p class="status-description error-text">{{ error }}</p>
        <div class="status-actions">
          <Button @click="goHome">На главную</Button>
        </div>
      </template>

      <template v-else>
        <dialog-title>Снятие второго фактора</dialog-title>
        <p class="status-description">
          Второй фактор будет снят через семь дней. Все сессии аккаунта
          завершатся сразу, а на почту придет письмо со ссылкой для отмены.
        </p>
        <p class="status-description status-secondary">
          Отменить снятие можно и входом со вторым фактором.
        </p>
        <div class="status-actions">
          <Button :loading="loading" @click="schedule">
            Назначить снятие
          </Button>
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

.status-description
  margin: 0 0 $medium
  font-size: $font-size
  line-height: 1.6
  color: $text

.status-secondary
  font-size: $secondary-font-size
  color: $text-muted

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
