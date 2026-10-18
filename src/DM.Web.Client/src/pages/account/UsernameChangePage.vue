<script setup lang="ts">
/**
 * The page the approval letter leads to: where the name a moderator agreed to
 * is actually chosen.
 *
 * The letter used to point at an address the site does not route, so the last
 * step of the flow was a 404 and the approval could only run out. Everything
 * behind it — the token, the two endpoints — was already there.
 *
 * The choice is made in two steps, the way the activation makes it: the name is
 * typed on the first and confirmed on the second, and the caution about a
 * further change stands on the second, over the plate that repeats the name.
 * On the first step there is nothing to be cautious about yet — the reader is
 * still trying names out.
 *
 * A dead link gets two answers and not three: the window that closed, because
 * that one leaves the reader a new request to file, and everything else, which
 * is the one dead link the whole site shows. Whichever of the answers it gives,
 * it gives it on the guest surface: this is the third page a letter opens, and
 * it drew its own card, its own widths and its own footer until the three were
 * put side by side.
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
import { symbols } from "@/shared/lib/utils/icons";
import { supportFor } from "@/entities/ticket";

/** loading -> selectName -> confirmName -> completed, or one of the two dead ends. */
type PageState =
  | "loading"
  | "selectName"
  | "confirmName"
  | "expired"
  | "invalid"
  | "completed";

/**
 * What a token that is no longer good for anything leaves on screen.
 *
 * The server tells a spent approval apart from one it never issued, and the
 * page used to tell the reader: a card of its own, headed "Имя уже изменено".
 * It is the same dead link either way — nothing is owed and nothing can be
 * done with it — so it takes the one answer the site gives a dead link, the
 * same as on the five screens beside this one. Owner's decision.
 */
const deadEnd = (status: "expired" | "used"): PageState =>
  status === "expired" ? "expired" : "invalid";

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

// The name passed the check in the field and the server still refused it, which
// here means one thing only: somebody took it in between. It is not a second
// message next to the field's own verdict but that verdict replaced — the strip
// cannot say "Имя свободно" about a name that has just been given away.
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
  pageState.value =
    data.status === "ready" ? "selectName" : deadEnd(data.status);
});

/**
 * Typing is the answer to a refusal: the field takes its own verdict back.
 *
 * The value is bound by hand rather than through v-model, so that the one
 * handler both stores the name and clears the refusal standing in place of the
 * field's verdict.
 */
function onNameTyped(value: string) {
  chosenUsername.value = value;
  nameRefused.value = false;
}

function onAvailability(available: boolean) {
  nameAvailable.value = available;
  if (available) nameRefused.value = false;
}

