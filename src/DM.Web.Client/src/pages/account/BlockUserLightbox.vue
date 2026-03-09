<script setup lang="ts">
import { ref, computed } from "vue";
import { BlacklistApi } from "@/shared/api";
import TheLightbox from "@/shared/ui/Layout/TheLightbox.vue";
import LightboxTitle from "@/shared/ui/Layout/LightboxTitle.vue";
import TheForm from "@/shared/ui/Form/TheForm.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import type { BlacklistEntry } from "@/shared/api/models/personal";

const props = defineProps<{
  initialUsername?: string;
}>();

const emit = defineEmits<{
  (e: "success", entry: BlacklistEntry): void;
  (e: "cancel"): void;
}>();

const username = ref(props.initialUsername || "");
const isUsernameLocked = computed(() => !!props.initialUsername);
const loading = ref(false);
const error = ref<string | null>(null);

const canSubmit = computed(() => username.value.trim().length >= 2);

async function submit() {
  if (!canSubmit.value) return;

  loading.value = true;
  error.value = null;

  const { data, error: apiError } = await BlacklistApi.blockUser({
    username: username.value.trim()
  });

  loading.value = false;

  if (apiError) {
    error.value = apiError.message || "Не удалось заблокировать пользователя";
    return;
  }

  if (data) {
    emit("success", data);
  }
}
</script>

<template>
  <the-lightbox narrow>
    <lightbox-title>Заблокировать пользователя</lightbox-title>

    <the-form
      @submit="submit"
      @cancel="emit('cancel')"
      :valid="canSubmit"
      :loading="loading"
      action="Заблокировать"
      cancel="Отмена"
    >
      <form-field
        label="Имя пользователя"
        name="username"
        :errors="error ? [error] : []"
      >
        <input
          v-model="username"
          type="text"
          autocomplete="off"
          placeholder="Введите имя..."
          :readonly="isUsernameLocked"
          :class="{ locked: isUsernameLocked }"
          @input="error = null"
        />
      </form-field>
    </the-form>
  </the-lightbox>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

input.locked
  background-color: $bg-element
  cursor: not-allowed
  opacity: 0.8
</style>
