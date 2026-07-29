<script setup lang="ts">
/**
 * TicketCard — "Обращение" content block (product doc 4.2.2.23), the
 * moderation-side rendering. Header is the subtype, the author line is
 * either a profile link or the guest email, the status is a colored
 * indicator. In `preview` mode the body is clamped and the card links
 * to the ticket page.
 */
import { computed } from "vue";
import type { Ticket } from "@/shared/api/moderationApi";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import {
  TICKET_STATUS_CLASSES,
  TICKET_STATUS_LABELS,
  TICKET_SUBTYPE_LABELS,
} from "../lib/labels";

const props = withDefaults(
  defineProps<{
    ticket: Ticket;
    /** Clamp the body and render the header as a link to the ticket page. */
    preview?: boolean;
  }>(),
  { preview: false },
);

const statusLabel = computed(() => TICKET_STATUS_LABELS[props.ticket.status]);
const statusClass = computed(() => TICKET_STATUS_CLASSES[props.ticket.status]);
const subtypeLabel = computed(
  () => TICKET_SUBTYPE_LABELS[props.ticket.subtype],
);
</script>

<template>
  <article class="ticket-card">
    <header class="ticket-header">
      <h3 class="ticket-subtype">
        <router-link
          v-if="preview"
          :to="{
            name: 'moderation-ticket',
            params: { ticketId: ticket.id },
          }"
        >
          {{ subtypeLabel }}
        </router-link>
        <template v-else>{{ subtypeLabel }}</template>
      </h3>
      <span class="ticket-status" :class="statusClass">{{ statusLabel }}</span>
    </header>

    <div class="ticket-meta">
      <span v-if="ticket.reporterUsername">
        Автор обращения:
        <router-link
          :to="{
            name: 'profile',
            params: { username: ticket.reporterUsername },
          }"
        >
          {{ ticket.reporterUsername }}
        </router-link>
      </span>
      <span v-else-if="ticket.guestEmail">Email: {{ ticket.guestEmail }}</span>
      <span v-else class="muted">Аноним</span>

      <span v-if="ticket.targetUsername">
        На пользователя:
        <router-link
          :to="{ name: 'profile', params: { username: ticket.targetUsername } }"
        >
          {{ ticket.targetUsername }}
        </router-link>
      </span>

      <span class="ticket-date">{{ formatDateFull(ticket.createdUtc) }}</span>
    </div>

    <p v-if="ticket.comment" class="ticket-subject">{{ ticket.comment }}</p>

    <p class="ticket-body" :class="{ 'is-preview': preview }">
      {{ ticket.description }}
    </p>

    <div v-if="ticket.assignedModeratorUsername" class="ticket-assigned">
      В работе у:
      <router-link
        :to="{
          name: 'profile',
          params: { username: ticket.assignedModeratorUsername },
        }"
      >
        {{ ticket.assignedModeratorUsername }}
      </router-link>
    </div>

    <div v-if="ticket.hasWarning || ticket.hasBan" class="ticket-sanctions">
      <span v-if="ticket.hasWarning">Выдано предупреждение</span>
      <span v-if="ticket.hasBan">Выдан бан</span>
    </div>
  </article>
</template>

<style scoped lang="sass">
.ticket-card
  +card()

.ticket-header
  display: flex
  align-items: baseline
  justify-content: space-between
  gap: $small
  margin-bottom: $small

.ticket-subtype
  margin: 0
  font-size: $font-size
  font-weight: 600

.ticket-status
  flex-shrink: 0
  font-size: $secondary-font-size

  &.status-waiting-moderation
    color: $accent-green

  &.status-waiting-user
    color: $text

  &.status-closed
    color: $text-muted

  &.status-spam
    color: $accent-red

.ticket-meta
  display: flex
  flex-wrap: wrap
  gap: $small $medium
  margin-bottom: $small
  color: $text-muted
  font-size: $secondary-font-size

.ticket-date
  margin-left: auto

.ticket-subject
  margin: 0 0 $small
  font-weight: 500

.ticket-body
  margin: 0
  white-space: pre-line
  overflow-wrap: anywhere

  &.is-preview
    display: -webkit-box
    -webkit-line-clamp: 3
    -webkit-box-orient: vertical
    overflow: hidden

.ticket-assigned
  margin-top: $small
  color: $text-muted
  font-size: $secondary-font-size

.ticket-sanctions
  display: flex
  gap: $medium
  margin-top: $small
  color: $accent-red
  font-size: $secondary-font-size

.muted
  color: $text-muted
</style>
