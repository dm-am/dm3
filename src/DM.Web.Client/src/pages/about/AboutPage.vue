<script setup lang="ts">
import { SvgIcon } from "@/shared/ui";

interface FaqItem {
  question: string;
  answer: string;
}

const faqItems: FaqItem[] = [
  {
    question: "Нужен ли опыт в настольных играх?",
    answer:
      'У многих игр есть тег "Для новичков" — там мастера всегда рады помочь освоиться новым пользователям и игрокам. Сообщество в целом дружелюбное, вопросы приветствуются. Если не уверены насчет конкретной игры — всегда лучше спросить мастера в обсуждении, нужно ли знать систему заранее.',
  },
  {
    question: "Какие игровые системы используются?",
    answer:
      'Разные: от классического D&D и других настольных систем до авторских правил и простых "словесок" без сложной механики. Каждый мастер выбирает сам и указывает систему в описании игры.',
  },
  {
    question: "Как часто нужно писать посты?",
    answer:
      "Зависит от игры — лучше уточнить у мастера напрямую. Обычно чем чаще, тем лучше для динамики, но это неторопливое хобби: и мастера, и игроки иногда берут паузы.",
  },
  {
    question: "Сколько длится одна игра?",
    answer:
      "От нескольких недель до нескольких лет — зависит от сюжета и активности игроков. Если решите покинуть игру — ничего страшного, просто дайте мастеру знать об этом заранее и обсудите, как лучше вывести персонажа.",
  },
];
</script>

<template>
  <page-title v-once>О проекте</page-title>

  <!-- MOTTO (static) -->
  <div class="motto" v-once>
    <SvgIcon name="snake" class="motto-snake" />
    <p class="motto-text">
      <span class="quote-mark">«</span>Мы в ответе за тех, кого сгенерили!<span class="quote-mark">»</span>
    </p>
  </div>

  <!-- MAIN CONTENT (static - faqItems is const) -->
  <div class="about-content" v-once>
    <p>
      DM.AM — одна из крупнейших русскоязычных площадок для текстовых ролевых игр. С 2007 года тысячи игроков
      создают здесь свои истории: фэнтези, sci-fi, horror, исторические драмы и многое другое.
    </p>

    <h3>Почему форумные игры?</h3>
    <p>
      В отличие от настольных сессий, здесь не нужно собираться в одно время и в одном месте. Пишите когда удобно —
      утром за кофе, в обеденный перерыв или поздно ночью. Игра идет непрерывно, а продуманные посты делают историю
      глубже и интереснее.
    </p>

    <h3>С чего начать?</h3>
    <ul class="cta-links">
      <li>
        Игры с открытым набором ждут игроков —
        <router-link to="/games?statuses=Active&recruitmentFilter=open&sortBy=activated"><strong>найдите игру для себя</strong></router-link>
      </li>
      <li>
        Станьте мастером —
        <router-link to="/games/create"><strong>создайте собственную</strong></router-link>
      </li>
    </ul>

    <block-title>Частые вопросы</block-title>
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - -
    </div>
    <div class="faq-list">
      <template v-for="(item, index) in faqItems" :key="item.question">
        <div class="faq-item">
          <div class="faq-question">{{ item.question }}</div>
          <div class="faq-answer">{{ item.answer }}</div>
        </div>
        <div v-if="index < faqItems.length - 1" class="separator">
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
          - - - - - - - - - - - - - -
        </div>
      </template>
    </div>
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - -
    </div>

    <p>
      Остались вопросы? Загляните в
      <router-link to="/forum/newbies"><strong>раздел для новичков на форуме</strong></router-link> — там точно помогут
      разобраться.
    </p>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.motto
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  margin-bottom: $big

  .motto-snake
    width: 330px
    max-width: 100%
    height: auto
    color: $text

  .motto-text
    margin: 0
    font-size: 1.2em
    font-style: italic
    color: $text

    .quote-mark
      color: $text-muted
      font-size: 1.4em

// ============================================
// MAIN CONTENT
// ============================================
.about-content
  display: flex
  flex-direction: column
  gap: $tiny
  color: $text
  line-height: 1.6

  p
    margin: 0 0 $small

  h3
    margin: $medium 0 $small
    font-size: $font-size
    font-weight: bold
    color: $text

  a
    color: $link
    &:hover
      color: $link-hover
      text-decoration: underline

// ============================================
// CTA SECTION
// ============================================
.cta-links
  margin: $small 0 $medium
  padding-left: $big

  li
    margin: $tiny 0
    color: $text

  a
    color: $link
    &:hover
      color: $link-hover

// ============================================
// FAQ LIST
// ============================================
.faq-list
  display: flex
  flex-direction: column
  gap: $tiny
  margin: 0 0 $small

.faq-question
  font-weight: bold
  color: $text

.faq-answer
  margin: $tiny 0 0 0
  color: $text

.separator
  margin: $tiny 0
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: 100%
  width: 0
  min-width: 100%
  user-select: none
</style>
