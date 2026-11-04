<script setup lang="ts">
/**
 * The page the address-change letter leads to: opening the link is the
 * confirmation, so there is no form. An address taken while the letter waited
 * has a card of its own, worded by the page from the server's code.
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

// A phase from the start: a `loading` flag starting false drew the failure card first.
const pageState = ref<PageState>("loading");

// The confirmation has no body, so the new address is read off the account: known
// to a reader signed in on this device, and left out for a guest.
const newEmail = ref("");

onMounted(async () => {
  // From the fragment, cleared as it is read.
  const token = readConfirmationToken();

  // No line of our own about the missing value: the card says the link is not a link.
  if (!token) {
    pageState.value = "failed";
    return;
  }

  const { error: apiError } = await accountApi.confirmEmailChange(token);

  if (apiError) {
    // The code is the contract and the wording the page's: printed, it read "Taken".
    const codes = Object.values(parseApiErrors(apiError)).flat();
    pageState.value = codes.includes(TAKEN) ? "taken" : "failed";
    return;
  }

  pageState.value = "confirmed";
  // The stored viewer still has the old address.
  await fetchUser();
  newEmail.value = auth.user?.email ?? "";
});

function goHome() {
  router.push("/");
}
</script>

<template>
  <!-- A page and not a dialog, so the card has a frame of its own and no cross. -->
  <div class="auth-surface email-change-page">
    <!-- A mark, a heading and one line: the narrow measure. -->
    <div
      class="auth-card auth-card--framed auth-card--center auth-card--compact"
    >
      <!-- The card says what it is doing while the request is out. -->
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
      </template>

      <!-- The address went to somebody else while the letter waited: the way on is another request. -->
      <template v-else-if="pageState === 'taken'">
        <!-- The cross: only the reader knows which address to ask for instead. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <!-- The heading names the outcome, as every card of the surface does. -->
        <h1 class="auth-title">Почта занята</h1>
        <p class="auth-text">Этот адрес уже занят другим аккаунтом</p>
        <p class="auth-note">Запросите смену почты на другой адрес</p>
      </template>

      <template v-else>
        <!-- The cross: a spent link leaves nothing to do next. Drawn by the surface. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>
        <!-- The card's own type: the mark carries the verdict; the sentence names which link. -->
        <p class="auth-text">
          Возможно ссылка уже была использована или срок ее действия истек
        </p>
      </template>

      <!-- One way on for everybody, signed in or not. -->
      <Button
        v-if="pageState !== 'loading'"
        type="button"
        variant="primary"
        class="auth-button auth-button--spaced"
        @click="goHome"
      >
        На главную
      </Button>

      <!-- No help while the link is checked, nor once the address is changed. -->
      <p
        v-if="pageState === 'failed' || pageState === 'taken'"
        class="auth-footnote"
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

// The line is the whole body, so it has no bottom gap.
.email-change-loading
  margin: 0
</style>
