<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { trackHash } from "@/entities/ticket";
import { permalinkOrigin } from "@/shared/config/site";

const props = defineProps<{
  title: string;
  again: string;
  trackingToken: string | null;
  ticketId: string | null;
}>();

defineEmits<{ again: [] }>();

// Spelled out in full: the guest's only way back to the answer.
const router = useRouter();
const trackingAddress = computed(() =>
  props.trackingToken
    ? permalinkOrigin() +
      router.resolve({
        name: "support-track",
        hash: trackHash(props.trackingToken),
      }).href
    : "",
);

const confirmation = ref<HTMLElement | null>(null);
onMounted(() => confirmation.value?.focus({ preventScroll: true }));
</script>

<template>
  <div ref="confirmation" class="ticket-sent" role="status" tabindex="-1">
    <p class="ticket-sent-title">{{ title }}</p>

    <p v-if="trackingToken" class="ticket-sent-text">
      Ответ появится на странице обращения:
      <router-link
        class="ticket-sent-address"
        :to="{ name: 'support-track', hash: trackHash(trackingToken) }"
        >{{ trackingAddress }}</router-link
      >. Сохраните ссылку, чтобы вернуться к обращению.
    </p>

    <p v-else-if="ticketId" class="ticket-sent-text">
      Ответ появится на
      <router-link :to="{ name: 'my-ticket', params: { ticketId } }"
        >странице обращения</router-link
      >
    </p>

    <p v-else class="ticket-sent-text">
      Ответ появится в разделе "<router-link :to="{ name: 'my-tickets' }"
        >Мои обращения</router-link
      >"
    </p>

    <p class="ticket-sent-again">
      <button type="button" class="again-button" @click="$emit('again')">
        {{ again }}
      </button>
    </p>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.ticket-sent
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small

  // Focused by the script when it replaces the form; a mouse press brings no ring.
  &:focus:not(:focus-visible)
    outline: none

  // The colour and the underline come of the site's link; only the weight is its own.
  a
    font-weight: 700

.ticket-sent-title
  margin: 0
  font-weight: 700

.ticket-sent-text,
.ticket-sent-again
  margin: 0

// One word of eighty letters: it breaks where the column ends.
.ticket-sent-address
  overflow-wrap: anywhere

.again-button
  +inline-link-button
</style>
