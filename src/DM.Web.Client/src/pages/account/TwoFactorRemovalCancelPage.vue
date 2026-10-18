<script setup lang="ts">
/**
 * The link from the second letter: call the scheduled removal off.
 *
 * Sent on open, unlike its sibling that schedules the removal. The asymmetry is
 * deliberate: this action undoes something rather than starting it, and the
 * reader who followed this link is the one saying the request was not theirs.
 * Nothing is lost if a scanner opens it first.
 *
 * The card says what it is doing while the request is in flight, the way every
 * other page reached from a letter does. An empty card with nothing but a line
 * to support under it is the shape of a broken page, shown at the moment
 * nothing is broken, and a slow answer leaves the reader looking at it.
 */
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { symbols } from "@/shared/lib/utils/icons";
import { supportFor } from "@/entities/ticket";

const router = useRouter();

/** The request is on the wire, and the card says so instead of standing empty. */
const loading = ref(true);
const cancelled = ref(false);
/** The link did not work, and the screen says so in its heading. */
const dead = ref(false);

onMounted(async () => {
  // The value arrives in the fragment and is cleared as it is read.
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

function goHome() {
  router.push("/");
}
</script>

<template>
  <div class="auth-surface removal-cancel-page">
    <!-- One width for every state of this page, and the same width the other
         screens that answer a dead link wear. The card used to widen on the
         dead link for a heading that no longer exists ("Ссылка отключения
         устарела", 314px against the 302 a narrow card gives); the heading it
         shares with activation, password reset, the change of address and the
         change of name occupies 270px and fits. A title that says one thing in
         two measures reads as two different screens. -->
    <div
      class="auth-card auth-card--framed auth-card--center auth-card--compact"
    >
      <p v-if="loading" class="auth-note">Проверяем ссылку...</p>

      <!-- Called off: a receipt, in the same two-line shape as the receipt on
           the page that schedules the removal. The question stands in ordinary
           type and the answer to it in grey - somebody asked to take the second
           factor off this account, and it was not the reader. No link here
           leads to the mailbox's own password, so the line names the place
           instead of offering a button. The framed box belongs to the question,
           not to the answer. Owner's decision. -->
      <template v-else-if="cancelled">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Отключение отменено</h1>
        <p class="auth-text">Отключение запросили не вы?</p>
        <p class="auth-note">
          Если да, то рекомендуем обновить пароль от вашего почтового ящика
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

      <!-- Nothing to call off, and the same heading as the page this link's
           twin leads to: both are a letter that outlived its link, and two
           names for one answer read as two different answers. The line under
           it says only what this link can say: there is nothing left for it
           to cancel. This page reads no account and does not know the state
           of the factor. The line used to say "уже отменено", and on the
           eighth day, when the nightly pass had already taken the factor off
           and the cancel token was not yet spent, that was untrue. Where the
           state can be read, the grey line says. -->
      <template v-else-if="dead">
        <!-- The cross by the rule at .auth-status--warn in AuthSurface.sass:
             a link that opens nothing leaves this card no next step. The same
             mark and the same heading as the page that schedules the removal,
             because both answer the same thing. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>
        <!-- The same sentence as the page that schedules the removal: a dead
             link dies the same two ways here, and two wordings for one event
             read as two different events. -->
        <p class="auth-text">
          Возможно ссылка уже была использована или срок ее действия истек
        </p>
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
    </div>
  </div>
</template>

<style scoped lang="sass">
// Everything inside the card is drawn by AuthSurface.sass. What is left here is
// where the card stands on the page.
.removal-cancel-page
  margin: $major auto
  padding: 0 $medium
</style>
