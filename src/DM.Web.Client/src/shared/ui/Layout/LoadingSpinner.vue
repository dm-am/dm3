<template>
  <div class="loading-spinner">
    <div class="spinner"></div>
  </div>
</template>

<style scoped lang="sass">
// Variables are injected globally via vite.config.ts additionalData

.loading-spinner
  display: flex
  justify-content: center
  align-items: center
  padding: $medium

.spinner
  width: 32px
  height: 32px
  border: 3px solid $border
  border-top-color: $link
  border-radius: 50%
  animation: spin 0.8s linear infinite

@keyframes spin
  to
    transform: rotate(360deg)
</style>
