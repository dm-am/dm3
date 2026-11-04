<script setup lang="ts">
// The browser's own question for a closed or reloaded tab allows no custom text, so none is written.
import { onBeforeUnmount, onMounted, ref } from "vue";
import { onBeforeRouteLeave } from "vue-router";
import ConfirmDialog from "../ConfirmDialog/ConfirmDialog.vue";

const props = defineProps<{
  /** True while the form holds input that leaving would discard. */
  dirty: boolean;
}>();

const show = ref(false);
let decide: ((leave: boolean) => void) | null = null;

onBeforeRouteLeave(() => {
  if (!props.dirty) return true;
  show.value = true;
  return new Promise<boolean>((resolve) => {
    decide = resolve;
  });
});

function answer(leave: boolean) {
  show.value = false;
  decide?.(leave);
  decide = null;
}

// preventDefault is what asks it.
function warnOnUnload(event: BeforeUnloadEvent) {
  if (!props.dirty) return;
  event.preventDefault();
}

onMounted(() => window.addEventListener("beforeunload", warnOnUnload));
onBeforeUnmount(() => window.removeEventListener("beforeunload", warnOnUnload));
</script>

<template>
  <ConfirmDialog
    :show="show"
    title="Уйти со страницы?"
    message="Введенное на этой странице не сохранится"
    confirm-label="Уйти"
    @confirm="answer(true)"
    @cancel="answer(false)"
  />
</template>
