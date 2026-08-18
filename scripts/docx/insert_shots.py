# -*- coding: utf-8 -*-
"""Ставит скриншоты в docx: страницы - под заголовок раздела, контентные блоки
- в подраздел "Мокапы" этого блока.

Запуск идемпотентен: снимок, уже стоящий под заголовком, ЗАМЕНЯЕТСЯ новым, а
не дублируется - старый абзац с рисунком удаляется, на его место встает новый.
Порядок в разделе страницы: заголовок, скриншот, строка "Адрес: ...", затем
скелет.

Контентный блок (4.2.2) устроен иначе: мокапы у него живут не под заголовком
раздела, а в конце, в подразделе "Мокапы", где у каждого кадра есть подпись
режима. Поэтому кадр блока встает В СВОЮ подпись: рисунок в подписанном абзаце
заменяется новым, а пометка "(old structure)" из подписи снимается - она
означает кадр со старого сайта и после пересъемки становится ложью. Раздел без
подраздела "Мокапы" получает его целиком: и заголовок, и подписи копируются с
соседнего раздела, чтобы форматирование совпадало до свойств абзаца.

Формат документа не трогается: правка - это абзац с рисунком после
существующего заголовка, все остальные части пакета (styles, numbering,
fonts, темы, колонтитулы) не переписываются. Абзац создается пустым, без
копирования свойств заголовка, поэтому форматирование заголовка (в том числе
жирность стиля) в него не протекает.
"""
import copy
import os
import re
import sys

from docx import Document
from docx.image.image import Image as DocxImage
from docx.shared import Cm
from docx.oxml.ns import qn
from docx.text.paragraph import Paragraph

DOCX = sys.argv[1] if len(sys.argv) > 1 else "D:/Projects/Web/dm3/docs/Документация_по_разработке_DM3.docx"
SHOTS = os.environ.get("SHOTS_DIR", os.path.join(os.path.dirname(__file__), "shots"))

# Кадр страницы - это окно браузера шириной 1280 пикселей, и в документе он
# занимает 16 см. Вырезанный элемент уже страницы, и ставить его во всю полосу
# значит увеличить его текст вдвое против соседних снимков: ширина считается от
# той же плотности, а не подгоняется под ширину полосы.
PAGE_WIDTH_CM = 16.0
SHOT_PX = 1280


def picture_width(png):
    px = DocxImage.from_file(png).px_width
    return Cm(PAGE_WIDTH_CM * min(1.0, px / SHOT_PX))


