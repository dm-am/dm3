<script setup lang="ts">
import { linkLifetimeNote } from "../lib/linkLifetime";

defineProps<{
  address: string;
  /** How long the link lives, as the answer that sent the letter named it. */
  hours?: number;
}>();
</script>

<template>
  <h2 class="auth-title">Проверьте почту</h2>
  <p class="auth-text">
    <slot />
    <strong>{{ address }}</strong>
  </p>
  <!-- No hours, no promise: a number of the client's own could be another link's. -->
  <p v-if="hours !== undefined" class="auth-note">
    {{ linkLifetimeNote(hours) }}
  </p>
</template>
