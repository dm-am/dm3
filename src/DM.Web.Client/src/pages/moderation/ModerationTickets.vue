<script setup lang="ts">
/** One queue of every ticket the reader moderates; what each role is shown is the server's (TicketVisibility). */
import TicketList from "./tickets/TicketList.vue";
</script>

<template>
  <div class="moderation-tickets">
    <page-title>Обращения</page-title>

    <TicketList />
  </div>
</template>
