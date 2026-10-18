<script setup lang="ts">
/**
 * ModerationTickets — "Обращения" (doc 4.2.3.8.7 / 4.2.3.8.8): one queue of
 * every ticket the reader moderates, complaints, suggestions and the support
 * desk alike. What each role is shown is the server's to decide
 * (TicketVisibility): a moderator the complaints about users and the
 * suggestions, a senior moderator the complaints about a junior moderator's
 * decision as well, an administrator every reason.
 */
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import TicketList from "./tickets/TicketList.vue";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess, deniedText } = useRoleGate("Moderator");
</script>

<template>
  <div class="moderation-tickets">
    <page-title>Обращения</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <TicketList v-else />
  </div>
</template>
