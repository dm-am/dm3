<script setup lang="ts">
/**
 * The way to the administration: security errors and account trouble; guests
 * may write too. ?subtype=<TicketSubtype> (supportFor) opens the form on a
 * reason, and the page draws the one line of the note the form cannot, the
 * site's "Войдите, чтобы" for a reason filed from an account.
 */
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { LoginPrompt } from "@/features/auth";
import { SupportTicketForm } from "@/features/support-ticket";
</script>

<template>
  <page-title>Поддержка</page-title>

  <LeadText
    ><strong>ВНИМАНИЕ!</strong><br />Это обращение отправится только
    администрации сайта, поэтому настоятельно рекомендуем сообщать здесь только
    об ошибках безопасности (например, у вас появилась возможность редактировать
    чужие профили) и о проблемах с аккаунтом (например, вы не можете войти или
    ваш аккаунт забанен, а написать о проблеме нужно). В других ситуациях лучше
    создать топик на форуме в разделе "<router-link
      :to="{ name: 'forum', params: { alias: 'bugs' } }"
      >Ошибки</router-link
    >", наше отзывчивое сообщество, скорее всего, быстро вам поможет.
    Предложения по развитию сайта обсуждаются там же, в разделе "<router-link
      :to="{ name: 'forum', params: { alias: 'improvements' } }"
      >Улучшение сайта</router-link
    >". Вопросы о том, как все устроено, задают
    <router-link :to="{ name: 'community', query: { role: 'Mentor' } }"
      >наставникам</router-link
    >
    или в разделе "<router-link
      :to="{ name: 'forum', params: { alias: 'newbies' } }"
      >Для новичков</router-link
    >".</LeadText
  >

  <SupportTicketForm kind="support">
    <template #login="{ action }">
      <LoginPrompt class="ticket-login" :action="action" />
    </template>
  </SupportTicketForm>
</template>

<style scoped lang="sass">
// Flush with the lines of the note: its own padding is for a line that stands alone.
.login-prompt.ticket-login
  padding: 0
</style>
