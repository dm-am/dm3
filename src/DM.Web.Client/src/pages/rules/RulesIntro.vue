<script setup lang="ts">
/**
 * RulesIntro - статическое приветствие и TL;DR правил
 */
</script>

<template>
  <section class="rules-intro" v-once>
    <p>
      Привет! Мы рады видеть вас на DM — одном из крупнейших русскоязычных
      ролевых сайтов. Это большое и дружелюбное сообщество любителей текстовых
      ролевых игр, существующее с 2007-го года.
    </p>

    <p>
      Здесь люди находят игры, чтобы творить и веселиться вместе с
      единомышленниками, заводят новые знакомства и даже друзей. Хотите узнать
      больше о площадке или не знаете, с чего начать — загляните на страницу
      <router-link to="/about" class="highlight-link">"О проекте"</router-link>.
    </p>

    <p>
      Наши правила созданы для того, чтобы всем было комфортно. Мы постарались
      написать их максимально коротко и понятно.
    </p>

    <h4>TL;DR</h4>
    <ol>
      <li>Не оскорбляйте других людей</li>
      <li>
        Мат и контент 18+ оборачивайте в тег <span class="tag">[nsfw]</span>
      </li>
      <li>Не обсуждайте политику нигде и никак</li>
      <li>Один человек — один аккаунт</li>
      <li>Не спамьте и не флудите</li>
    </ol>

    <p class="outro">
      Если вам что-то непонятно в правилах — обратитесь к кому-нибудь из
      Администрации, мы всегда поможем разобраться.
    </p>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.rules-intro
  margin: $medium 0

  p
    margin: $medium 0
    line-height: 1.6

  h4
    margin: $medium 0 $small
    font-weight: 600

  ol
    margin: $small 0
    padding-left: $big

    li
      margin: $minor 0

  .tag
    font-family: monospace
    background: $bg-element-accent
    padding: 3px 2px
    border-radius: 2px
    font-size: 0.9em

  .highlight-link
    color: $link
    text-decoration: none
    font-weight: bold
    &:hover
      color: $link-hover
      text-decoration: underline

  .outro
    color: $text
</style>
