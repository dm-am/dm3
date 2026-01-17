<template>
  <h4><slot /></h4>
</template>

<script setup lang="ts"></script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

h4
  margin: $medium 0 $small
  font-size: $font-size
  font-weight: bold
  text-transform: uppercase
  letter-spacing: 0.5px
  +theme(color, $muted-text)
  transition: color 0.15s ease
</style>
