<template>
  <!-- The scale is hidden until there is something to measure, unless the
       screen asked for it to stand there from the start: on the guest forms it
       is where the rule "Минимум 8 символов" is written, and a rule that
       appears only after the reader has broken it is not a rule he was given.
       A field that already carries the rule in a hint of its own keeps the old
       behaviour, or the same sentence would be on screen twice. -->
  <div class="password-strength" v-if="always || password.length > 0">
    <div class="strength-bar">
      <div
        class="strength-fill"
        :style="{ width: `${barPercent}%`, '--progress': strengthProgress }"
        :class="barClass"
      />
    </div>
    <!-- One line under the bar, and what wins it, in order: the answer the
         server gave about this password, then a rule the field itself can
         judge, then the strength. The server comes first because it is the only
         one of the three that knows something the browser cannot find out - a
         breach index is not reachable from here, the document allows
         connections to this origin only. -->
    <!-- The server's answer is a refusal and arrives after the reader has
         already left the field, so it is announced. The other two lines are
         not: the rule and the strength change under the reader's own typing,
         and a live region would read every keystroke back to him. -->
    <div v-if="lineText" :class="lineClass" :role="error ? 'alert' : undefined">
      {{ lineText }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = withDefaults(
  defineProps<{
    password: string;
    /** The password change form: the new password repeats the current one. */
    isSameAsOld?: boolean;
    /**
     * What the server said about this password on submit - a breach, above all
     * else. It takes the line and turns the bar red, because it is a refusal
     * and not advice: the password is long enough and still will not do.
     */
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

const meetsMinimum = computed(() => props.password.length >= 8);

const strengthText = computed(() => {
  const len = props.password.length;
  if (len < 10) return "Слабый";
  if (len < 13) return "Средний";
  return "Надежный";
});

const lineText = computed(() => {
  if (props.error) return props.error;
  if (!meetsMinimum.value) return "Минимум 8 символов";
  if (props.isSameAsOld) return "Новый пароль совпадает с текущим";
  return strengthText.value;
});

const hasError = computed(
  () => !meetsMinimum.value || props.isSameAsOld || !!props.error,
);

// An empty field is not a field that broke the rule: the reader has not typed
// anything yet, so the minimum is stated in the colour of a hint rather than of
// a refusal, and the bar is empty rather than fully red.
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
  return 40 + ((len - 8) / 7) * 60;
});

// Progress 0-1 for gradient (8 chars = 0, 15+ chars = 1)
const strengthProgress = computed(() => {
  const len = props.password.length;
  if (len < 8) return 0;
  return Math.min((len - 8) / 7, 1);
});

const barClass = computed(() => {
  if (hasError.value) return "error";
  return null;
});
</script>

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
  transition: width 0.3s ease, background-color 0.3s ease
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

// The verdict is advice, not a refusal, so it is written in the colour of a
// hint. The scale it belongs to is one thing said twice over — the bar carries
// the reading in colour and length, and a word repeating that colour beside it
// makes a second signal out of one fact, in a place where the only coloured
// text on these cards is a refusal.
.strength-text
  margin-top: $tiny
  font-size: $secondary-font-size
  color: $text-muted
</style>
