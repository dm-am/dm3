/**
 * What the generated markup is made of, for both sides.
 *
 * The grammar table (bbcodeGrammar.cases.ts) holds the spellings somebody
 * thought of. The property checks hold the rest: texts assembled at random from
 * the pieces below, on the server by the generator in test/DM.Testing/Markup and
 * here by the arbitraries of markupFuzz.ts. Both read this one list, so a piece
 * added here is a piece both sides are tried on. How the checks work and how a
 * failure is reproduced is written in docs/guides/TESTING.md, section
 * "Генеративные проверки разметки".
 *
 * How the server reads it. The object below is strict JSON between the two
 * marker comments: the server takes the text between them and parses it with
 * System.Text.Json, the way BbGrammarTable reads the grammar table. Keys stay
 * quoted and there is no trailing comma, which is why the declaration carries
 * prettier-ignore. A character the eye does not tell from another is written as
 * a \u escape.
 *
 * Fields:
 * - pairs: an opening and a closing tag, written around generated content, and
 *   now and then left without the closing one. "{names}" in an opening stands
 *   for one of names, and "{high-surrogate}" and "{low-surrogate}" for a
 *   surrogate with no partner, which System.Text.Json refuses to read as an
 *   escape.
 * - pieces: everything else, one piece at a time: tags alone, tags broken in
 *   every way the grammar names, brackets, quotes, line breaks, the control
 *   characters and the characters that reorder text, which both sides take out
 *   before reading, spaces of other kinds, addresses good and refused.
 * - names: what an addressee list of [private] is made of. "Вася" and "Петя" are
 *   the two characters of the room the checks read a post in.
 * - leaves: the tags that hold one address or one login, and addresses: what
 *   is put between them, whole, now and then. Both sides read an address by
 *   the parser of their own language (Uri in .NET, URL in the browser), and
 *   what one takes and the other refuses is a link on one side and text on
 *   the other.
 * - repeats: a piece and how many times it is written in a row: the shapes
 *   whose cost grows with their length, and a nesting deeper than the
 *   renderer takes.
 * - valid: markup the grammar accepts whole, for the checks that hold what is
 *   right: the editor's markup check stays silent on it and the save path
 *   takes it. Its pairs close always; lists hold items; [mod] goes only where
 *   the surface has it and [private] only into a game post, one level deep;
 *   [noparse] holds pieces, whatever they are, less its own closing tag.
 *
 * Words are not listed: both generators write a word as the letter Q, four
 * digits and the letter Q again, each one different, so a check can tell which
 * word of the input reached which reader.
 */

export interface MarkupAlphabet {
  pairs: Array<[string, string]>;
  pieces: string[];
  names: string[];
  leaves: Array<[string, string]>;
  addresses: string[];
  repeats: Array<[string, number]>;
  valid: {
    pairs: Array<[string, string]>;
    lists: Array<[string, string]>;
    items: Array<[string, string]>;
    mod: Array<[string, string]>;
    private: string[];
    privateClosings: string[];
    leaves: string[];
    text: string[];
  };
}

