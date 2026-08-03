<script setup lang="ts">
/**
 * ModerationComplaints — "Жалобы" (doc 4.2.3.8.8). Complaint and
 * suggestion tickets: "Жалоба на пользователя", "Жалоба на решение
 * младшего/старшего модератора", "Предложение по улучшению сайта".
 * Moderator+ — the backend narrows which complaint subtypes each role
 * actually sees (senior moderators see junior-decision complaints,
 * admins see everything).
 */
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import TicketList from "./tickets/TicketList.vue";
import { COMPLAINT_SUBTYPES } from "./lib/labels";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess, deniedText } = useRoleGate("Moderator");
</script>

<template>
  <div class="moderation-complaints">
    <page-title>Жалобы</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <TicketList
      v-else
      :subtypes="COMPLAINT_SUBTYPES"
      empty-text="Жалоб пока нет"
    />
  </div>
</template>
