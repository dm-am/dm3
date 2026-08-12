<script setup lang="ts">
import { ref, computed } from "vue";
import { blacklistApi } from "@/entities/user";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import type { BlacklistEntry } from "@/shared/api/models/personal";
import { describeFailure } from "@/shared/lib/errors";

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

  const { data, error: apiError } = await blacklistApi.blockUser({
    username: username.value.trim(),
  });

  loading.value = false;

  if (apiError) {
    error.value = describeFailure(
      apiError,
      "Не удалось заблокировать пользователя",
    );
    return;
  }

  if (data) {
    emit("success", data);
  }
}
</script>

<template>
  <Dialog narrow>
    <dialog-title>Заблокировать пользователя</dialog-title>

    <Form
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
          id="block-user-username"
          v-model="username"
          type="text"
          autocomplete="off"
          placeholder="Введите имя"
          :readonly="isUsernameLocked"
          :class="{ locked: isUsernameLocked }"
          @input="error = null"
        />
      </form-field>
    </Form>
  </Dialog>
</template>

<style scoped lang="sass">
input.locked
  background-color: $bg-element
  cursor: default
  opacity: 0.8
</style>
