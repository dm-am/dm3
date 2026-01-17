<template>
  <h3><slot /></h3>
</template>

<script setup lang="ts"></script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

h3
  margin: $medium 0 $small
  font-size: $font-size
  font-weight: bold
  text-transform: uppercase
  letter-spacing: 0.5px
  +theme(color, $accent-text)
</style>
