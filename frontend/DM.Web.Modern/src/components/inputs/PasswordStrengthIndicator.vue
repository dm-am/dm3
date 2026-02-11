<template>
  <div class="password-strength" v-if="password.length > 0">
    <div class="strength-bar">
      <div
        class="strength-fill"
        :style="{ width: `${strengthPercent}%` }"
        :class="strengthClass"
      />
    </div>
    <span class="strength-label" :class="strengthClass">{{ strengthLabel }}</span>
    <ul class="strength-rules">
      <li :class="{ met: rules.minLength }">Минимум 10 символов</li>
      <li :class="{ met: rules.hasUpper }">Заглавная буква</li>
      <li :class="{ met: rules.hasLower }">Строчная буква</li>
      <li :class="{ met: rules.hasDigit }">Цифра</li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  password: string;
}>();

const rules = computed(() => ({
  minLength: props.password.length >= 10,
  hasUpper: /[A-ZА-Яе]/.test(props.password),
  hasLower: /[a-zа-яе]/.test(props.password),
  hasDigit: /\d/.test(props.password),
}));

const metCount = computed(() =>
  Object.values(rules.value).filter(Boolean).length,
);

const strengthPercent = computed(() => (metCount.value / 4) * 100);

const strengthClass = computed(() => {
  if (metCount.value <= 1) return "weak";
  if (metCount.value <= 2) return "fair";
  if (metCount.value <= 3) return "good";
  return "strong";
});

const strengthLabel = computed(() => {
  if (metCount.value <= 1) return "Слабый";
  if (metCount.value <= 2) return "Средний";
  if (metCount.value <= 3) return "Хороший";
  return "Надежный";
});
</script>

<style scoped lang="sass">
.password-strength
  margin-top: 0.25rem

.strength-bar
  height: 4px
  background: var(--bg-secondary, #e0e0e0)
  border-radius: 2px
  overflow: hidden
  margin-bottom: 0.25rem

.strength-fill
  height: 100%
  border-radius: 2px
  transition: width 0.3s ease, background-color 0.3s ease

  &.weak
    background-color: var(--accent-red, #f44336)
  &.fair
    background-color: var(--text-muted, #999)
  &.good
    background-color: var(--accent-green-muted, #8a9a8a)
  &.strong
    background-color: var(--accent-green, #4caf50)

.strength-label
  font-size: 0.8rem
  &.weak
    color: var(--accent-red, #f44336)
  &.fair
    color: var(--text-muted, #999)
  &.good
    color: var(--accent-green-muted, #8a9a8a)
  &.strong
    color: var(--accent-green, #4caf50)

.strength-rules
  list-style: none
  padding: 0
  margin: 0.25rem 0 0
  font-size: 0.75rem
  color: var(--text-muted, #999)

  li
    &::before
      content: "✗ "
      color: var(--accent-red, #f44336)
    &.met::before
      content: "✓ "
      color: var(--accent-green, #4caf50)
    &.met
      color: var(--text-secondary, #666)
</style>
