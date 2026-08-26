<script setup lang="ts">
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Button from "@/shared/ui/Button/Button.vue";

defineProps<{
  email: string;
}>();

const emit = defineEmits<{
  (e: "confirm"): void;
}>();
</script>

<template>
  <Dialog auto>
    <div>
      <dialog-title>Проверьте почту</dialog-title>

      <p class="main-text">
        Мы отправили письмо на <strong>{{ email }}</strong> со ссылкой для
        активации.
      </p>

      <p class="expiry-note">Ссылка действительна 48 часов</p>

      <Button type="button" @click="emit('confirm')"> Закрыть </Button>
    </div>
  </Dialog>
</template>

<style scoped lang="sass">
.main-text
  margin: 0 0 $small
  line-height: 1.5

.expiry-note
  margin: 0 0 $medium
  color: $text-muted
  font-size: $secondary-font-size
</style>
