# -*- coding: utf-8 -*-
"""Ставит скриншоты страниц в docx сразу после заголовка раздела страницы.

Запуск идемпотентен: снимок, уже стоящий под заголовком, ЗАМЕНЯЕТСЯ новым, а
не дублируется - старый абзац с рисунком удаляется, на его место встает новый.
Порядок в разделе: заголовок, скриншот, строка "Адрес: ...", затем скелет.

Формат документа не трогается: правка - это абзац с рисунком после
существующего заголовка, все остальные части пакета (styles, numbering,
fonts, темы, колонтитулы) не переписываются. Абзац создается пустым, без
копирования свойств заголовка, поэтому форматирование заголовка (в том числе
жирность стиля) в него не протекает.
"""
import copy
import json
import os
import sys

from docx import Document
from docx.shared import Cm
from docx.oxml.ns import qn

DOCX = sys.argv[1] if len(sys.argv) > 1 else "D:/Projects/Web/dm3/docs/Документация_по_разработке_DM3.docx"
SHOTS = os.environ.get("SHOTS_DIR", os.path.join(os.path.dirname(__file__), "shots"))

# Номер раздела -> файл скриншота. Разделы без страницы на сайте не входят.
MAPPING = {
    # Диалоговые окна (модалки авторизации живут в 4.2.4 после каталога правок)
    "4.2.4.4.": "48-modal-login",
    "4.2.4.5.": "49-modal-register",
    "4.2.4.6.": "50-modal-recovery",
    # Авторизация: страницы
    "4.2.3.1.1.": "46-reset-password",
    "4.2.3.1.2.": "47-confirm-email",
    "4.2.3.1.3.": "45-error-401",
    # Общие
    "4.2.3.2.1.": "01-home",
    "4.2.3.2.2.": "02-about",
    "4.2.3.2.3.": "59-rules",
    "4.2.3.2.4.": "12-support",
    "4.2.3.2.5.": "13-complaint",
    "4.2.3.2.6.": "58-my-tickets",
    "4.2.3.2.8.": "11-agreement",
    "4.2.3.2.9.": "10-privacy",
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
    "4.2.3.5.13.": "38-game-reviews",
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


def main():
    doc = Document(DOCX)
    paras = doc.paragraphs
    before_paras = len(paras)
    before_tables = len(doc.tables)

    inserted, replaced, missing_shot, missing_section = [], [], [], []
    used = set()

    for p in paras:
        text = p.text.strip()
        for prefix, shot in MAPPING.items():
            if not text.startswith(prefix):
                continue
            used.add(prefix)
            png = os.path.join(SHOTS, shot + ".png")
            if not os.path.exists(png):
                missing_shot.append(prefix + " " + shot)
                break
            from docx.text.paragraph import Paragraph

            # Снимок предыдущего прогона живет в следующем абзаце: если там
            # рисунок и ничего кроме, он уходит - иначе пересъемка копилась бы
            # слоями под одним заголовком.
            nxt = p._p.getnext()
            if nxt is not None and nxt.tag == qn("w:p"):
                nxt_para = Paragraph(nxt, p._parent)
                has_picture = nxt.findall(".//" + qn("a:blip"))
                if has_picture and not nxt_para.text.strip():
                    nxt.getparent().remove(nxt)
                    replaced.append(prefix)

            # Новый абзац сразу после заголовка: пустой, без свойств заголовка.
            new_p = p._p.makeelement(qn("w:p"), {})
            p._p.addnext(new_p)

            np = Paragraph(new_p, p._parent)
            run = np.add_run()
            run.add_picture(png, width=Cm(16))
            inserted.append(prefix + " <- " + shot)
            break

    for prefix in MAPPING:
        if prefix not in used:
            missing_section.append(prefix)

    doc.save(DOCX)

    after = Document(DOCX)
    print("вставлено: %d (из них заменили прежний снимок: %d)" % (len(inserted), len(replaced)))
    expected = before_paras + len(inserted) - len(replaced)
    print("абзацев было %d, стало %d (ожидалось %d)" % (
        before_paras, len(after.paragraphs), expected))
    print("таблиц было %d, стало %d" % (before_tables, len(after.tables)))
    if missing_shot:
        print("НЕТ СКРИНШОТА: " + "; ".join(missing_shot))
    if missing_section:
        print("НЕТ РАЗДЕЛА В DOCX: " + "; ".join(missing_section))


if __name__ == "__main__":
    main()
