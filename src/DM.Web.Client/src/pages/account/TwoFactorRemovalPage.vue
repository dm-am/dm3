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
 * is learnt from the answer to this one. The card names a dead link in its own
 * heading, says under it why the link no longer works, and lets the reader out.
 * The server's own sentence takes that line when the refusal is about something
 * else - a rank the mailed path is closed for, a factor that is not on -
 * because for a link that is not a link the server sends back the heading word
 * for word, and the line then names the two ways such a link dies.
 *
 * A rate limit is not a dead link, and the card does not call it one. The
 * question stays on screen under its own heading, the wait stands in a line
 * over the button with the seconds running down, and the button opens again
 * when they run out - the shape every guest form gives a 429. It used to fall
 * into the dead-link card: "Ссылка недействительна" over a link that was fine,
 * with the limiter's title and no number under it, while the interceptor's
 * plate above named the seconds.
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

/**
 * The statuses this endpoint answers with when the link itself is the problem:
 * nothing was found behind it, or it had already been spent. The heading of the
 * screen says that, so there is nothing left for the server's sentence to add.
 */
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
  // The value arrives in the fragment and is cleared as it is read.
  token.value = readConfirmationToken();
  if (!token.value) dead.value = true;
});

async function schedule() {
  if (!token.value || loading.value) return;
  // A wait the server imposed is not over because the button was pressed again.
  if (rateLimit.waiting.value) return;

  loading.value = true;
  // The card below is the carrier of whatever comes back, so the press claims
  // the refusal and the limit, and the response interceptor keeps out of both.
  // Without that, a 403 stood in the card and in a plate over the card at once
  // - one sentence, two boxes, and the reader left to wonder which of them
  // answered him - and a 429 stood twice in two wordings, the seconds in the
  // plate and the limiter's bare title in the card.
  const { error: failure } = await accountApi.scheduleTwoFactorRemoval(
    token.value,
    { ownsRefusal: true, ownsRateLimit: true },
  );
  loading.value = false;

  if (failure) {
    // The link is still good; only the moment is wrong. The question keeps the
    // screen, the wait runs down in its own line and the button opens again.
    if (rateLimit.take(failure)) return;

    // The card owns this refusal and keeps it. A 403 here is news the heading
    // cannot carry - the mailed way in is closed for this rank, or the factor
    // is not on - and the generic line about links that outlive themselves
    // would send the reader to ask for another letter that will be refused the
    // same way (twoFactorRemovalPages.spec.ts).
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
      <!-- Scheduled: a receipt, and shaped like the other receipts of the
           surface. The mark says the press went through, the first line says
           what was scheduled and the grey line under it says the one thing the
           reader can still do about it - the same two-line shape the expired
           activation link uses. The framed box belongs to the question,
           where the reader still has a choice; on the answer it draws a second
           border around a card that is already a card. Owner's decision. -->
      <template v-if="scheduled">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Отключение назначено</h1>
        <p class="auth-text">Защита отключится через 7 дней</p>
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

      <!-- The link did not work. -->
      <template v-else-if="dead">
        <!-- The cross by the rule at .auth-status--warn in AuthSurface.sass:
             a link that opens nothing leaves this card no next step, and the
             warning sign is kept for the screens that do have one. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>
        <!-- The refusal when the server had one worth reading, and the two ways
             a link dies when it did not: a heading alone leaves the reader to
             guess what happened to a link he followed a minute ago. -->
        <p class="auth-text">
          {{
            refusal ||
            "Возможно ссылка уже была использована или срок ее действия истек"
          }}
        </p>
        <!-- One control, and the same one the other five dead links carry. The
             card used to open the dialog that mails a new letter; the reader
             who still needs one reaches it from the sign-in, where he came
             from. Owner's decision. -->
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

      <!-- The press. Both consequences are named before it: the delay the bold
           line above them announces, and then the sessions, which is the price
           paid on the spot for a removal that is a week away. -->
      <template v-else>
        <h1 class="auth-title">Подтвердите отключение</h1>
        <div class="auth-warnbox">
          <b>Это завершит все текущие сессии</b>Отключение двухфакторной
          аутентификации произойдет через 7 дней
        </div>
        <!-- The wait, only while there is one: the card has no field whose
             strip is held open, and a blank line kept between the box and the
             button on every visit would answer a refusal nobody has met. -->
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
// Everything inside the card is drawn by AuthSurface.sass. What is left here is
// where the card stands on the page.
.removal-page
  margin: $major auto
  padding: 0 $medium
</style>
