<script setup lang="ts">
/**
 * The page the approval letter leads to, where the name a moderator agreed to
 * is chosen: typed on one step, confirmed under the caution on the next, as
 * the activation does it. A dead link gets two answers: the window that
 * closed, which leaves a new request to file, and the one dead link of the site.
 */
import { ref, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import {
  accountApi,
  useAuthStore,
  UsernameInput,
  fetchUser,
} from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import HomeButton from "./HomeButton.vue";
import { symbols } from "@/shared/lib/utils/icons";
import { supportFor } from "@/entities/ticket";
import { linkLifetimeNote } from "@/features/auth";

/** loading -> selectName -> confirmName -> completed, or one of the two dead ends. */
type PageState =
  | "loading"
  | "selectName"
  | "confirmName"
  | "expired"
  | "invalid"
  | "completed";

/** A spent approval and one never issued are the same dead link: nothing is owed and nothing can be done. */
const deadEnd = (status: "expired" | "used"): PageState =>
  status === "expired" ? "expired" : "invalid";

const router = useRouter();
const auth = useAuthStore();

const pageState = ref<PageState>("loading");
const submitting = ref(false);

// From the fragment, cleared from the address as it is read, so kept here.
const token = ref("");

const currentUsername = ref("");
const chosenUsername = ref("");
/** How long an approval link lives, as the answer about the token named it. */
const linkHours = ref<number>();
const nameAvailable = ref(false);

// A name the server refused after the field's check, which means somebody took
// it in between: the refusal replaces the field's verdict.
const nameRefused = ref(false);

const canProceed = computed(
  () => nameAvailable.value && !!chosenUsername.value && !nameRefused.value,
);

onMounted(async () => {
  token.value = readConfirmationToken();
  if (!token.value) {
    pageState.value = "invalid";
    return;
  }

  const { data, error } = await accountApi.getUsernameChangeApproval(
    token.value,
  );

  if (error || !data) {
    pageState.value = "invalid";
    return;
  }

  currentUsername.value = data.currentUsername ?? "";
  linkHours.value = data.linkLifetimeHours;
  pageState.value =
    data.status === "ready" ? "selectName" : deadEnd(data.status);
});

/** Typing answers a refusal: one handler stores the name and clears it. */
function onNameTyped(value: string) {
  chosenUsername.value = value;
  nameRefused.value = false;
}

function onAvailability(available: boolean) {
  nameAvailable.value = available;
  if (available) nameRefused.value = false;
}

function proceedToConfirm() {
  pageState.value = "confirmName";
}

function goBackToSelect() {
  if (submitting.value) return;
  pageState.value = "selectName";
}

async function submit() {
  if (submitting.value) return;

  submitting.value = true;

  const { data, error } = await accountApi.completeUsernameChange(
    token.value,
    chosenUsername.value,
  );

  submitting.value = false;

  if (error) {
    // The name taken and the letter followed in a second tab are both a 409, so the
    // token is asked for its state rather than the message read for words.
    const { data: state } = await accountApi.getUsernameChangeApproval(
      token.value,
    );

    if (!state) {
      pageState.value = "invalid";
      return;
    }

    if (state.status !== "ready") {
      pageState.value = deadEnd(state.status);
      return;
    }

    // The approval stands, so the name was taken between the check and the send:
    // back to the field, the approval kept.
    nameRefused.value = true;
    nameAvailable.value = false;
    pageState.value = "selectName";
    return;
  }

  if (data) {
    currentUsername.value = data.requestedUsername ?? chosenUsername.value;
    pageState.value = "completed";
    // The stored viewer still has the old name; the server has the rest, so it is refetched.
    await fetchUser();
  }
}
</script>

<template>
  <!-- A page and not a dialog, so the card has a frame of its own and no cross. -->
  <div class="auth-surface username-change-page">
    <!-- A mark, a heading and a line or two, one field at most: the narrow measure in every state. -->
    <div
      class="auth-card auth-card--framed auth-card--center auth-card--compact"
    >
      <!-- Checking the link: never an empty card under the support line. -->
      <p
        v-if="pageState === 'loading'"
        class="auth-note username-change-loading"
      >
        Проверяем ссылку...
      </p>

      <template v-else-if="pageState === 'selectName'">
        <h1 class="auth-title">Выберите новое имя</h1>

        <!-- The card is centred, the field is not: it reads from the left, as every field of the site. -->
        <form class="username-change-name" @submit.prevent="proceedToConfirm">
          <label class="auth-label" for="new-username">Новое имя</label>
          <username-input
            id="new-username"
            :model-value="chosenUsername"
            class="auth-field username-change-name__field"
            :class="{ 'username-change-name__field--refused': nameRefused }"
            @update:model-value="onNameTyped"
            @availability="onAvailability"
          />

          <!-- In the strip's own place, never beside it. -->
          <p v-if="nameRefused" class="username-change-refusal" role="alert">
            Это имя занято
          </p>

          <Button
            type="submit"
            variant="primary"
            class="auth-button"
            :disabled="!canProceed"
          >
            Продолжить
          </Button>
        </form>
      </template>

      <template v-else-if="pageState === 'confirmName'">
        <h1 class="auth-title">Подтвердите выбор имени</h1>

        <!-- The caution of the step that cannot be taken back, over the plate, as in the activation. -->
        <div class="auth-warnbox">
          <b>Будьте внимательны</b>Дальнейшая смена имени возможна лишь в
          исключительных случаях
        </div>

        <div class="auth-label-row">
          <span class="auth-label">Новое имя</span>
          <!-- A real space, so the row copies as one line. -->
          <span class="copy-space">{{ " " }}</span>
          <!-- "Изменить" goes back a step and nowhere in the address, so it is a button in the guest surface's secondary action. -->
          <button
            type="button"
            class="auth-action"
            :disabled="submitting"
            @click="goBackToSelect"
          >
            Изменить
          </button>
        </div>
        <div class="auth-value">{{ chosenUsername }}</div>

        <Button
          type="button"
          variant="primary"
          class="auth-button"
          :loading="submitting"
          @click="submit"
        >
          Сменить имя
        </Button>
      </template>

      <template v-else-if="pageState === 'completed'">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Имя изменено</h1>
        <p class="auth-text">Ваше имя успешно обновлено</p>
        <Button
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="router.push(`/users/${currentUsername}`)"
        >
          Перейти в профиль
        </Button>
      </template>

      <!-- The window closed before the name was chosen: a new request is filed from the settings, after a sign-in for a guest. -->
      <template v-else-if="pageState === 'expired'">
        <!-- The warning sign: the settings for whoever is signed in, the sign-in for whoever is not. -->
        <div class="auth-status auth-status--warn" aria-hidden="true">
          {{ symbols.warning }}
        </div>
        <h1 class="auth-title">Ссылка устарела</h1>
        <p v-if="linkHours !== undefined" class="auth-text">
          {{ linkLifetimeNote(linkHours) }}
        </p>
        <p class="auth-note">Переподайте заявку для изменения имени</p>
        <!-- The way out is the card's button, not a word inside the sentence. -->
        <Button
          v-if="auth.isAuthenticated"
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="router.push('/account')"
        >
          Перейти в настройки
        </Button>
        <Button
          v-else
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="router.push({ path: '/', query: { action: 'login' } })"
        >
          Войти
        </Button>
      </template>

      <!-- Nothing was issued for this value, or the approval is spent. -->
      <template v-else>
        <!-- The cross: a link that opens nothing leaves no next step. Drawn by the surface. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>

        <!-- An address cut or edited, or the letter followed twice: a rejected request mails no link, and expiry has its own card. -->
        <p class="auth-text">
          Возможно ссылка уже была использована или срок ее действия истек
        </p>
        <HomeButton />
      </template>

      <!-- Help under every answer but the checking and the changed name. -->
      <p
        v-if="pageState !== 'loading' && pageState !== 'completed'"
        class="auth-footnote"
      >
        Нужна помощь? Обратитесь в
        <router-link :to="supportFor('Bug')">поддержку</router-link>
      </p>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/AuthTokens" as *

.username-change-page
  margin: $major auto
  padding: 0 $medium

// The line is the whole body, so it has no bottom gap.
.username-change-loading
  margin: 0

.username-change-name
  text-align: left

  // The verdict is drawn inside UsernameInput, out of reach of the strip of
  // _AuthField.sass, so the strip is rebuilt one level in, as in the activation;
  // the extra class carries the specificity.
  .username-change-name__field
    margin-bottom: 0

    :deep(.username-status)
      margin-top: $auth-strip-gap
      min-height: calc(#{$auth-strip} - #{$auth-strip-gap})

    // The cross of the guest surface, from two bars, on the icon the shared field
    // renders, as in the activation.
    :deep(.status-icon.unavailable)
      width: 16px
      height: 16px
      font-size: 0

      +auth-bar-cross

  // A name given away while it was being confirmed: the refusal takes the place of the strip, on its metrics.
  .username-change-name__field--refused
    :deep(.username-status)
      display: none

.username-change-refusal
  margin: $auth-strip-gap 0 0
  min-height: calc(#{$auth-strip} - #{$auth-strip-gap})
  font-size: $secondary-font-size
  line-height: 1.4
  color: $accent-red
  text-align: left
</style>
