<script setup lang="ts">
/**
 * The page the address-change letter leads to. It asks nothing of the reader:
 * opening the link is the confirmation, so the page has no form — it is
 * checking, it worked, or the link is dead.
 *
 * The one refusal that is not a dead link has a card of its own: the address
 * was free when the change was asked for and somebody took it while the letter
 * waited in the mailbox. The server names that case with a validation code, and
 * the page used to print the code — the reader was told "Taken".
 *
 * On the guest surface, like the other pages a letter opens. It drew a card of
 * its own before that, and the difference showed the moment the three were put
 * side by side.
 */
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { accountApi, useAuthStore, fetchUser } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { symbols } from "@/shared/lib/utils/icons";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { supportFor } from "@/entities/ticket";

/** The four answers, and the state the page opens in. */
type PageState = "loading" | "confirmed" | "taken" | "failed";

/** The code the server refuses an address in use with. */
const TAKEN = "Taken";

const router = useRouter();
const auth = useAuthStore();

// The page opens in the state it is actually in. Held as a phase and not as the
// `loading` flag of an async helper, which starts false: the first frame then
// drew the failure card before anything had been asked of the server.
const pageState = ref<PageState>("loading");

// The address the account now answers to. The confirmation itself carries no
// body, so it is read back off the account — which means it is there for a
// reader who followed the letter while signed in on this device, and not for a
// guest. The card names the address when it knows it and stops at the fact when
// it does not, rather than printing an empty tail.
const newEmail = ref("");

onMounted(async () => {
  // The value arrives in the fragment and is cleared as it is read — see
  // readConfirmationToken.
  const token = readConfirmationToken();

  // No line of our own about what is missing: the card already says the link is
  // not a link, and the only word this page had for the missing value was the
  // name of the thing in the database.
  if (!token) {
    pageState.value = "failed";
    return;
  }

  const { error: apiError } = await accountApi.confirmEmailChange(token);

  if (apiError) {
    // Read for the code and never for the sentence: the codes are the contract
    // and the wording is this page's job. Printed as they came, the reader of a
    // taken address was shown the English word the domain names the rule with.
    const codes = Object.values(parseApiErrors(apiError)).flat();
    pageState.value = codes.includes(TAKEN) ? "taken" : "failed";
    return;
  }

  pageState.value = "confirmed";
  // The viewer's stored copy still carries the old address, and the new one is
  // only on the account.
  await fetchUser();
  newEmail.value = auth.user?.email ?? "";
});

function goHome() {
  router.push("/");
}
</script>

<template>
  <!-- A page and not a dialog: nothing dims the ground behind it, so the card
       carries a frame of its own and there is no cross to close it with. -->
  <div class="auth-surface email-change-page">
    <!-- A mark, a heading and one line in every answer: the narrow measure, the
         one the other pages from letters keep. -->
    <div
      class="auth-card auth-card--framed auth-card--center auth-card--compact"
    >
      <!-- The card says what it is doing rather than standing empty under the
           support line while the request is out. -->
      <p v-if="pageState === 'loading'" class="auth-note email-change-loading">
        Проверяем ссылку...
      </p>

      <template v-else-if="pageState === 'confirmed'">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Почта изменена</h1>
        <p class="auth-text">
          Ваша почта успешно обновлена<span v-if="newEmail">
            на <b>{{ newEmail }}</b></span
          >
        </p>
        <!-- The errand is done, and the way on is the same for everybody who
             did it. The card used to read who was signed in and hand the guest
             a second control beside the first, so one outcome was drawn as two
             different cards and the difference said nothing about the outcome.
             Owner's decision. -->
        <Button
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="goHome"
        >
          На главную
        </Button>
      </template>

      <!-- The address went to somebody else while the letter waited. The link
           is spent either way, so there is nothing to retry here: the way on is
           another request, for another address. -->
      <template v-else-if="pageState === 'taken'">
        <!-- The cross by the rule at .auth-status--warn in AuthSurface.sass:
             the site has no next step to offer here. The address is gone and
             only the reader knows which one to ask for instead. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <!-- The heading names the outcome and the line under it states it, the
             way every other card of this surface does. It used to say "Ошибка
             подтверждения", which names nothing: every card of a failed link
             is an error of a confirmation, and this is the one that is not
             about the link at all. -->
        <h1 class="auth-title">Почта занята</h1>
        <p class="auth-text">Этот адрес уже занят другим аккаунтом</p>
        <p class="auth-note">Запросите смену почты на другой адрес</p>
        <Button
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="goHome"
        >
          На главную
        </Button>
      </template>

      <template v-else>
        <!-- The cross by the rule at .auth-status--warn in AuthSurface.sass:
             a spent link leaves nothing this card can do next. It is drawn by
             the surface and not typed, so no glyph goes inside it. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>
        <!-- The card's own type and not red ink: the mark above carries the
             verdict, and a sentence that is red as well says it twice. The
             sentence names which link it is about — the reader may be holding
             three of them from the same site. -->
        <p class="auth-text">
          Возможно ссылка уже была использована или срок ее действия истек
        </p>
        <Button
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="goHome"
        >
          На главную
        </Button>
      </template>

      <!-- Nothing has gone wrong while the link is being checked, and nothing
           has gone wrong once the address is changed, so there is nobody to ask
           for help on either. -->
      <p
        v-if="pageState === 'failed' || pageState === 'taken'"
        class="auth-footnote help-section"
      >
        Нужна помощь? Обратитесь в
        <router-link :to="supportFor('AccessRecovery')">поддержку</router-link>
      </p>
    </div>
  </div>
</template>

<style scoped lang="sass">
.email-change-page
  margin: $major auto
  padding: 0 $medium

// The line is the whole body of the card, so it carries no bottom gap of its
// own.
.email-change-loading
  margin: 0
</style>
