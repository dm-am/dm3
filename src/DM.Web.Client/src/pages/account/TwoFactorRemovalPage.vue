<script setup lang="ts">
/**
 * The link of the first letter: schedule the removal of the second factor. A
 * press and not a request on open: it ends every session and starts a week,
 * and scanners open links too. With no check of the token on the server, a
 * dead or refused link is learnt from the answer; a rate limit keeps the
 * question on screen with the wait over the button.
 */
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { describeFailure } from "@/shared/lib/errors";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { symbols } from "@/shared/lib/utils/icons";
import { supportFor } from "@/entities/ticket";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

/** The statuses of a dead link, which the heading already says. */
const LINK_IS_DEAD = [404, 410];

const router = useRouter();

const token = ref("");
const loading = ref(false);
const scheduled = ref(false);
/** The link did not work, and the screen says so in its heading. */
const dead = ref(false);
/** What the server refused with, when the refusal was not about the link. */
const refusal = ref("");
/** The wait the limiter imposed, counted down in the card that earned it. */
const rateLimit = useRateLimitNotice();

onMounted(() => {
  // From the fragment, cleared as it is read.
  token.value = readConfirmationToken();
  if (!token.value) dead.value = true;
});

async function schedule() {
  if (loading.value) return;

  loading.value = true;
  // The card carries whatever comes back, so the press takes the refusal and the limit over.
  const { error: failure } = await accountApi.scheduleTwoFactorRemoval(
    token.value,
    OWNS_REFUSAL,
  );
  loading.value = false;

  if (failure) {
    // The link is good, only the moment is wrong: the wait runs down over the button.
    if (rateLimit.take(failure)) return;

    // A 403 is news the heading cannot carry (the rank, a factor that is not on), so the card says it.
    refusal.value = LINK_IS_DEAD.includes(failure.status)
      ? ""
      : describeFailure(failure, "");
    dead.value = true;
    return;
  }

  scheduled.value = true;
}

function goHome() {
  router.push("/");
}
</script>

<template>
  <div class="auth-surface removal-page">
    <!-- One width for every state, the width of the other screens of a dead link. -->
    <div
      class="auth-card auth-card--framed auth-card--center auth-card--compact"
    >
      <!-- Scheduled: a receipt in the two-line shape of the other receipts, no framed box. -->
      <template v-if="scheduled">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Отключение назначено</h1>
        <p class="auth-text">
          Двухфакторная аутентификация отключится через 7 дней
        </p>
        <p class="auth-note">
          Вход с кодом автоматически отменит назначенное отключение
        </p>
        <Button
          class="auth-button auth-button--spaced"
          type="button"
          variant="primary"
          @click="goHome"
        >
          На главную
        </Button>
      </template>

      <template v-else-if="dead">
        <!-- The cross: a link that opens nothing leaves no next step. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>
        <!-- The server's refusal when it had one worth reading, else the two ways a link dies. -->
        <p class="auth-text">
          {{
            refusal ||
            "Возможно ссылка уже была использована или срок ее действия истек"
          }}
        </p>
        <!-- The one control all the dead links carry. -->
        <Button
          class="auth-button auth-button--spaced"
          type="button"
          variant="primary"
          @click="goHome"
        >
          На главную
        </Button>
        <p class="auth-footnote">
          Нужна помощь? Обратитесь в
          <router-link :to="supportFor('AccessRecovery')"
            >поддержку</router-link
          >
        </p>
      </template>

      <!-- The press, both consequences named before it: the delay and the sessions. -->
      <template v-else>
        <h1 class="auth-title">Подтвердите отключение</h1>
        <div class="auth-warnbox">
          <b>Это завершит все текущие сессии</b>Отключение двухфакторной
          аутентификации произойдет через 7 дней
        </div>
        <!-- The wait, only while there is one. -->
        <p
          v-if="rateLimit.message.value"
          class="auth-message auth-message--error"
          role="alert"
        >
          {{ rateLimit.message.value }}
        </p>
        <Button
          class="auth-button auth-button--spaced"
          type="button"
          variant="primary"
          :disabled="rateLimit.waiting.value"
          :loading="loading"
          @click="schedule"
        >
          Назначить отключение
        </Button>
        <p class="auth-footnote">
          Нужна помощь? Обратитесь в
          <router-link :to="supportFor('AccessRecovery')"
            >поддержку</router-link
          >
        </p>
      </template>
    </div>
  </div>
</template>

<style scoped lang="sass">
// The card is drawn by AuthSurface.sass; this is where it stands on the page.
.removal-page
  margin: $major auto
  padding: 0 $medium
</style>
