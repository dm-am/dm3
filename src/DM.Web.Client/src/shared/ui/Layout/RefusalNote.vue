<script setup lang="ts">
/**
 * RefusalNote — the line that tells the reader he may not write here, or has
 * to sign in first ("Войдите, чтобы ..."), standing where the form would be.
 *
 * Drawn as a plaque, DM2's span.lightbg: the beige ground and the dashed frame
 * the reader already knows from an opened spoiler, ordinary text of the body
 * size. That is what tells it from an empty-state line ("Комментариев пока
 * нет"), which stays grey SecondaryText: on DM2 the plaque means a refusal and
 * nothing else.
 *
 * It spans the column, as the form it stands for would, and keeps no margin:
 * where it stands among the blocks of a page is the caller's.
 */
</script>

<template>
  <div class="refusal-note"><slot /></div>
</template>

<style scoped lang="sass">
// DM2's .lightbg: padding 5px, 1px dashed frame, 16px text in the ink of the
// body. 5 is DM2's literal, the one the spoiler keeps (_BbcodeContent.sass),
// deliberately off the 4px grid. Square: it is content, not a control.
.refusal-note
  box-sizing: border-box
  width: 100%
  padding: 5px
  border: 1px dashed $border
  background-color: $bg-highlight-yellow
  color: $text
  font-size: $font-size
  font-weight: normal
</style>
