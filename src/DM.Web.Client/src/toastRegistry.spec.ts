/**
 * @vitest-environment node
 */

/**
 * Every plate the site raises is written down here, with the reason the page
 * could not say it at the place of the press.
 *
 * The owner's rule of 26 September 2026: a plate only where it is necessary
 * and the page where the action happens has nowhere to say it (UI_STANDARDS,
 * "Всплывающие сообщения"). Before it the site had 2 plates of success and
 * over a hundred of refusal: 83 calls of notifyFailure and the interceptor's
 * plate for every 403, 429 and 5xx a press met. A press that had a button, a
 * row or a form on the screen answered in the corner, far from where the
 * reader looked, and the line under the button stayed empty so as not to say
 * it twice. Those refusals now stand at the place (ActionResult,
 * refusalHere), and the plates that are left are the ones below.
 *
 * The buttons of the sidebars are the exception the owner made to it: a row
 * of the game or blog menu and a vote in a poll answer with a plate, not with
 * a line under the row (UI_STANDARDS, "Всплывающие сообщения").
 *
 * The rule alone does not hold. A plate is the cheapest thing to write when a
 * press needs an answer, and the next one goes in without anyone noticing
 * that the page had a place for it. So each one is declared, in a list short
 * enough to read in a glance, with a sentence that has to be true of that
 * press. Adding a plate means adding a line here and saying out loud why the
 * page is mute - which is exactly the moment to notice that it is not.
 *
 * Refusals are listed as well as confirmations, and the interceptor's own
 * plates with them: its words are built from the answer, so they are listed
 * by the call that raises them (UNWORDED) and not by a sentence.
 */
import { describe, expect, it } from "vitest";
import {
  disk,
  scanOf,
  sites,
  treeOf,
  type Tree,
} from "@/shared/lib/testing/toastScan";

/**
 * Every plate the site raises, by its words, and why the page cannot say it.
 *
 * The list only ever shrinks by itself. It grows when a press genuinely has
 * nowhere on the page to put the answer, and the sentence beside it is what a
 * reviewer checks against that page.
 */
const REGISTRY: Record<string, string> = {
  "Предупреждение выдано":
    "Предупреждение это запись в профиле нарушителя. Модератор в этот момент стоит в топике форума, в чате или в списке комментариев, и там от выдачи не меняется ничего. Блок профиля, где счетчик меняется на глазах, эту плашку гасит пропом hideToast.",
  "Не удалось изменить подписку":
    "Подписку на игру или блог меняют пунктом меню в левой колонке, а кнопки сайдбаров отвечают плашкой по решению владельца: строки отказа под пунктом меню нет.",
  "Не удалось изменить статус игры":
    "Статус игры меняют пунктом меню в левой колонке, и кнопки сайдбаров отвечают плашкой по решению владельца. Смена, о которой спрашивают окном, пишет отказ в самом окне, а эта плашка у смены без вопроса.",
  "Не удалось упорядочить закрепленные топики":
    "Порядок закрепленных топиков меняют перетаскиванием топика за значок в самом списке, и уходит он на сервер при отпускании: у этого действия нет кнопки, рядом с которой встала бы строка отказа. Строки возвращаются в порядок, сохраненный на сервере, а отказ говорит плашка.",
  "Не удалось проголосовать в опросе":
    "Голос отдают в опросе правой колонки, а кнопки сайдбаров отвечают плашкой по решению владельца: строки отказа под вариантом ответа нет.",
  "Не удалось изменить статус блога":
    "То же меню левой колонки, что у статуса игры: кнопки сайдбаров отвечают плашкой, смена с вопросом пишет отказ в окне.",
  "Не удалось выйти из аккаунта":
    'Выход нажимают в меню шапки, а меню закрывается до ответа сервера; в строке шапки, где остается одно слово "Выйти", места под фразу нет. В мобильной панели те же две функции, и они говорят тем же носителем.',
  "Не удалось выйти со всех устройств":
    'То же меню шапки, что у "Выйти": оно закрывается до ответа сервера, и у пункта нет места, где встала бы строка отказа.',
  "Часовой пояс не совпадает с устройством, [синхронизировать]?":
    "Предложение появляется само, при входе на сайт, на любой странице: у него нет своего места на экране и нет нажатия, рядом с которым оно могло бы стоять.",
  "Не удалось сменить часовой пояс":
    "Нажатие было в самой плашке предложения, а она уходит по нажатию. Другого места у этого нажатия нет, а строка на странице настроек говорила бы о нажатии, которого там не было.",
  "Сессия истекла, войдите снова":
    "Истекшая сессия не про нажатие, а про читателя: страница уходит на вход, и места, где стояла бы строка, больше нет. Любое окно или форма закрываются вместе с сессией.",
  "Сайт не отвечает по этому адресу":
    "Запрос не дошел до сервера: это про сеть читателя, а не про нажатие, и следующее нажатие рядом упрется в то же. Строка у места промолчит, плашка одна на всю страницу.",
  "${UNREACHABLE}, попробуйте [${other.name}]":
    "Та же плашка о недоступном адресе, дополненная предложением второго адреса, когда он проверен и отвечает. Переход на другой адрес касается всей страницы, а не одного места на ней.",
};