// prettier-ignore
export const MARKUP_ALPHABET: MarkupAlphabet = /* alphabet:begin */ {
"pairs": [
["[b]","[/b]"],["[B]","[/B]"],["[b]","[/B]"],["[i]","[/i]"],["[I]","[/i]"],["[u]","[/u]"],["[strike]","[/strike]"],["[Strike]","[/STRIKE]"],
["[code]","[/code]"],["[CODE]","[/Code]"],["[noparse]","[/noparse]"],["[NOPARSE]","[/NoParse]"],["[noparse]","[/NOPARSE]"],
["[spoiler]","[/spoiler]"],["[spoiler text=\"Подпись\"]","[/spoiler]"],["[Spoiler TEXT=\"a]b\"]","[/SPOILER]"],["[spoiler text=\"\"]","[/spoiler]"],["[spoiler text=\"Показать содержимое\"]","[/spoiler]"],
["[nsfw]","[/nsfw]"],["[quote]","[/quote]"],["[QUOTE]","[/quote]"],["[mod]","[/mod]"],["[Mod]","[/MOD]"],
["[ul]","[/ul]"],["[ol]","[/ol]"],["[li]","[/li]"],["[LI]","[/li]"],
["[link]","[/link]"],["[link text=\"Т\"]","[/link]"],["[LINK Text=\"он сказал \"да\" и ушел\"]","[/link]"],
["[img]","[/img]"],["[img width=\"30\"]","[/img]"],["[IMG Height=\"20\" width=\"30\"]","[/img]"],["[mention]","[/mention]"],
["[private]","[/private]"],["[private=\"{names}\"]","[/private]"],["[PRIVATE=\"{names}\"]","[/Private]"],["[Private]","[/PRIVATE]"],["[private=\"{names}\"]","[/pRiVaTe]"],
["[private={names}]","[/private]"],["[private to=\"{names}\"]","[/private]"],["[private =\"{names}\"]","[/private]"],["[private=\"{names}]","[/private]"],
["[private\n=\"{names}\"]","[/private]"],["[private\u00a0]","[/private]"],["[private\u3000=\"{names}\"]","[/private]"],["[private=\"Ва[ся\"]","[/private]"],
["[b=1]","[/b]"],["[quote=\"Имя\"]","[/quote]"],["[code lang=\"js\"]","[/code]"],["[code=\"x\"]","[/code]"],["[noparse=x]","[/noparse]"],["[noparse text=\"x\"]","[/noparse]"],
["[img alt=\"x\"]","[/img]"],["[spoiler text=Подпись]","[/spoiler]"],["[spoiler text=\"a[b\"]","[/spoiler]"],["[spoiler text=\"раз\nдва\"]","[/spoiler]"],
["[img width=\"300\" width=\"400\"]","[/img]"],["[img width=\"0\"]","[/img]"],["[img width=\"300\" ]","[/img]"],["[spoiler text = \"x\"]","[/spoiler]"],
["[b\u00a0]","[/b]"],["[noparse\u2028]","[/noparse]"],["[quote\u3000]","[/quote]"],
["[s]","[/s]"],["[cut]","[/cut]"]
],
"pieces": [
"[b]","[/b]","[B]","[/B]","[i]","[/i]","[u]","[/u]","[strike]","[/strike]","[code]","[/code]","[CODE]","[/CODE]",
"[noparse]","[/noparse]","[NoParse]","[/NOPARSE]","[noparse","[/noparse","[spoiler]","[/spoiler]","[spoiler text=\"","[nsfw]","[/nsfw]",
"[quote]","[/quote]","[mod]","[/mod]","[ul]","[/ul]","[ol]","[/ol]","[li]","[/li]","[tab]","[TAB]","[tab=1]","[tab ]",
"[img]","[/img]","[img width=\"","[link]","[/link]","[mention]","[/mention]",
"[private]","[/private]","[PRIVATE]","[/Private]","[private=\"Вася\"]","[private=\"Петя\"]","[private=Вася]","[private","[private=","[private ",
"[/private=\"x\"]","[/private ]","[/private","[/pri\u0001vate]","[pri\u202evate]","[pri\u0085vate=\"Вася\"]","[nopa\u0001rse]","[/nopa\u202arse]","[co\u0001de]",
"[privateer]","[private_x]","[priv\u0131te]","[lin\u212a]","[/b=\"x\"]","[/i=x]","[b x=\"1\"]","[quote=Имя]","[img=300]","[link=https://example.com/]","[mention=\"Коля\"]",
"[","]","[/","[]","[/]","=","\"","\"]","'","<",">","&","&amp;","&lt;b&gt;","<script>",
" ","  ","\t","\n","\n\n","\r\n","\r","--","---","----","___","-",
"\u0001","\u0007","\u001f","\u007f","\u0085","\u009f","\u202a","\u202e","\u2066","\u2069","\u200e","\u200f","\u200b",
"\u00a0","\u1680","\u2000","\u2028","\u2029","\u202f","\u205f","\u3000","\ufeff","\u000b","\u000c",
"{high-surrogate}","{low-surrogate}","\ud834\udd1e",
"https://example.com/a.png","https://example.com/","HTTPS://EXAMPLE.COM/","\ufeffhttps://example.com/"," https://example.com/ ","https://exa mple.com/",
"http://127.0.0.1/","http://localhost/","https://[::1]/","http://192.168.0.1/a.png","javascript:alert(1)","mailto:a@example.com",
"Коля"," Коля ","Ко\nля","раз","два","Вася","Петя"
],
"names": ["Вася","Петя","вася"," Вася ","Вася, Петя","Петя,Вася","Вася \"Кот\""," , ",""],
"leaves": [["[img]","[/img]"],["[link]","[/link]"],["[link text=\"Т\"]","[/link]"],["[IMG Width=\"30\"]","[/IMG]"],["[mention]","[/mention]"]],
"addresses": [
"https://example.com/a.png","http://example.com/","HTTPS://EXAMPLE.COM/A.PNG","https://example.com:8080/a.png","https://example.com:99999/","https://example.com:0/",
"http://127.0.0.1/","http://127.1/","http://2130706433/","http://0x7f.0.0.1/","http://0177.0.0.1/","http://0/","http://0.0.0.0/","http://10.1.2.3/","http://172.16.0.1/","http://172.32.0.1/","http://192.168.1.1/","http://169.254.169.254/","http://100.64.0.1/","http://100.128.0.1/",
"http://localhost/","http://LOCALHOST/","https://localhost./","https://x.localhost/","https://localhostx.com/",
"https://[::1]/","http://[::]/","http://[::ffff:127.0.0.1]/","http://[::ffff:7f00:1]/","http://[fe80::1]/","http://[fc00::1]/","http://[fec0::1]/","http://[2001:db8::1]/","http://[::1/",
"https://user@example.com/","http://user:pass@127.0.0.1/","https://example.com\\@127.0.0.1/","https://example.com/\\..\\a","https:example.com","https:/example.com/","https:///example.com/","https://","http:// example.com/",
"https://ex%61mple.com/","https://example.com/%00","https://example.com/a b","https://example.com/\u00e9","https://\u043f\u0440\u0438\u043c\u0435\u0440.\u0440\u0444/","https://xn--e1afmkfd.xn--p1ai/","https://exa\u200bmple.com/","https://example.com/\ufeff","\ufeffhttps://example.com/","https://example.com/\u00a0",
"http://127.0.0.1./","http://example.com./","https://localhost../","https://example.com?x","https://example.com#x","https://example.com","https://a_b.com/","https://-a.com/","https://a..b.com/","https://example.com:/","http://[::1]:80/","http://127.0.0.1:80@example.com/","http://example.com@127.0.0.1/","HtTp://example.com/","https://1.2.3/","https://0x0a.1/","https://example.com:65535/","https://example.com:65536/","http://[::ffff:10.0.0.1]/","http://[0:0:0:0:0:ffff:7f00:1]/","http://[::127.0.0.1]/","https://a%40b@example.com/","https://example.com/a?b=c#d","https://EXAMPLE.com./a.png","http://0x7f000001/","http://017700000001/","http://127.000.000.001/","http://1.1.1.1/","https://example.com/\u2028",
"javascript:alert(1)","JAVASCRIPT:alert(1)","data:text/html,x","//example.com/","ftp://example.com/","mailto:a@example.com",
"Коля"," Коля ","Ко ля","Ко[ля","Ко\u2028ля","\ufeffКоля","\u00a0Коля\u00a0",""
],
"repeats": [["[b]",40],["[/b]",40],["[quote]",520],["[private]",30],["[noparse]",30],["[/noparse]",30],["[",300],["]",300],[" ",300],["\"",80],["[img",40],[" a=\"1\"",60],["[spoiler text=\"",40],["[/private]",30],["--",60],["\n",60]],
"valid": {
"pairs": [["[b]","[/b]"],["[B]","[/b]"],["[i]","[/i]"],["[u]","[/u]"],["[strike]","[/strike]"],["[code]","[/code]"],["[Code]","[/CODE]"],["[spoiler]","[/spoiler]"],["[spoiler text=\"Подпись\"]","[/spoiler]"],["[spoiler text=\"он сказал \"да\"\"]","[/spoiler]"],["[nsfw]","[/nsfw]"],["[quote]","[/quote]"],["[Quote]","[/QUOTE]"]],
"lists": [["[ul]","[/ul]"],["[ol]","[/ol]"],["[UL]","[/ul]"]],
"items": [["[li]","[/li]"],["[LI]","[/Li]"]],
"mod": [["[mod]","[/mod]"],["[MOD]","[/mod]"]],
"private": ["[private]","[private=\"{names}\"]","[PRIVATE=\"{names}\"]","[Private]"],
"privateClosings": ["[/private]","[/PRIVATE]"],
"leaves": ["[img]https://example.com/a.png[/img]","[img width=\"300\"]https://example.com/a.png[/img]","[IMG Height=\"20\"]https://example.com/a.png[/IMG]","[img height=\"200\" width=\"300\"]https://example.com/a.png[/img]","[link]https://example.com/[/link]","[link text=\"Главная\"]https://example.com/[/link]","[link text=\"он сказал \"да\" и ушел\"]https://example.com/[/link]","[mention]Коля[/mention]","[mention] Коля [/mention]","[tab]","[TAB]"],
"text": ["раз","два"," ","  ","\n","\r\n","--","---","___","[1]","[privateer]","a < b & c","[cut]","[s]текст[/s]","\"","=","\u00a0","\u200e"]
}
} /* alphabet:end */;
