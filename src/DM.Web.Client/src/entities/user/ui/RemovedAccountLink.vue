<script setup lang="ts">
/**
 * "Аккаунт удален" in the place of a user link, for an author, a master or an
 * owner that arrives as null.
 *
 * The server keeps what a removed account wrote and names nobody over it, and
 * every such name leads to one profile, the one that gathers everything the
 * removed accounts left. It reads as every other name of its place does: no
 * colour of its own, the link's, with the site's link hover over it (owner,
 * 12 October 2026; it stood grey before). A class the place gives it falls
 * through to the link.
 */
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
