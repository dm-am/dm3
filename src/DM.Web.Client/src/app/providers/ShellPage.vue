<script setup lang="ts">
/**
 * The `page` slot of the application shell.
 *
 * Every top-level route is a child of one record that mounts the sidebars, so a
 * new page cannot forget them. This component is what that record puts in the
 * `page` outlet: it renders the matched child and nothing else — no wrapper
 * element, no styles, no layout of its own.
 */
</script>

<template>
  <router-view />
</template>
