<script setup lang="ts">
/**
 * ComplaintPage - complaint form (rule violations, complaints about other
 * users). Hosts the real submission form (SupportTicketForm) with the
 * complaint wording and an optional violation link; guests are allowed
 * to submit.
 */
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { DISCORD_INVITE_URL } from "@/shared/config/contacts";
import { SupportTicketForm } from "@/features/support-ticket";
</script>

<template>
  <page-title>Жалоба</page-title>

  <LeadText
    >Если кто-то нарушает правила, опишите ситуацию — модерация
    разберется</LeadText
  >

  <SupportTicketForm kind="complaint" />

  <p class="discord-fallback">
    Модерация тоже читает
    <a :href="DISCORD_INVITE_URL" target="_blank" rel="noopener noreferrer"
      >Discord</a
    >
    — жалобу там рассмотрят так же, как и через форму
  </p>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Variables"
@import "@/assets/styles/Themes"

.discord-fallback
  margin-top: $medium
  color: $text-muted
  font-size: $secondary-font-size

  a
    color: $link

    &:hover
      color: $link-hover
</style>
