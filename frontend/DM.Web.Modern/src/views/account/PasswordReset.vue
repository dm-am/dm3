<script setup lang="ts">
import LightboxTitle from "@/components/layout/LightboxTitle.vue";

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();
</script>

<template>
  <the-lightbox :with-form="true">
    <lightbox-title>Восстановление пароля</lightbox-title>
    <secondary-text>Функция в разработке</secondary-text>
    <the-button @click="emit('cancel')">Закрыть</the-button>
  </the-lightbox>
</template>
