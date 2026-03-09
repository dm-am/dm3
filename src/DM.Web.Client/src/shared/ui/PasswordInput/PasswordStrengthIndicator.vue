<template>
  <div class="password-strength" v-if="password.length > 0">
    <div class="strength-bar">
      <div
        class="strength-fill"
        :style="{ width: `${barPercent}%`, '--progress': strengthProgress }"
        :class="barClass"
      />
    </div>
    <div v-if="warningText" class="warning-text">{{ warningText }}</div>
    <div v-else-if="strengthText" class="strength-text" :style="{ '--progress': strengthProgress }">{{ strengthText }}</div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

export type HibpStatus = 'idle' | 'checking' | 'compromised' | 'safe';

const props = withDefaults(defineProps<{
  password: string;
  hibpStatus?: HibpStatus;
  isSameAsOld?: boolean;
}>(), {
  hibpStatus: 'idle',
  isSameAsOld: false
});

const meetsMinimum = computed(() => props.password.length >= 8);

const warningText = computed(() => {
  if (!meetsMinimum.value) return 'Минимум 8 символов';
  if (props.hibpStatus === 'compromised') return 'Пароль найден в утечках данных';
  if (props.isSameAsOld) return 'Новый пароль совпадает с текущим';
  return null;
});

const strengthText = computed(() => {
  if (!meetsMinimum.value || hasError.value) return null;
  const len = props.password.length;
  if (len < 10) return 'Слабый';
  if (len < 13) return 'Средний';
  return 'Надежный';
});

const hasError = computed(() =>
  !meetsMinimum.value ||
  props.hibpStatus === 'compromised' ||
  props.isSameAsOld
);

const barPercent = computed(() => {
  const len = props.password.length;
  if (len === 0) return 0;
  if (hasError.value) return 100;
  if (len < 8) return (len / 8) * 40;
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
  if (hasError.value) return 'error';
  return null;
});
</script>

<style scoped lang="sass">
@import "@/assets/styles/Variables"
@import "@/assets/styles/Themes"

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

.strength-text
  margin-top: $tiny
  font-size: $secondary-font-size
  color: color-mix(in srgb, $accent-yellow calc((1 - var(--progress)) * 100%), $accent-green calc(var(--progress) * 100%))
</style>
