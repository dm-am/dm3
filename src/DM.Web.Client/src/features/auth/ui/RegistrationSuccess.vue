<script setup lang="ts">
// The card that ends the registration: the letter is out, and there is nothing
// left to do here. The cross closes it; a button that only repeats the cross
// took the place of a main action this card does not have.
import { AuthDialog } from "@/shared/ui/Layout";

defineProps<{
  email: string;
}>();

const emit = defineEmits<{
  (e: "confirm"): void;
}>();
</script>

<template>
  <AuthDialog compact center @close="emit('confirm')">
    <h2 class="auth-title">Проверьте почту</h2>

    <p class="auth-text">
      Мы отправили письмо со ссылкой для активации на
      <strong>{{ email }}</strong>
    </p>

    <p class="auth-note">Ссылка действительна 48 часов</p>
  </AuthDialog>
</template>
