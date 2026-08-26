<script setup lang="ts">
/**
 * The page the approval letter leads to: where the name a moderator agreed to
 * is actually chosen.
 *
 * The letter used to point at an address the site does not route, so the last
 * step of the flow was a 404 and the approval could only run out. Everything
 * behind it — the token, the two endpoints — was already there.
 *
 * A dead link is not one thing. The reader is told which of the three it is,
 * because only one of them leaves them anything to do.
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
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import StatusIcon from "@/shared/ui/Icon/StatusIcon.vue";
import { describeFailure } from "@/shared/lib/errors";

/** loading -> ready -> completed, or one of the three dead ends. */
type PageState =
  | "loading"
  | "ready"
  | "expired"
  | "used"
  | "invalid"
  | "completed";

const router = useRouter();
const auth = useAuthStore();

const pageState = ref<PageState>("loading");
const submitting = ref(false);

// The value arrives in the fragment and is cleared as it is read, so it is read
// once and kept: in the path it went into browser history and into the access
// log of the edge — see readConfirmationToken.
const token = ref("");

const currentUsername = ref("");
const chosenUsername = ref("");
const nameAvailable = ref(false);
const nameError = ref("");

const canSubmit = computed(
  () => nameAvailable.value && !submitting.value && !!chosenUsername.value,
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
  pageState.value = data.status;
});

async function submit() {
  if (!canSubmit.value) return;

  submitting.value = true;
  nameError.value = "";

  const { data, error } = await accountApi.completeUsernameChange(
    token.value,
    chosenUsername.value,
  );

  submitting.value = false;

  if (error) {
    // A refusal is either about the name or about the link, and the status code
    // does not separate them: the name being taken and the same letter followed
    // in a second tab are both a 409. The token is asked what state it is in
    // rather than the message being read for words, which would make a sentence
    // on the server part of the contract.
    const { data: state } = await accountApi.getUsernameChangeApproval(
      token.value,
    );

    if (!state || state.status !== "ready") {
      pageState.value = state?.status ?? "invalid";
      return;
    }

    // The approval still stands, so the name is what was refused. The form stays
    // open: picking another one is something the reader can do right here, and
    // closing it over this would cost them the approval.
    nameError.value = describeFailure(error, "Это имя уже занято");
    nameAvailable.value = false;
    return;
  }

  if (data) {
    currentUsername.value = data.requestedUsername ?? chosenUsername.value;
    pageState.value = "completed";
    // The viewer's stored copy still carries the old name. Only the server
    // knows the rest of the profile, so it is refetched rather than patched.
    await fetchUser();
  }
}

function goToProfile() {
  router.push(`/users/${currentUsername.value}`);
}

function goHome() {
  router.push("/");
}
</script>

<template>
  <div
    class="username-change-page"
    :class="{ 'username-change-page--wide': pageState === 'invalid' }"
  >
    <div class="username-change-card">
      <!-- Checking the link, as on the three pages next to this one: the card
           is never blank under the support line while the request is out. -->
      <div v-if="pageState === 'loading'" class="checking">
        <p class="loading-text">Проверяем ссылку...</p>
      </div>

      <!-- The approval stands: choose the name -->
      <template v-else-if="pageState === 'ready'">
        <dialog-title>Выберите новое имя</dialog-title>
        <p class="status-description">
          Сейчас вас зовут <strong>{{ currentUsername }}</strong
          >.
        </p>

        <form @submit.prevent="submit" class="name-form">
          <label class="field-label" for="new-username">Новое имя</label>
          <username-input
            id="new-username"
            v-model="chosenUsername"
            :disabled="submitting"
            @availability="nameAvailable = $event"
          />

          <p v-if="nameError" class="field-error">{{ nameError }}</p>

          <div class="warning">
            Следующая смена имени снова потребует заявки и одобрения модератора.
          </div>

          <Button
            type="submit"
            :loading="submitting"
            :disabled="!canSubmit"
            class="submit-button"
          >
            Сменить имя
          </Button>
        </form>
      </template>

      <!-- Done -->
      <template v-else-if="pageState === 'completed'">
        <status-icon type="success" />
        <dialog-title>Имя изменено</dialog-title>
        <p class="status-description">
          Теперь вас зовут <strong>{{ currentUsername }}</strong
          >.
        </p>
        <div class="status-actions">
          <Button v-if="auth.isAuthenticated" @click="goToProfile"
            >В профиль</Button
          >
          <Button v-else @click="goHome">На главную</Button>
        </div>
      </template>

      <!-- The window closed before the name was chosen -->
      <template v-else-if="pageState === 'expired'">
        <status-icon type="warning" />
        <dialog-title>Срок действия ссылки истек</dialog-title>
        <p class="status-description">
          Ссылка на выбор имени действует 48 часов. Чтобы сменить имя, подайте
          заявку заново в
          <router-link to="/account">настройках аккаунта</router-link>.
        </p>
      </template>

      <!-- The same letter followed twice -->
      <template v-else-if="pageState === 'used'">
        <status-icon type="warning" />
        <dialog-title>Имя уже изменено</dialog-title>
        <p class="status-description">
          По этой ссылке новое имя уже выбрано. Второй раз она не сработает: на
          каждую смену имени нужна своя заявка.
        </p>
        <div class="status-actions">
          <Button v-if="auth.isAuthenticated" @click="goHome"
            >На главную</Button
          >
          <router-link
            v-else
            :to="{ path: '/', query: { action: 'login' } }"
            class="login-link"
            >Войти</router-link
          >
        </div>
      </template>

      <!-- Nothing was ever issued for this value -->
      <template v-else>
        <status-icon type="error" />
        <dialog-title>Ссылка не работает</dialog-title>

        <div class="error-info">
          <p><strong>Возможные причины:</strong></p>
          <ul>
            <li>Адрес скопирован из письма не целиком</li>
            <li>Заявка была отозвана или отклонена модератором</li>
            <li>Пришло более новое письмо, проверьте последнее из них</li>
          </ul>
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
.username-change-page
  max-width: 380px
  margin: $major auto
  padding: 0 $medium

  &--wide
    max-width: 480px

.checking
  padding: $big 0

.loading-text
  color: $text-muted

.username-change-card
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

.status-actions
  margin-top: $big

  :deep(.button)
    min-width: 200px

.login-link
  font-weight: bold

// Form
.name-form
  display: flex
  flex-direction: column
  gap: $small
  text-align: left

.field-label
  font-size: $secondary-font-size
  color: $text-muted

.field-error
  margin: 0
  font-size: $secondary-font-size
  color: $accent-red

// Info section — same notice as .registration-info: square frame, ordinary ink.
.warning
  padding: $small $medium
  border: 1px solid $border
  font-size: $secondary-font-size
  line-height: 1.5
  color: $text

.submit-button
  margin-top: $tiny

// Error info box
.error-info
  margin: $medium 0
  padding: $small $medium
  border: 1px solid $border-accent-red
  font-size: $secondary-font-size
  line-height: 1.5
  text-align: left

  p
    margin: $minor 0

    &:first-child
      margin-top: 0

  ul
    margin: $minor 0 0
    padding-left: $medium

    li
      margin: $minor 0

// Help section
.help-section
  margin-top: $big
  padding-top: $medium
  border-top: 1px solid $border
  font-size: $secondary-font-size
  color: $text-muted

  a
    font-weight: bold
</style>
