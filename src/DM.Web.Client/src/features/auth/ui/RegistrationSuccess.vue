<script setup lang="ts">
import { onMounted } from "vue";
import Lightbox from "@/shared/ui/Layout/Lightbox.vue";
import LightboxTitle from "@/shared/ui/Layout/LightboxTitle.vue";

const props = defineProps<{
  email: string;
}>();

defineEmits<{
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
  <Lightbox>
    <div class="success-content">
      <lightbox-title>Проверьте почту</lightbox-title>

      <p class="main-text">
        Мы отправили письмо на <strong>{{ email }}</strong> со ссылкой для
        активации.
      </p>

      <p class="expiry-note">Ссылка действительна 48 часов</p>
    </div>
  </Lightbox>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.success-content
  text-align: center

.main-text
  margin: 0 0 $small
  line-height: 1.5

.expiry-note
  margin: 0
  color: $text-muted
  font-size: $secondary-font-size
</style>

<style lang="sass">
// Global style for success lightbox (teleported modal)
.lightbox:has(.success-content)
  width: auto !important
  max-width: 320px !important
</style>