# Номер раздела -> файл скриншота (или список файлов, если раздел показывают
# несколько кадров). Разделы без страницы на сайте не входят.
MAPPING = {
    # Диалоговые окна (модалки авторизации живут в 4.2.4 после каталога правок)
    "4.2.4.1.": "91-modal-warning",
    "4.2.4.2.": "92-modal-ban",
    "4.2.4.3.": "93-bbcode-help",
    "4.2.4.4.": "48-modal-login",
    "4.2.4.5.": "49-modal-register",
    "4.2.4.6.": "50-modal-recovery",
    "4.2.4.7.": "94-confirm-delete-game",
    # BBCode-редактор: два режима одного поля
    "4.2.5.2.": ["95-editor-wysiwyg", "96-editor-bbcode"],
    # Авторизация: страницы
    "4.2.3.1.1.": "46-reset-password",
    "4.2.3.1.2.": "47-confirm-email",
    # Общие
    "4.2.3.2.1.": "01-home",
    "4.2.3.2.2.": "02-about",
    "4.2.3.2.3.": "59-rules",
    "4.2.3.2.4.": "12-support",
    "4.2.3.2.5.": "13-complaint",
    "4.2.3.2.6.": "58-my-tickets",
    "4.2.3.2.7.": "90-donate",
    "4.2.3.2.8.": "11-agreement",
    "4.2.3.2.9.": "10-privacy",
    "4.2.3.2.13.": "45-error-401",
    # Профиль
    "4.2.3.3.1.": "15-profile",
    "4.2.3.3.2.": "52-messenger",
    "4.2.3.3.3.": "55-notifications",
    "4.2.3.3.4.": "51-account",
    "4.2.3.3.5.": "21-received-reviews",
    "4.2.3.3.6.": "22-given-reviews",
    "4.2.3.3.7.": "27-profile-uploads",
    "4.2.3.3.9.": "56-subscriptions",
    "4.2.3.3.10.": "57-notepad",
    "4.2.3.3.11.": "23-received-endorsements",
    "4.2.3.3.12.": "24-given-endorsements",
    "4.2.3.3.13.": "25-received-game-reviews",
    "4.2.3.3.14.": "26-given-game-reviews",
    # Сообщество
    "4.2.3.4.1.": "06-community",
    "4.2.3.4.2.": "04-polls",
    "4.2.3.4.3.": "05-statistics",
    "4.2.3.4.4.": "31-games",
    "4.2.3.4.5.": "40-blogs",
    "4.2.3.4.6.": "08-global-chat",
    "4.2.3.4.7.": "07-pulse",
    # Игры
    "4.2.3.5.1.": "32-game",
    "4.2.3.5.2.": "33-game-room",
    "4.2.3.5.3.": "34-game-chat-room",
    "4.2.3.5.4.": "37-game-comments",
    "4.2.3.5.5.": "35-game-characters",
    "4.2.3.5.6.": "39-game-post-reviews",
    "4.2.3.5.7.": "63-game-notes",
    "4.2.3.5.8.": "67-char-create",
    "4.2.3.5.9.": "68-char-edit",
    "4.2.3.5.10.": "69-games-create",
    "4.2.3.5.11.": "64-game-settings",
    "4.2.3.5.12.": "38-game-reviews",
    "4.2.3.5.13.": "97-game-character",
    # Блоги
    "4.2.3.6.1.": "41-blog",
    "4.2.3.6.2.": "42-blog-feed",
    "4.2.3.6.3.": "43-blog-comments",
    "4.2.3.6.4.": "65-blog-notes",
    "4.2.3.6.5.": "60-blogs-create",
    "4.2.3.6.6.": "66-blog-settings",
    "4.2.3.6.7.": "61-pub-create",
    "4.2.3.6.8.": "62-pub-edit",
    # Форум
    "4.2.3.7.1.": "28-forum",
    "4.2.3.7.2.": "29-forum-general",
    "4.2.3.7.3.": "30-topic",
    # Модерация
    "4.2.3.8.1.": "70-mod-overview",
    "4.2.3.8.2.": "72-mod-games",
    "4.2.3.8.3.": "75-mod-warnings",
    "4.2.3.8.4.": "76-mod-rated-posts",
    "4.2.3.8.5.": "77-mod-new-users",
    "4.2.3.8.6.": "78-mod-violators",
    "4.2.3.8.7.": "79-mod-support",
    "4.2.3.8.8.": "80-mod-complaints",
    "4.2.3.8.9.": "82-mod-uploads",
    "4.2.3.8.10.": "71-mod-moderators",
    "4.2.3.8.11.": "73-mod-blogs",
    "4.2.3.8.12.": "74-mod-bans",
    "4.2.3.8.13.": "81-mod-ticket",
    "4.2.3.8.14.": "83-mod-username-changes",
    "4.2.3.8.15.": "84-mod-tags",
    "4.2.3.8.16.": "85-mod-awards",
    "4.2.3.8.17.": "86-mod-awards-series",
    "4.2.3.8.18.": "87-mod-award-types",
    "4.2.3.8.19.": "88-mod-achievements",
    "4.2.3.8.20.": "89-mod-fundraising",
}

