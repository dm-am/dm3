<script setup lang="ts">
// In the place of a user link for a null author; it reads as every other name of its place.
import {
  REMOVED_ACCOUNT_NAME,
  removedAccountRoute,
} from "../lib/removedAccount";
</script>

<template>
  <router-link :to="removedAccountRoute">{{
    REMOVED_ACCOUNT_NAME
  }}</router-link>
</template>
