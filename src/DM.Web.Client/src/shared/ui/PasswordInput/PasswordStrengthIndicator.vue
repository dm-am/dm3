<script setup lang="ts">
import { computed } from "vue";
import { PASSWORD_MIN_LENGTH } from "@/shared/lib/constants/user";
import { meetsPasswordMinimum } from "@/shared/lib/composables/useNewPasswordField";
import { pluralize } from "@/shared/lib/utils/pluralize";

const props = withDefaults(
  defineProps<{
    password: string;
    /** The password change form: the new password repeats the current one. */
    isSameAsOld?: boolean;
    /** The server's answer on submit, a breach above all: a refusal, so it takes the line and turns the bar red. */
    error?: string | null;
    /** Show the scale and its line on an empty field too. */
    always?: boolean;
  }>(),
  {
    isSameAsOld: false,
    error: null,
    always: false,
  },
);

const meetsMinimum = computed(() => meetsPasswordMinimum(props.password));

const strengthText = computed(() => {
  const len = props.password.length;
  if (len < 10) return "Слабый";
  if (len < 13) return "Средний";
  return "Надежный";
});

const lineText = computed(() => {
  if (props.error) return props.error;
  if (!meetsMinimum.value)
    return `Минимум ${PASSWORD_MIN_LENGTH} ${pluralize(PASSWORD_MIN_LENGTH, "символ", "символа", "символов")}`;
  if (props.isSameAsOld) return "Новый пароль совпадает с текущим";
  return strengthText.value;
});

const hasError = computed(
  () => !meetsMinimum.value || props.isSameAsOld || !!props.error,
);

// An empty field has broken no rule: the minimum is a hint, and the bar is empty.
const untouched = computed(() => props.password.length === 0);

const lineClass = computed(() => {
  if (props.error) return "warning-text";
  if (untouched.value) return "hint-text";
  if (hasError.value) return "warning-text";
  return "strength-text";
});

const barPercent = computed(() => {
  const len = props.password.length;
  if (len === 0) return 0;
  if (hasError.value) return 100;
  if (len >= 15) return 100;
  return 40 + ((len - PASSWORD_MIN_LENGTH) / 7) * 60;
});

const strengthProgress = computed(() => {
  const len = props.password.length;
  if (len < PASSWORD_MIN_LENGTH) return 0;
  return Math.min((len - PASSWORD_MIN_LENGTH) / 7, 1);
});

const barClass = computed(() => {
  if (hasError.value) return "error";
  return null;
});
</script>

<template>
  <!-- Shown from the start with `always`: there the minimum is written before it is broken. -->
  <div class="password-strength" v-if="always || password.length > 0">
    <div class="strength-bar">
      <div
        class="strength-fill"
        :style="{ width: `${barPercent}%`, '--progress': strengthProgress }"
        :class="barClass"
      />
    </div>
    <!-- The server's answer first: it alone knows a breach. Only it is announced; the others change under the typing. -->
    <div v-if="lineText" :class="lineClass" :role="error ? 'alert' : undefined">
      {{ lineText }}
    </div>
  </div>
</template>

<style scoped lang="sass">
.password-strength
  margin-top: $minor

.strength-bar
  height: 4px
  background: $border
  border-radius: 2px
  overflow: hidden

.strength-fill
  height: 100%
  border-radius: 2px
  transition: width $transition-slow, background-color $transition-slow
  background-color: color-mix(in srgb, $accent-yellow calc((1 - var(--progress)) * 100%), $accent-green calc(var(--progress) * 100%))

  &.error
    background-color: $accent-red

.warning-text
  margin-top: $tiny
  font-size: $secondary-font-size
  color: $accent-red

.hint-text
  margin-top: $tiny
  font-size: $secondary-font-size
  color: $text-muted

// Advice, not a refusal, in the colour of a hint: the bar already says it in colour.
.strength-text
  margin-top: $tiny
  font-size: $secondary-font-size
  color: $text-muted
</style>
