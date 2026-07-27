<script setup lang="ts">
import { DashSeparator } from "@/shared/ui/DashSeparator";

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
    <!-- Inline SVG (one-off illustration): fill="currentColor" follows theme text color -->
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 253.23595 66.720597"
      fill="currentColor"
      class="motto-snake"
      aria-hidden="true"
    >
      <g transform="translate(-13.164066,-12.893282)">
        <path
          d="m 21.9,79.490725 c -8.003037,-0.364154 -10.489137,-1.443335 -7.53334,-3.270119 2.625841,-1.622858 21.692322,-4.217432 31.003478,-4.218964 13.041384,-0.0021 26.326984,-2.085186 30.835525,-4.83468 3.851753,-2.348958 6.700659,-6.458551 13.635809,-19.669874 C 93.477761,40.570029 95.194246,38.64099 100.2,35.85585 108.47973,31.249115 118.78548,28.8 129.89066,28.8 c 11.25368,0 15.13725,-1.4737 23.3041,-8.843211 7.78365,-7.023712 14.20168,-8.604797 22.93198,-5.649298 12.92095,4.374173 21.14766,15.019381 31.05241,40.181198 1.53472,3.898779 3.69301,8.086057 4.7962,9.305062 3.27803,3.622187 10.81658,4.869182 29.17882,4.826639 19.18947,-0.04446 25.24584,1.334479 25.24584,5.74808 0,1.545713 -1.12632,2.919158 -2.96817,3.61943 -1.78969,0.680436 -47.648,1.054757 -115.50001,0.942776 -61.892505,-0.102146 -113.611829,0.07115 -114.931829,0.385097 -1.32,0.31395 -6.315,0.392678 -11.100001,0.174952 z m 137.7,-2.961189 c 63.13181,0.168902 102.07554,-0.16289 103.18997,-0.879156 1.46133,-0.939214 1.50583,-1.434583 0.2424,-2.698013 -1.1215,-1.121504 -6.90035,-1.592345 -20.98997,-1.710196 C 213.20717,71.000982 210.86621,69.960044 204.61585,54.600001 192.89262,25.790567 183.52401,15.6 168.76154,15.6 c -6.61116,0 -10.75078,1.891012 -15.76154,7.2 -5.95932,6.314011 -9.61209,7.575277 -24.6,8.494151 -15.46804,0.94831 -24.26406,3.600397 -31.363622,9.456438 -2.399993,1.979623 -4.672487,4.825587 -5.049987,6.324364 -1.327956,5.27234 -8.118691,16.392589 -12.197065,19.973449 -5.778137,5.073272 -10.428357,6.101094 -32.989325,7.291517 -10.89,0.574608 -21.15,1.634889 -22.8,2.356181 -2.231953,0.975689 1.761891,1.087516 15.6,0.436795 C 49.830001,76.651841 103.83,76.380329 159.6,76.529536 Z"
        />
        <circle cx="258.89764" cy="73.757095" r="0.70844597" />
      </g>
    </svg>
    <p class="motto-text">
      <span class="quote-mark">«</span>Мы в ответе за тех, кого сгенерили!<span
        class="quote-mark"
        >»</span
      >
    </p>
  </div>

  <!-- MAIN CONTENT (static - faqItems is const) -->
  <div class="about-content" v-once>
    <p>
      Dungeon Master — одна из крупнейших русскоязычных площадок для текстовых
      ролевых игр. С 2007 года тысячи игроков создают здесь свои истории:
      фэнтези, фантастика, хоррор, исторические драмы и многое другое.
    </p>

    <block-title>Почему форумные игры?</block-title>
    <p>
      В отличие от настольных сессий, здесь не нужно собираться в одно время и в
      одном месте. Пишите, когда удобно — утром за кофе, в обеденный перерыв или
      поздно ночью. Игра идет непрерывно, а продуманные посты делают историю
      глубже и интереснее.
    </p>

    <block-title>С чего начать?</block-title>
    <ul class="cta-links">
      <li>
        Игры с открытым набором ждут игроков —
        <router-link
          :to="{
            name: 'games',
            query: {
              status: 'Active',
              recruitmentFilter: 'open',
              sortBy: 'activated',
            },
          }"
          ><strong>найдите игру для себя</strong></router-link
        >
      </li>
      <li>
        Станьте мастером —
        <router-link to="/games/create"
          ><strong>создайте собственную</strong></router-link
        >
      </li>
    </ul>

    <block-title>Частые вопросы</block-title>
    <DashSeparator spacing="tiny" />
    <div class="faq-list">
      <template v-for="item in faqItems" :key="item.question">
        <div class="faq-item">
          <h3 class="faq-question">{{ item.question }}</h3>
          <div class="faq-answer">{{ item.answer }}</div>
        </div>
        <DashSeparator spacing="tiny" />
      </template>

      <!-- Testimonials link: natural home for #26, kept out of faqItems
           since it needs a router-link (plain data array only holds text) -->
      <div class="faq-item">
        <h3 class="faq-question">Что говорят о Dungeon Master игроки?</h3>
        <div class="faq-answer">
          Мы собираем отзывы участников на отдельной странице —
          <router-link to="/testimonials"
            ><strong>почитайте, что пишут игроки</strong></router-link
          >.
        </div>
      </div>
    </div>
    <DashSeparator spacing="tiny" />

    <p>
      Остались вопросы? Загляните в
      <router-link :to="{ name: 'forum', params: { alias: 'newbies' } }"
        ><strong>раздел для новичков на форуме</strong></router-link
      >
      — там точно помогут разобраться.
    </p>
  </div>
</template>

<style scoped lang="sass">
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

// ============================================
// FAQ LIST
// ============================================
.faq-list
  display: flex
  flex-direction: column
  gap: $tiny
  margin: 0 0 $small

.faq-question
  margin: 0
  font-size: inherit
  font-weight: bold
  color: $text

.faq-answer
  margin: $tiny 0 0 0
  color: $text
</style>
