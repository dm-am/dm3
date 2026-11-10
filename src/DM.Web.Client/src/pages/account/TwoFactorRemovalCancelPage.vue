<script setup lang="ts">
/**
 * The link of the second letter: call the scheduled removal off, sent on open,
 * since it undoes rather than starts. The card says it is checking while the
 * request is out.
 */
import { onMounted, ref } from "vue";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { accountApi } from "@/entities/user";
import HomeButton from "./HomeButton.vue";
import { symbols } from "@/shared/lib/utils/icons";
import { supportFor } from "@/entities/ticket";

/** The request is on the wire. */
const loading = ref(true);
const cancelled = ref(false);
/** The link did not work, and the screen says so in its heading. */
const dead = ref(false);

onMounted(async () => {
  // From the fragment, cleared as it is read.
  const token = readConfirmationToken();
  if (!token) {
    loading.value = false;
    dead.value = true;
    return;
  }

  const { error: failure } = await accountApi.cancelTwoFactorRemoval(token);
  loading.value = false;

  if (failure) {
    dead.value = true;
    return;
  }

  cancelled.value = true;
});
</script>

<template>
  <div class="auth-surface removal-cancel-page">
    <!-- One width for every state, the width of the other screens of a dead link. -->
    <div
      class="auth-card auth-card--framed auth-card--center auth-card--compact"
    >
      <p v-if="loading" class="auth-note">Проверяем ссылку...</p>

      <!-- Called off: a receipt in the two-line shape of the scheduling page's; the line names the mailbox's password. -->
      <template v-else-if="cancelled">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Отключение отменено</h1>
        <p class="auth-text">Отключение запросили не вы?</p>
        <p class="auth-note">
          Если да, то рекомендуем обновить пароль от вашего почтового ящика
        </p>
        <HomeButton />
      </template>

      <!-- Nothing to call off: the heading of the twin page, and only what this link can say, since the page reads no account. -->
      <template v-else-if="dead">
        <!-- The cross and the heading of the scheduling page: they answer the same thing. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>
        <!-- The sentence of the scheduling page: a link dies the same two ways here. -->
        <p class="auth-text">
          Возможно ссылка уже была использована или срок ее действия истек
        </p>
        <HomeButton />
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
.removal-cancel-page
  margin: $major auto
  padding: 0 $medium
</style>
