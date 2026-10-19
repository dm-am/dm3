<script setup lang="ts">
// An empty setup still names the component, and tests stub it by that name.
</script>

<template>
  <h1><slot /></h1>
</template>

<style scoped lang="sass">
h1
  margin: $medium 0 $small
  font-size: $title-font-size
  font-weight: 700
  text-transform: uppercase
  letter-spacing: 0.5px
  color: $heading
</style>
