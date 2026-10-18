<script setup lang="ts">
/**
 * SupportPage - the way to the administration: security errors of the site
 * and trouble with an account. Guests may write too.
 *
 * The words are the old site's error report page, carried over where the site
 * works the same: who reads what is sent, what belongs here, and that the rest
 * is a topic in the forum's "Ошибки", where the community answers. What
 * differs is said the way the site says it: the reader is the administration,
 * the forum entity is a "топик", and suggestions go to "Улучшение сайта". One
 * sentence is DM3's own, at the end: a question about how the site works goes
 * to the mentors or to "Для новичков", the place a page of questions and
 * answers holds on other sites.
 *
 * The address may name the reason the reader came with
 * (?subtype=<TicketSubtype>, built by supportFor); the form opens on it. The
 * note the form keeps under a reason says what can be done without the
 * administration, and the page draws the one line of it the form cannot, the
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
    ><strong>ВНИМАНИЕ!</strong><br />Это сообщение отправится только
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
// The site's sign-in line stands among the lines of the note, flush with them:
// its own padding is for a line that stands alone under a block.
.login-prompt.ticket-login
  padding: 0
</style>
