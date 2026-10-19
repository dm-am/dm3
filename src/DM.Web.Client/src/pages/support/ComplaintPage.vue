<script setup lang="ts">
/**
 * The complaint form. The master's decisions in his own game, the complaints
 * moderation got most often, are answered over the form before they are
 * written. A complaint about a person is filed from an account, as on DM2.
 */
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { DISCORD_INVITE_URL } from "@/shared/config/contacts";
import { LoginPrompt } from "@/features/auth";
import { SupportTicketForm } from "@/features/support-ticket";
</script>

<template>
  <page-title>Жалоба</page-title>

  <LeadText class="complaint-lead"
    >Жалобу разбирают модераторы. Пишите, если кто-то нарушает
    <router-link :to="{ name: 'rules', hash: '#penalties' }"
      >правила сайта</router-link
    >: оскорбляет, рекламирует, флеймит, флудит, заводит разговор о политике,
    выкладывает мат и шок-контент без тега [nsfw]</LeadText
  >
  <p class="complaint-not">
    Решения мастера в его игре нарушением не считаются: подаваясь в игру, игроки
    принимают его условия
  </p>

  <SupportTicketForm kind="complaint">
    <template #login="{ action }">
      <LoginPrompt class="ticket-login" :action="action" />
    </template>
  </SupportTicketForm>

  <p class="discord-fallback">
    Модерация тоже читает
    <a :href="DISCORD_INVITE_URL" target="_blank" rel="noopener noreferrer"
      >Discord</a
    >: жалобу там рассмотрят так же, как и через форму
  </p>
</template>

<style scoped lang="sass">
// What is not a violation, in the grey of a remark under the first paragraph.
.complaint-lead
  margin-bottom: $small

.complaint-not
  margin: 0 0 $medium
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.45

// Flush with the lines of the note: its own padding is for a line that stands alone.
.login-prompt.ticket-login
  padding: 0

.discord-fallback
  margin-top: $medium
  color: $text-muted
  font-size: $secondary-font-size
</style>
