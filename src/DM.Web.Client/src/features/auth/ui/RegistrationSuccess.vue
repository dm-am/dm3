<script setup lang="ts">
import { onMounted } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Button from "@/shared/ui/Button/Button.vue";

const props = defineProps<{
  email: string;
}>();

const emit = defineEmits<{
  (e: "confirm"): void;
}>();

// Save email to sessionStorage for pre-filling in ActivationPage if needed
onMounted(() => {
  if (props.email) {
    sessionStorage.setItem("dm_registration_email", props.email);
  }
});
</script>

<template>
  <Dialog auto>
    <div class="success-content">
      <dialog-title>Проверьте почту</dialog-title>

      <p class="main-text">
        Мы отправили письмо на <strong>{{ email }}</strong> со ссылкой для
        активации.
      </p>

      <p class="expiry-note">Ссылка действительна 48 часов</p>

      <Button type="button" class="confirm-btn" @click="emit('confirm')">
        Закрыть
      </Button>
    </div>
  </Dialog>
</template>

<style scoped lang="sass">
.success-content
  text-align: center

.main-text
  margin: 0 0 $small
  line-height: 1.5

.expiry-note
  margin: 0 0 $medium
  color: $text-muted
  font-size: $secondary-font-size

.confirm-btn
  width: 100%
</style>