/**
 * A plate whose words are built when the refusal arrives, so the scan cannot
 * read them off the source: the call is named by its file and a piece of its
 * argument, and an entry whose call has left the code fails the check.
 */
interface Unworded {
  file: string;
  argument: string;
  why: string;
}

const UNWORDED: Unworded[] = [
  {
    file: "shared/api/client.ts",
    argument: "said",
    why: "Отказ 403 или 5xx со своей фразой сервера, пересказанный как есть, у запроса, который никто на странице не показал: фоновая загрузка, отметка прочитанного, запрос без своего места. Место, которое показывает отказ, снимает эту плашку (refusalHere, useActionResult) до того, как она встанет.",
  },
  {
    file: "shared/api/client.ts",
    argument: "describeRateLimit(",
    why: "Ожидание 429 у запроса, который никто на странице не показал. Место, которое показывает отказ, снимает эту плашку и пишет ожидание у себя с обратным отсчетом (useActionResult), а гостевые формы забирают его флагом ownsRateLimit.",
  },
  {
    file: "shared/lib/errors/generalPlate.ts",
    argument: "generalPlate(phrase",
    why: 'Общий отказ, "недостаточно прав" или "ошибка сервера", у запроса, который никто на странице не показал: "Запрос не выполнен: ошибка сервера", или с фразой одного из вызовов notifyFailure во главе. Место, которое показывает отказ, снимает эту плашку.',
  },
  {
    file: "shared/lib/composables/useDialogAction.ts",
    argument: "headed",
    why: "Отказ формы или вопроса на странице, который пришел, когда хозяин уже свернул форму, закрыл вопрос или увел строку со страницы: строки, где он встал бы, на странице больше нет, а запрос нес ownsRefusal, и перехватчик промолчал. Фраза действия во главе, за ней слова сервера.",
  },
  {
    file: "shared/lib/composables/useDialogAction.ts",
    argument: "waited",
    why: "Ожидание 429 для той же формы или того же вопроса, свернутых хозяином, пока запрос был в пути: обратному отсчету негде стоять, и плашка говорит то же, что сказал бы перехватчик.",
  },
  {
    file: "shared/lib/errors/notifyFailure.ts",
    argument: "describeFailure(error, fallback",
    why: "Сам помощник notifyFailure: его фраза пишется у вызова, а каждый вызов записан в этом реестре своей фразой: нажатия без места на странице и кнопки сайдбаров.",
  },
];

/**
 * Every message a tree raises, with the files that raise it, and what the scan
 * could not know: the calls it could not follow, the screens that leave a prop
 * out, and the entries of UNWORDED whose call is gone. The scan is the one
 * toastWidth.spec.ts measures plates with, shared/lib/testing/toastScan.ts,
 * and the reasoning behind each of its steps is written there.
 */
const raised = (tree: Tree, unworded: Unworded[] = []) => {
  const scan = scanOf(tree);
  const found = new Map<string, string[]>();
  const unread: string[] = [];
  const excused = new Set<Unworded>();
  for (const file of tree.files) {
    for (const site of sites(scan.read(file))) {
      const written = site.argument.replace(/\s+/g, " ").trim();
      const entries = unworded.filter(
        (entry) => entry.file === file && written.includes(entry.argument),
      );
      if (entries.length) {
        entries.forEach((entry) => excused.add(entry));
        continue;
      }
      const says = scan.valuesOf(site.argument, file);
      if (!says.length)
        unread.push(`${file}:${site.line}: ${site.kind}(${written})`);
      for (const message of says)
        found.set(message, [...(found.get(message) ?? []), file]);
    }
  }
  const stale = unworded
    .filter((entry) => !excused.has(entry))
    .map((entry) => `${entry.file}: ${entry.argument}`);
  return { found, unread, gaps: [...new Set(scan.gaps)], stale };
};

