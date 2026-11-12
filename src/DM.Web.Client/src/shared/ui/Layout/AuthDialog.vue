<script setup lang="ts">
// The dialog every guest form opens in: the vue-final-modal tier underneath,
// the guest surface on top of it.
//
// It exists so that the seven guest forms do not each re-derive the same three
// facts — how wide the card is, that it is painted by AuthSurface.sass and not
// by the dialog wrapper, and that the visible way out is the cross. The grey
// footer of Form.vue is not part of this contour: the guest language has no
// "Отмена" button, and the main action stands in the body of the card, naming
// its result.
//
// The cross is drawn out of two bars rather than typed, and not with the
// glyph the site uses elsewhere: a typographic cross is thinner than the marks it
// stands beside, and how much thinner depends on which font actually loaded.
// The bars come from the surface; the one rule here is the cross locked while
// a request runs, which only this component knows about.
import { nextTick, ref, watch } from "vue";
import Dialog from "./Dialog.vue";

const props = defineProps<{
  /**
   * The narrow card, for a dialog that asks one short question or states one
   * outcome. The host says so of itself: the sheet cannot read the contents of
   * a card to work the width out, because the width of a dialog sits on the
   * wrapper and a wrapper asking about its card would need one :has inside
   * another. The word goes on the wrapper and on the card together, so the lit
   * rectangle and the card are one rectangle and a click beside the card
   * closes the dialog.
   */
  compact?: boolean;
  /** Centred body, for a card that states an outcome rather than takes input. */
  center?: boolean;
  /**
   * The form's request is in flight. The window is locked as every site window
   * is (Dialog.vue): the cross, Escape, a click beside the card and the card's
   * own links do nothing, since a form taken away mid-request loses its answer.
   */
  busy?: boolean;
}>();

const emit = defineEmits<{
  /** The cross was pressed. The host that opened the modal closes it. */
  close: [];
  /** Forwarded from the tier below, for callers that reset their form. */
  beforeClose: [];
}>();

const cross = ref<HTMLButtonElement | null>(null);

const close = () => {
  if (!props.busy) emit("close");
};

// Sync, before the render shuts the pressed button: a disabled control drops
// the caret onto <body>, outside the window. It waits on the cross, as in
// Dialog.vue, and goes back once the button opens again.
let pressed: HTMLButtonElement | null = null;
watch(
  () => props.busy,
  (busy) => {
    const spare = cross.value;
    if (busy) {
      const active = document.activeElement;
      if (!spare || !(active instanceof HTMLButtonElement) || active === spare)
        return;
      pressed = active;
      spare.focus();
      return;
    }
    const back = pressed;
    pressed = null;
    if (!back) return;
    void nextTick(() => {
      if (
        document.activeElement === spare &&
        back.isConnected &&
        !back.disabled
      )
        back.focus();
    });
  },
  { flush: "sync" },
);

/** The links of the card lead into another window or page: the answer would arrive nowhere. */
function holdLinks(e: MouseEvent) {
  if (!props.busy) return;
  if (!(e.target as Element | null)?.closest?.("a[href], .auth-action")) return;
  e.preventDefault();
  e.stopPropagation();
}
</script>

<template>
  <!-- The library's own Escape and click beside the card, which `auto` keeps:
       these attributes fall through onto it and override Dialog's. The held
       links ride the same way, onto the library's frame. -->
  <Dialog
    auto
    :click-to-close="!busy"
    :esc-to-close="!busy"
    @click.capture="holdLinks"
    @before-close="emit('beforeClose')"
  >
    <div
      class="auth-surface auth-dialog"
      :class="{ 'auth-dialog--compact': compact }"
    >
      <div
        class="auth-card"
        :class="{
          'auth-card--center': center,
          'auth-card--compact': compact,
        }"
      >
        <!-- Empty on purpose: the two bars of the cross are drawn by the
             surface, and a glyph inside would stand behind them. -->
        <!-- aria-disabled and not disabled: it holds the caret the pressed button lost. -->
        <button
          ref="cross"
          type="button"
          class="auth-close"
          aria-label="Закрыть"
          :aria-disabled="busy || undefined"
          @click="close"
        ></button>
        <slot />
      </div>
    </div>
  </Dialog>
</template>

<style lang="sass">
// The window's locked cross (+dialog-close), on the guest card's.
.auth-surface .auth-close[aria-disabled="true"]
  opacity: $disabled-opacity
  cursor: default

  &:hover
    color: $text-muted
</style>
