<script setup lang="ts">
/**
 * Stops a navigation that would throw away what the viewer has typed.
 *
 * The BBCode editor autosaves under a `draft-key`, and that covers the
 * composers. It does not cover the forms where the text is longest — the
 * character sheet, the description of a game, the description of a blog, an
 * edit of a publication — nor the plain fields beside them: a name, a title,
 * a chosen rubric. The sidebar of a game is one click away on every page of
 * it, the navigation is instant and silent, and the form comes back empty.
 * There was no onBeforeRouteLeave and no beforeunload anywhere in the client.
 *
 * Mounted next to a form; the form says whether it is dirty. Both exits are
 * covered: an in-app navigation, asked in the app's own ConfirmDialog, and
 * closing or reloading the tab, asked by the browser — which allows no custom
 * text, so none is written here.
 */
import { onBeforeUnmount, onMounted, ref } from "vue";
import { onBeforeRouteLeave } from "vue-router";
import ConfirmDialog from "../ConfirmDialog/ConfirmDialog.vue";

const props = defineProps<{
  /** True while the form holds input that leaving would discard. */
  dirty: boolean;
}>();

const show = ref(false);
/** Resolver of the navigation currently held open, if any. */
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

// The browser's own question for a closed or reloaded tab. preventDefault is
// what asks it; the wording belongs to the browser.
function warnOnUnload(event: BeforeUnloadEvent) {
  if (!props.dirty) return;
  event.preventDefault();
}

onMounted(() => window.addEventListener("beforeunload", warnOnUnload));
onBeforeUnmount(() => window.removeEventListener("beforeunload", warnOnUnload));

// Exposed for the test: onBeforeRouteLeave needs a live router to fire.
defineExpose({ answer });
</script>

<template>
  <ConfirmDialog
    :show="show"
    title="Уйти со страницы?"
    message="Введенное на этой странице не сохранится."
    confirm-label="Уйти"
    cancel-label="Остаться"
    danger
    @confirm="answer(true)"
    @cancel="answer(false)"
  />
</template>
