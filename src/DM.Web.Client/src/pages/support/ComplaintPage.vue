<script setup lang="ts">
/**
 * ComplaintPage - the complaint form: a person who breaks the rules, or a
 * moderator's decision the reader disputes. Hosts the one ticket form
 * (SupportTicketForm) with the complaint wording.
 *
 * Over the form, what a complaint is for, in two paragraphs: what the rules
 * count as a violation, with the table of violations one link away, and what
 * they do not, in the words of the rules on authors' decisions: "Мастер ведет
 * как считает нужным, а игроки, подаваясь в игру, принимают его условия". The
 * complaints that reached moderation most often, against a master's decision
 * in his own game, are answered there before they are written.
 *
 * A complaint about a person is filed from an account, as on DM2; the page
 * draws the site's "Войдите, чтобы" for it, and the form says where a reader
 * whose account is banned goes instead.
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
@use "@/assets/styles/Variables" as *
@use "@/assets/styles/Themes" as *

// The second paragraph of the lead: what is not a violation, in the grey of a
// remark under the first, a small step below it.
.complaint-lead
  margin-bottom: $small

.complaint-not
  margin: 0 0 $medium
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.45

// The site's sign-in line stands among the lines of the note, flush with them:
// its own padding is for a line that stands alone under a block.
.login-prompt.ticket-login
  padding: 0

.discord-fallback
  margin-top: $medium
  color: $text-muted
  font-size: $secondary-font-size

  a
    color: $link

    &:hover
      color: $link-hover
</style>