# Контентные блоки: номер раздела -> список (подпись кадра, файл). Подпись
# ищется среди мокапов раздела; найденная получает новый рисунок, ненайденная
# заводится с нуля. Разделы блоков, которых нет на сайте, сюда не входят.
BLOCK_MAPPING = {
    # Каркас страницы: кадры снимает capture-chrome.cjs. Хэдер описан двумя
    # состояниями, и вошедший там обычный пользователь, а не администратор.
    "4.2.1.1.": [
        ("Гость", "chrome-header-guest"),
        ("Авторизованный пользователь", "chrome-header-user"),
    ],
    "4.2.1.2.": [("Футер", "chrome-footer")],
    # Панель игры описана тремя мокапами - это три разных зрителя одной игры.
    "4.2.1.3.": [
        ("Общая панель игры", "sb-game-panel"),
        ("Панель игры мастера", "sb-game-panel-master"),
        ("Панель игры модератора", "sb-game-panel-moderator"),
    ],
    # Панели сайдбаров: кадры снимает capture-sidebar.cjs, гостем - у вошедшего
    # эти панели другие, а описаны в документе гостевые.
    "4.2.1.9.": [("Панель набора и активных игр", "sb-active-games")],
    "4.2.1.10.": [("Панель активных блогов", "sb-active-blogs")],
    "4.2.1.12.": [("Панель серверов сайта", "sb-site-addresses")],
    "4.2.1.18.": [("Панель поддержки проекта", "sb-support")],
    # Блоки страницы профиля: кадры снимает capture-profile.cjs, и зритель у них
    # разный - личная заметка видна вошедшему только на чужом профиле.
    "4.2.2.2.": [("Режим просмотра", "114-profile-block")],
    "4.2.2.3.": [
        ("Режим просмотра", "115-profile-note"),
        ("Режим редактирования", "116-profile-note-edit"),
    ],
    "4.2.2.17.": [
        ("Режим просмотра", "119-topic"),
        ("Режим создания", "120-topic-create"),
    ],
    "4.2.2.18.": [("Режим просмотра", "121-comment")],
    "4.2.2.21.": [("Режим просмотра", "122-warning")],
    "4.2.2.20.": [
        ("Режим просмотра", "117-mod-note"),
        ("Режим редактирования", "118-mod-note-edit"),
    ],
    "4.2.2.12.": [
        ("Режим просмотра", "98-game-post"),
        ("Режим редактирования", "99-game-post-edit"),
        ("Режим создания", "100-game-post-create"),
    ],
    "4.2.2.13.": [
        ("Форма создания", "101-dice-form"),
        ("Режим просмотра", "102-dice-rolls"),
    ],
    "4.2.2.14.": [("Режим просмотра", "103-post-review")],
    "4.2.2.15.": [("Режим просмотра", "104-blog-details")],
    "4.2.2.16.": [("Режим просмотра", "105-publication")],
    "4.2.2.23.": [("Режим просмотра", "106-ticket")],
    "4.2.2.24.": [("Режим просмотра", "107-ticket-answer")],
    "4.2.2.25.": [("Режим просмотра", "108-game-review")],
    "4.2.2.26.": [("Режим просмотра", "109-endorsement")],
    "4.2.2.27.": [
        ("Плитка в профиле", "110-award-tile"),
        ("Попап по наведению", "111-award-popup"),
    ],
    "4.2.2.28.": [
        ("Плитка в профиле", "112-achievement-tile"),
        ("Попап по наведению", "113-achievement-popup"),
    ],
}

MOCKUPS_TITLE = "Мокапы"
OLD_TAIL = re.compile(r"\s*\(old structure\)")


def has_picture(el):
    return bool(el.findall(".//" + qn("a:blip")))


def heading_level(par):
    name = par.style.name or ""
    return int(name.split()[-1]) if name.startswith("Heading") else None


def section_paragraphs(paras, start):
    """Абзацы раздела: от его заголовка до следующего заголовка того же или
    более высокого уровня."""
    level = heading_level(paras[start]) or 4
    out = []
    for par in paras[start + 1 :]:
        lvl = heading_level(par)
        if lvl is not None and lvl <= level:
            break
        out.append(par)
    return out


def new_drawing(doc, png):
    """Рисунок как элемент w:drawing. Картинка добавляется штатным способом во
    временный абзац - иначе часть с изображением и связь на нее не заводятся -
    и переносится оттуда в нужный абзац."""
    tmp = doc.add_paragraph()
    run = tmp.add_run()
    run.add_picture(png, width=picture_width(png))
    drawing = run._r.find(qn("w:drawing"))
    run._r.remove(drawing)
    tmp._p.getparent().remove(tmp._p)
    return drawing


