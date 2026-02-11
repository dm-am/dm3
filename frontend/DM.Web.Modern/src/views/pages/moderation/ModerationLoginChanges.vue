<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useToast } from "@/composables/useToast";

const toast = useToast();
const requests = ref<any[]>([]);
const loading = ref(false);

onMounted(async () => {
  // TODO: Fetch login change requests from API when endpoint is available
  // GET /v1/moderation/login-changes
});
</script>

<template>
  <div class="login-changes">
    <h3>Запросы на смену имени пользователя</h3>
    <secondary-text v-if="!requests.length && !loading">Нет активных запросов</secondary-text>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.login-changes
  h3
    color: $text
    margin-bottom: $medium
</style>
