<script setup lang="ts">
// An empty setup still names the component, and tests stub it by that name.
</script>

<template>
  <h2><slot /></h2>
</template>

<style scoped lang="sass">
h2
  margin: $medium 0 $small
  font-size: $font-size
  font-weight: 700
  text-transform: uppercase
  letter-spacing: 0.5px
  color: $heading
</style>
