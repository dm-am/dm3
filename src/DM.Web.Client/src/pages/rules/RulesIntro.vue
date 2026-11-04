<template>
  <section class="rules-intro" v-once>
    <p>
      Привет! Мы рады видеть вас на Dungeon Master, одном из крупнейших
      русскоязычных ролевых сайтов. Это большое и дружелюбное сообщество
      любителей текстовых ролевых игр, существующее с 2007-го года.
    </p>

    <p>
      Здесь люди находят игры, чтобы творить и веселиться вместе с
      единомышленниками, заводят новые знакомства и даже друзей. Если хотите
      узнать больше о площадке или не знаете, с чего начать, загляните на
      страницу
      <router-link to="/about" class="highlight-link">"О проекте"</router-link>.
    </p>

    <p>
      Наши правила созданы для того, чтобы всем было комфортно. Мы постарались
      написать их максимально коротко и понятно.
    </p>

    <h3>TL;DR</h3>
    <ol>
      <li>Не оскорбляйте других людей</li>
      <li>
        Мат и контент 18+ оборачивайте в тег <span class="tag">[nsfw]</span>
      </li>
      <li>Не обсуждайте политику нигде и никак</li>
      <li>Один человек — один аккаунт</li>
      <li>Не спамьте и не флудите</li>
    </ol>

    <p>
      Если вам что-то непонятно в правилах, обратитесь к кому-нибудь из
      Администрации, мы всегда поможем разобраться.
    </p>
  </section>
</template>

<style scoped lang="sass">
.rules-intro
  margin: $medium 0

  p
    margin: $medium 0
    line-height: 1.6

  // h3 to keep the outline without a skip, in the plain look rather than BlockTitle's.
  h3
    margin: $medium 0 $small
    font-weight: 700

  ol
    margin: $small 0
    padding-left: $big

    li
      margin: $minor 0

  // Color and hover come from the global `a` rule (Reset.sass); only the
  // bold weight is local.
  .highlight-link
    font-weight: bold

  // The tag in the font [code] has in a post.
  .tag
    font-family: $code-font
    background: $bg-element-accent
    padding: $tiny
    border-radius: $tiny
    font-size: 0.9em
</style>
