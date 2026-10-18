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
// The bars come from the surface, so the component carries no style block.
import Dialog from "./Dialog.vue";

defineProps<{
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
}>();

const emit = defineEmits<{
  /** The cross was pressed. The host that opened the modal closes it. */
  close: [];
  /** Forwarded from the tier below, for callers that reset their form. */
  beforeClose: [];
}>();
</script>

<template>
  <Dialog auto @before-close="emit('beforeClose')">
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
        <button
          type="button"
          class="auth-close"
          aria-label="Закрыть"
          @click="emit('close')"
        ></button>
        <slot />
      </div>
    </div>
  </Dialog>
</template>