def set_paragraph_picture(par, drawing):
    """Меняет рисунок в подписанном абзаце, не трогая ни подпись, ни свойства
    прогонов: старый w:drawing вынимается, новый встает на его место."""
    for run in par.runs:
        old = run._r.find(qn("w:drawing"))
        if old is not None:
            run._r.replace(old, drawing)
            return True
    return False


def strip_old_tail(par):
    """Снимает пометку "(old structure)" с подписи переснятого кадра."""
    changed = False
    for node in par._p.iter(qn("w:t")):
        if node.text and "(old structure)" in node.text:
            node.text = OLD_TAIL.sub("", node.text)
            changed = True
    return changed


def clone_caption(template, caption, drawing):
    """Новая подпись с рисунком по образцу существующей: копия несет свойства
    абзаца и прогонов (включая парные w:b и w:bCs), меняются только текст и
    рисунок. Закладки из копии убираются - их идентификаторы уникальны."""
    el = copy.deepcopy(template._p)
    for tag in ("w:bookmarkStart", "w:bookmarkEnd"):
        for node in el.findall(qn(tag)):
            el.remove(node)
    par = Paragraph(el, template._parent)

    text_set = False
    for run in list(par.runs):
        if run._r.find(qn("w:drawing")) is not None:
            continue
        texts = run._r.findall(qn("w:t"))
        if texts and not text_set:
            texts[0].text = caption + ":"
            for extra in texts[1:]:
                run._r.remove(extra)
            text_set = True
        elif texts:
            run._r.getparent().remove(run._r)
    if not set_paragraph_picture(par, drawing):
        run = par.add_run()
        run._r.append(drawing)
    return el


def clone_heading(template, title):
    """Заголовок "Мокапы" по образцу такого же из соседнего раздела."""
    el = copy.deepcopy(template._p)
    for tag in ("w:bookmarkStart", "w:bookmarkEnd"):
        for node in el.findall(qn(tag)):
            el.remove(node)
    par = Paragraph(el, template._parent)
    runs = par.runs
    if runs:
        texts = runs[0]._r.findall(qn("w:t"))
        if texts:
            texts[0].text = title
        for run in runs[1:]:
            run._r.getparent().remove(run._r)
    return el


def place_blocks(doc, report):
    """Кадры контентных блоков: замена рисунка в своей подписи или новый
    подраздел "Мокапы" целиком."""
    paras = doc.paragraphs

    # Образцы формата берутся из готового подраздела мокапов соседнего
    # контентного блока: заголовок и подписанный абзац с рисунком.
    start = next(
        (i for i, p in enumerate(paras) if p.text.strip().startswith("4.2.2.")), 0
    )
    scope = paras[start:]
    heading_tpl = next(
        (p for p in scope if p.text.strip() == MOCKUPS_TITLE), None
    )
    caption_tpl = next(
        (p for p in scope if has_picture(p._p) and p.text.strip()), None
    )
    if heading_tpl is None or caption_tpl is None:
        report["failed"].append("в документе нет образца подраздела мокапов")
        return 0, 0

    replaced = added = 0
    for index, par in enumerate(paras):
        prefix = next(
            (k for k in BLOCK_MAPPING if par.text.strip().startswith(k)), None
        )
        if prefix is None or heading_level(par) is None:
            continue
        report["seen"].add(prefix)

        body = section_paragraphs(paras, index)
        mockups = [p for p in body if has_picture(p._p)]
        taken = set()
        # Новые подписи встают в конец раздела, после последнего непустого
        # абзаца: хвостовые пустые абзацы - разделитель перед следующим
        # разделом, и вставка после них разорвала бы этот отступ.
        anchor = par._p
        for p in body:
            if p.text.strip():
                anchor = p._p

        for caption, shot in BLOCK_MAPPING[prefix]:
            png = os.path.join(SHOTS, shot + ".png")
            if not os.path.exists(png):
                report["missing_shot"].append(prefix + " " + shot)
                continue

            target = next(
                (
                    p
                    for p in mockups
                    if id(p._p) not in taken
                    and p.text.strip().startswith(caption)
                ),
                None,
            )
            if target is not None:
                taken.add(id(target._p))
                set_paragraph_picture(target, new_drawing(doc, png))
                replaced += 1
                note = prefix + " " + caption + " <- " + shot
                if strip_old_tail(target):
                    note += " (снята пометка old structure)"
                report["blocks"].append(note)
                continue

            # Подписи нет: раздел получает подраздел мокапов целиком.
            if not any(p.text.strip() == MOCKUPS_TITLE for p in body):
                head = clone_heading(heading_tpl, MOCKUPS_TITLE)
                anchor.addnext(head)
                anchor = head
                body.append(Paragraph(head, par._parent))
            new_par = clone_caption(caption_tpl, caption, new_drawing(doc, png))
            anchor.addnext(new_par)
            anchor = new_par
            added += 1
            report["blocks"].append(prefix + " " + caption + " <- " + shot + " (новый)")

    for prefix in BLOCK_MAPPING:
        if prefix not in report["seen"]:
            report["missing_section"].append(prefix)
    return replaced, added


