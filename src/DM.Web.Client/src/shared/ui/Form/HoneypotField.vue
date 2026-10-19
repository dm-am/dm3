<script setup lang="ts">
/**
 * The field of the bot trap (useBotTrap): off the page and out of the tab
 * order, so a person never reaches it, and named "website", a name a bot fills
 * in on sight. The rule that takes it off the page is .honeypot-field in
 * InputsGlobal.sass.
 */
const value = defineModel<string>({ required: true });
</script>

<template>
  <input
    name="website"
    v-model="value"
    class="honeypot-field"
    autocomplete="off"
    tabindex="-1"
    aria-hidden="true"
  />
</template>
