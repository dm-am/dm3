<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";
import { completeSecondFactor } from "@/entities/user";
import { Button } from "@/shared/ui/Button";
import { useCodeInput } from "@/shared/lib/composables/useCodeInput";
import type { RateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import {
  isRemoved,
  readAccountRefusal,
  sayLoginFailure,
} from "../lib/loginRefusal";

const props = defineProps<{
  // Shared with the password step: the limit is on the address.
  rateLimit: RateLimitNotice;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "restore"): void;
  (e: "cantPassSecondFactor"): void;
  /** The code went to the server, or its answer came: the dialog locks for the wait. */
  (e: "busy", busy: boolean): void;
}>();

// One value goes to the server either way; the server tells the kinds apart by shape.
const recoveryMode = ref(false);

const APP_CODE = "Код из приложения";
const RECOVERY_CODE = "Резервный код";
const ENTER_APP_CODE = "Ввести код из приложения";
const ENTER_RECOVERY_CODE = "Ввести резервный код";

const code = useCodeInput({
  mode: () => (recoveryMode.value ? "recovery" : "digits"),
});

const codeError = ref("");

const loading = ref(false);
watch(loading, (busy) => emit("busy", busy), { flush: "sync" });

// Ban or removal met between the factors, said as the password step says it.
const accountRefusal = ref<string | null>(null);

const removedAccount = ref(false);

const codeMessage = computed(
  () =>
    props.rateLimit.message.value || accountRefusal.value || codeError.value,
);

const codeName = computed(() =>
  recoveryMode.value ? RECOVERY_CODE : APP_CODE,
);

const otherCodeAction = computed(() =>
  recoveryMode.value ? ENTER_APP_CODE : ENTER_RECOVERY_CODE,
);

const codeHint = computed(() =>
  recoveryMode.value
    ? "Шестнадцать знаков из списка резервных кодов"
    : "Шесть цифр из приложения-аутентификатора",
);

onMounted(() => code.inputRef.value?.focus());

const submitSecondFactor = async () => {
  // Each attempt spends one of the few the server allows; the check also starts by itself.
  if (
    loading.value ||
    !code.isComplete.value ||
    props.rateLimit.waiting.value ||
    accountRefusal.value
  ) {
    return;
  }

  loading.value = true;
  codeError.value = "";
  const failure = await completeSecondFactor(code.normalized.value);
  loading.value = false;

  if (!failure) {
    emit("success");
    return;
  }

  // The code stays in the field: there is only a wait to sit out.
  if (props.rateLimit.take(failure)) return;

  // The only two account refusals CompleteTwoFactor can throw: a ban and a removal.
  const refusal = readAccountRefusal(failure);
  if (refusal) {
    accountRefusal.value = refusal;
    removedAccount.value = isRemoved(failure);
    return;
  }

  // A 403 of no documented shape would otherwise leave the screen silent.
  const said = sayLoginFailure(failure);
  if (said) codeError.value = said;

  // Selected whole, so the next code is typed over it.
  await nextTick();
  code.inputRef.value?.focus();
  code.inputRef.value?.select();
};

const onCodeInput = () => {
  code.onInput();
  codeError.value = "";
  // Starts by itself on a complete code, unless the field dropped symbols to get there.
  // No maxlength on the field: the browser would cut a pasted recovery code first.
  if (code.isComplete.value && !code.droppedSymbols.value) {
    void submitSecondFactor();
  }
};

const swapCodeKind = async () => {
  recoveryMode.value = !recoveryMode.value;
  // The old code and its refusal belong to the other kind; no check starts on a switch.
  code.reset();
  codeError.value = "";
  await nextTick();
  code.inputRef.value?.focus();
};
</script>

<template>
  <h2 class="auth-title">Подтверждение входа</h2>

  <form @submit.prevent="submitSecondFactor">
    <!-- A removed account takes the row over: no code opens it. -->
    <div class="auth-label-row">
      <label class="auth-label" for="two-factor-code">{{ codeName }}</label>
      <span class="copy-space">{{ " " }}</span>
      <button
        v-if="!removedAccount"
        type="button"
        class="auth-action"
        @click="swapCodeKind"
      >
        {{ otherCodeAction }}
      </button>
      <button v-else type="button" class="auth-action" @click="emit('restore')">
        Восстановить аккаунт
      </button>
    </div>
    <div class="auth-field">
      <!-- :value and @input, not v-model: v-model would move the caret to the end. -->
      <input
        id="two-factor-code"
        :ref="code.inputRef"
        class="auth-input--code"
        :class="{ 'auth-input--code-recovery': recoveryMode }"
        type="text"
        :inputmode="recoveryMode ? 'text' : 'numeric'"
        autocomplete="one-time-code"
        :autocapitalize="recoveryMode ? 'characters' : 'off'"
        autocorrect="off"
        spellcheck="false"
        enterkeyhint="go"
        aria-describedby="two-factor-code-message"
        :aria-invalid="codeError ? 'true' : undefined"
        :value="code.value.value"
        @input="onCodeInput"
        @keydown="code.onKeydown"
      />
    </div>
    <div
      id="two-factor-code-message"
      class="auth-message auth-message--reserved"
      :class="{ 'auth-message--error': !!codeMessage }"
      aria-live="polite"
    >
      {{ codeMessage || codeHint }}
    </div>

    <Button
      class="auth-button"
      variant="primary"
      :disabled="
        !code.isComplete.value || rateLimit.waiting.value || !!accountRefusal
      "
      :loading="loading"
      >Подтвердить</Button
    >
  </form>

  <p class="auth-footnote">
    Нет доступа к приложению и резервным кодам?
    <button
      type="button"
      class="auth-action"
      @click="emit('cantPassSecondFactor')"
    >
      Отключить двухфакторную аутентификацию
    </button>
  </p>
</template>