def main():
    doc = Document(DOCX)
    paras = doc.paragraphs
    before_paras = len(paras)
    before_tables = len(doc.tables)

    inserted, missing_shot, missing_section = [], [], []
    added, removed = 0, 0
    used = set()

    from docx.text.paragraph import Paragraph

    for p in paras:
        text = p.text.strip()
        for prefix, shot in MAPPING.items():
            if not text.startswith(prefix):
                continue
            used.add(prefix)
            shots = [shot] if isinstance(shot, str) else list(shot)
            pngs = [os.path.join(SHOTS, s + ".png") for s in shots]
            absent = [s for s, f in zip(shots, pngs) if not os.path.exists(f)]
            if absent:
                missing_shot.append(prefix + " " + ", ".join(absent))
                break

            # Снимки предыдущего прогона живут в следующих абзацах: абзац с
            # рисунком и без текста уходит - иначе пересъемка копилась бы
            # слоями под одним заголовком.
            while True:
                nxt = p._p.getnext()
                if nxt is None or nxt.tag != qn("w:p"):
                    break
                nxt_para = Paragraph(nxt, p._parent)
                if not nxt.findall(".//" + qn("a:blip")) or nxt_para.text.strip():
                    break
                nxt.getparent().remove(nxt)
                removed += 1

            # Новые абзацы сразу после заголовка: пустые, без свойств заголовка.
            anchor = p._p
            for png in pngs:
                new_p = anchor.makeelement(qn("w:p"), {})
                anchor.addnext(new_p)
                run = Paragraph(new_p, p._parent).add_run()
                run.add_picture(png, width=picture_width(png))
                anchor = new_p
                added += 1
            inserted.append(prefix + " <- " + ", ".join(shots))
            break

    for prefix in MAPPING:
        if prefix not in used:
            missing_section.append(prefix)

    blocks = {
        "blocks": [],
        "seen": set(),
        "missing_shot": missing_shot,
        "missing_section": missing_section,
        "failed": [],
    }
    replaced, block_added = place_blocks(doc, blocks)
    added += block_added

    doc.save(DOCX)

    after = Document(DOCX)
    print("разделов: %d, снимков вставлено: %d, прежних снято: %d" % (
        len(inserted), added, removed))
    print("блоков: кадров заменено %d, подписей добавлено %d" % (
        replaced, block_added))
    for line in blocks["blocks"]:
        print("   " + line)
    expected = before_paras + added - removed
    print("абзацев было %d, стало %d (ожидалось не меньше %d)" % (
        before_paras, len(after.paragraphs), expected))
    print("таблиц было %d, стало %d" % (before_tables, len(after.tables)))
    if missing_shot:
        print("НЕТ СКРИНШОТА: " + "; ".join(missing_shot))
    if missing_section:
        print("НЕТ РАЗДЕЛА В DOCX: " + "; ".join(missing_section))
    for line in blocks["failed"]:
        print("ОШИБКА: " + line)


if __name__ == "__main__":
    main()