describe("a plate", () => {
  const { found, unread, gaps, stale } = raised(disk(), UNWORDED);

  it("is written down, with the reason the page cannot say it", () => {
    const undeclared = [...found]
      .filter(([message]) => !(message in REGISTRY))
      .map(([message, where]) => `${message} - ${where.join(", ")}`);

    expect(undeclared).toEqual([]);
  });

  it("leaves the register when it leaves the code", () => {
    const gone = Object.keys(REGISTRY).filter((message) => !found.has(message));

    expect([...gone, ...stale]).toEqual([]);
  });

  it("carries a reason, not a shrug", () => {
    const thin = [
      ...Object.entries(REGISTRY).map(([message, why]) => [message, why]),
      ...UNWORDED.map((entry) => [entry.argument, entry.why]),
    ]
      .filter(([, why]) => why.trim().length < 60)
      .map(([what]) => what);

    expect(thin).toEqual([]);
  });

  // A plate whose words cannot be found is a plate the register never hears
  // of. The fix is to hand the words over in a form the scan follows - a
  // sentence, a constant, a table, a computed, a prop - or, for a plate built
  // from the answer, an entry in UNWORDED.
  it("is followed to its words wherever they are written", () => {
    expect([...unread, ...gaps]).toEqual([]);
  });

  // The canary: a scan that stops matching turns the first check green by
  // finding nothing at all.
  it("is found however the file names the helper, of every kind", () => {
    const source = [
      `const { success: toastSuccess } = useToast();`,
      `const toast = useToast();`,
      `toastSuccess("первое");`,
      `toast.success("второе");`,
      `useToast().success(flag ? "третье" : "четвертое");`,
      `toast.error("отказ");`,
      `toast.warning("ожидание");`,
      `toast.info("предложение");`,
      `notifyFailure(error, "через помощника");`,
    ].join("\n");

    expect([...raised(treeOf({ "a.ts": source })).found.keys()].sort()).toEqual(
      [
        "второе",
        "первое",
        "третье",
        "четвертое",
        "отказ",
        "ожидание",
        "предложение",
        "через помощника",
      ].sort(),
    );
  });

  // The same canary one step further: the words stand somewhere else than the
  // call, in each of the ways the site hides them.
  it("is followed to wherever its words are written", () => {
    const tree = treeOf({
      "shared/copy/index.ts": [
        `export * from "./done";`,
        `export { NAMED } from "./named";`,
      ].join("\n"),
      "shared/copy/done.ts": `export const STARRED = "через звезду";`,
      "shared/copy/named.ts": `export const NAMED = "через имя";`,
      "pages/Own.vue": [
        `const toast = useToast();`,
        `const OWN = "своя константа";`,
        `toast.success(OWN);`,
      ].join("\n"),
      "pages/Imported.vue": [
        `import { STARRED, NAMED as SAID } from "@/shared/copy";`,
        `const toast = useToast();`,
        `toast.success(flag ? STARRED : SAID);`,
      ].join("\n"),
      "pages/Table.vue": [
        `const DONE = { kill: "из таблицы", leave: "из таблицы тоже" };`,
        `notifyFailure(error, DONE[kind]);`,
      ].join("\n"),
      "pages/Computed.vue": [
        `const toast = useToast();`,
        "const done = computed(() => `статус ${props.subject} сменен`);",
        `toast.error(done.value);`,
      ].join("\n"),
      "pages/SubjectPage.vue": `<Computed subject="блога" />`,
      "widgets/Board.vue": [
        `const toast = useToast();`,
        `toast.success(props.savedText);`,
      ].join("\n"),
      "pages/Written.vue": `<Board saved-text="из атрибута" />`,
      "pages/Bound.vue": [
        `<script setup>const TEXT = "из привязки";</script>`,
        `<board :saved-text="TEXT"></board>`,
      ].join("\n"),
    });

    const scanned = raised(tree);
    expect(scanned.unread).toEqual([]);
    expect(scanned.gaps).toEqual([]);
    expect([...scanned.found.keys()].sort()).toEqual(
      [
        "своя константа",
        "через звезду",
        "через имя",
        "из таблицы",
        "из таблицы тоже",
        "статус блога сменен",
        "из атрибута",
        "из привязки",
      ].sort(),
    );
  });

  // A sentence beside a name at the call stopped the scan at the sentence,
  // and the plate behind the name never reached the register.
  it("reads every branch of an argument that spells one of its own", () => {
    const scanned = raised(
      treeOf({
        "pages/Mixed.vue": [
          `const toast = useToast();`,
          `const saved = "за именем";`,
          `toast.success(flag ? "рядом" : saved);`,
        ].join("\n"),
        "widgets/Board.vue": [
          `const toast = useToast();`,
          `toast.error(flag ? "рядом" : props.savedText);`,
        ].join("\n"),
        "pages/Written.vue": `<Board saved-text="из атрибута" />`,
      }),
    );

    expect([...scanned.found.keys()].sort()).toEqual(
      ["за именем", "из атрибута", "рядом"].sort(),
    );
  });

  it("fails a plate it cannot follow to its words", () => {
    const scanned = raised(
      treeOf({
        "pages/Lost.vue": [
          `const toast = useToast();`,
          `toast.warning(fromSomewhere);`,
        ].join("\n"),
      }),
    );

    expect(scanned.unread).toEqual([
      "pages/Lost.vue:2: warning(fromSomewhere)",
    ]);
  });

  it("takes the word of UNWORDED only while its call is there", () => {
    const entry = (argument: string): Unworded => ({
      file: "a.ts",
      argument,
      why: "canary",
    });
    const kept = entry("build(");
    const gone = entry("gone(");
    const scanned = raised(
      treeOf({
        "a.ts": [
          `const toast = useToast();`,
          `toast.error(build(seconds));`,
        ].join("\n"),
      }),
      [kept, gone],
    );

    expect(scanned.unread).toEqual([]);
    expect(scanned.found.size).toBe(0);
    expect(scanned.stale).toEqual(["a.ts: gone("]);
  });
});
