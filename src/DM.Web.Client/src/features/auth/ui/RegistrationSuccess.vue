<script setup lang="ts">
import { AuthDialog } from "@/shared/ui/Layout";
import LetterReceipt from "./LetterReceipt.vue";

defineProps<{
  email: string;
  /** The lifetime of the activation link, from the answer to the registration. */
  hours?: number;
}>();

const emit = defineEmits<{
  (e: "confirm"): void;
}>();
</script>

<template>
  <AuthDialog compact center @close="emit('confirm')">
    <LetterReceipt :address="email" :hours="hours">
      Мы отправили письмо со ссылкой для активации на
    </LetterReceipt>
  </AuthDialog>
</template>
