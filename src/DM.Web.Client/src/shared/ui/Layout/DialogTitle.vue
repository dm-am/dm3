<template>
  <h2><slot /></h2>
</template>

<style scoped lang="sass">
@use "@/assets/styles/DialogSurface" as *

// The heading of a window with a form: the site's heading style from the
// surface, on the left, with the cross's corner kept free on the right so a
// long heading wraps before it runs under the cross. The step under it is the
// step between two fields, so the first field stands where any other would.
h2
  +dialog-title
  margin-bottom: $dialog-field-step
  padding-right: $dialog-close-reserve
</style>