function proceedToConfirm() {
  if (!canProceed.value) return;
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
    // A refusal is either about the name or about the link, and the status code
    // does not separate them: the name being taken and the same letter followed
    // in a second tab are both a 409. The token is asked what state it is in
    // rather than the message being read for words, which would make a sentence
    // on the server part of the contract.
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

    // The approval still stands, so the name is what was refused, and the only
    // way that happens is somebody taking it between the check and the send.
    // The reader goes back to the field with the answer in it: picking another
    // name is something they can do right here, and closing the card over this
    // would cost them the approval.
    nameRefused.value = true;
    nameAvailable.value = false;
    pageState.value = "selectName";
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

function goToSettings() {
  router.push("/account");
}

function goToLogin() {
  router.push({ path: "/", query: { action: "login" } });
}
</script>

<template>
  <!-- A page and not a dialog: nothing dims the ground behind it, so the card
       carries a frame of its own and there is no cross to close it with. -->
  <div class="auth-surface username-change-page">
    <!-- Every answer here is a mark, a heading and a line or two, and the one
         form on the page holds a single field, so the card takes the narrow
         measure in all of its states — the same one the activation keeps. The
         sheet cannot work that out from the contents, so the card says it. -->
    <div
      class="auth-card auth-card--framed auth-card--center auth-card--compact"
    >
      <!-- Checking the link, as on the pages next to this one: the card is
           never blank under the support line while the request is out. -->
      <p
        v-if="pageState === 'loading'"
        class="auth-note username-change-loading"
      >
        Проверяем ссылку...
      </p>

      <!-- Step 1: the approval stands, so a name is typed -->
      <template v-else-if="pageState === 'selectName'">
        <h1 class="auth-title">Выберите новое имя</h1>

        <!-- The card is centred, the field is not: a label, the box under it
             and the verdict under that read from the left, as everywhere else
             on the site something is filled in. -->
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

          <!-- In the strip's own place and never beside it: two verdicts on one
               name, one green and one red, is the state this card used to
               reach. -->
          <p v-if="nameRefused" class="username-change-refusal" role="alert">
            Это имя занято
          </p>

          <Button
            type="submit"
            variant="primary"
            class="auth-button submit-button"
            :disabled="!canProceed"
          >
            Продолжить
          </Button>
        </form>
      </template>

      <!-- Step 2: the name is repeated back before it is spent -->
      <template v-else-if="pageState === 'confirmName'">
        <h1 class="auth-title">Подтвердите выбор имени</h1>

        <!-- The caution belongs to the step that cannot be taken back, and it
             stands over the plate rather than under it: it is read before the
             button, not after. Same box and same opening line as the
             confirmation step of the activation, because it is the same
             caution. -->
        <div class="auth-warnbox">
          <b>Будьте внимательны</b>Дальнейшая смена имени возможна лишь в
          исключительных случаях
        </div>

        <div class="auth-label-row">
          <span class="auth-label">Новое имя</span>
          <!-- A real space, so the row copies as one line (Reset.sass,
               .copy-space). -->
          <span class="copy-space">{{ " " }}</span>
          <!-- "Изменить" goes back a step in code and nowhere in the address
               bar, so the control is a button, wearing the guest surface's own
               secondary action to read as the <a> it replaces. -->
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
          class="auth-button submit-button"
          :loading="submitting"
          @click="submit"
        >
          Сменить имя
        </Button>
      </template>

      <!-- Done -->
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
          @click="goToProfile"
        >
          Перейти в профиль
        </Button>
      </template>

      <!-- The window closed before the name was chosen. The way on is a new
           request, and a new request is filed from the settings — so the reader
           who is not signed in is sent to sign in first. -->
      <template v-else-if="pageState === 'expired'">
        <!-- The warning sign by the rule at .auth-status--warn in
             AuthSurface.sass: the site can take the next step for the reader,
             and the button under the card is that step - the settings for
             whoever is signed in, the sign-in for whoever is not. -->
        <div class="auth-status auth-status--warn" aria-hidden="true">
          {{ symbols.warning }}
        </div>
        <h1 class="auth-title">Ссылка устарела</h1>
        <p class="auth-text">Ссылка действительна 48 часов</p>
        <p class="auth-note">Переподайте заявку для изменения имени</p>
        <!-- The way out is the button of the card and not a word inside the
             sentence: on a dead link there is one thing left to do, and it is
             the thing the card is for. -->
        <Button
          v-if="auth.isAuthenticated"
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="goToSettings"
        >
          Перейти в настройки
        </Button>
        <Button
          v-else
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="goToLogin"
        >
          Войти
        </Button>
      </template>

      <!-- Nothing was ever issued for this value, or the letter was followed
           twice and the approval is spent. -->
      <template v-else>
        <!-- The cross by the rule at .auth-status--warn in AuthSurface.sass:
             a link that opens nothing leaves this card no next step, unlike the
             expired one above it. It is drawn by the surface and not typed, so
             no glyph goes inside it. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>

        <!-- What actually puts a reader here: an address that lost its tail, a
             value the mail client wrapped or cut, an address edited by hand, or
             the same letter followed a second time. A refusal by a moderator is
             not one of them — a rejected request is never given a token and
             never mails a link — and expiry does not reach this card: it has
             one of its own, because there the reader can still file a new
             request. -->
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
           has gone wrong once the name is changed, so there is nobody to ask
           for help on either: the offer stands under every other answer. -->
      <p
        v-if="pageState !== 'loading' && pageState !== 'completed'"
        class="auth-footnote help-section"
      >
        Нужна помощь? Обратитесь в
        <router-link :to="supportFor('Bug')">поддержку</router-link>
      </p>
    </div>
  </div>
</template>

<style scoped lang="sass">
.username-change-page
  margin: $major auto
  padding: 0 $medium

// The card says what it is doing rather than standing empty; the line is the
// whole body, so it carries no bottom gap of its own.
.username-change-loading
  margin: 0

.username-change-name
  text-align: left

  // UsernameInput draws the verdict on the name inside its own root, so the
  // strip AuthSurface hangs on `.auth-field + .auth-message` cannot reach it.
  // It is rebuilt one level in, exactly as the activation rebuilds it: the
  // field gives up its 2rem, the verdict line starts on the usual 3px and holds
  // the rest of the strip open, and the button below stands 2rem under the
  // field whatever the line says.
  //
  // The extra class carries the specificity — a bare `.auth-field` here ties
  // with the sheet's own two-level selector, and a tie is settled by whichever
  // stylesheet the bundler happened to emit last.
  .username-change-name__field
    margin-bottom: 0

    :deep(.username-status)
      margin-top: 3px
      min-height: calc(2rem - 3px)

    // The mark inside the field. The component prints the ✗ of the icon set
    // for a refusal; the guest surface draws its crosses from two bars,
    // because every typographic cross in the fonts the site ships is thinner
    // than the ✓ standing beside it and how much thinner depends on which font
    // actually loaded. The component is shared and its markup is not this
    // page's to change, so the recipe of .auth-field-status--error is repeated
    // on the icon it does render — the same repeat the activation carries, and
    // for the same reason.
    :deep(.status-icon.unavailable)
      width: 16px
      height: 16px
      font-size: 0

      &::before,
      &::after
        content: ""
        position: absolute
        left: 50%
        top: 50%
        width: 14px
        height: 2px
        margin: -1px 0 0 -7px
        border-radius: 1px
        background-color: currentColor

      &::before
        transform: rotate(45deg)

      &::after
        transform: rotate(-45deg)

  // A name the server gave away while the reader was confirming it. The field's
  // own strip is standing on the answer to a question that is no longer being
  // asked — it checked the name before it was taken — so the strip steps aside
  // and the refusal takes its place, in the same slot and on the same metrics,
  // rather than standing under it as a second opinion.
  .username-change-name__field--refused
    :deep(.username-status)
      display: none

.username-change-refusal
  margin: 3px 0 0
  min-height: calc(2rem - 3px)
  font-size: $secondary-font-size
  line-height: 1.4
  color: $accent-red
  text-align: left
</style>
