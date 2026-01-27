const http = require('http');

const API_BASE = 'http://localhost:5051/v1';

// Boards to create topics in (all except "Ошибки")
const boards = [
  'Общий',
  'Игровые системы',
  'Поиск мастера и игроков',
  'Конкурсы',
  'Котёл идей',
  'Под столом',
  'Неролевые игры',
  'Улучшение сайта',
  'Для новичков',
  'Новости проекта'
];

const topics = [
  { board: 'Общий', title: 'Как вы начали играть в ролёвки?', text: 'Расскажите свои истории! Кто-то начал с настолок, кто-то сразу в текстовые игры попал. Интересно узнать, как вы пришли к этому хобби.' },
  { board: 'Общий', title: 'Любимые сеттинги для игр', text: 'Какие миры вам нравятся больше всего? Фэнтези, киберпанк, постапокалипсис? Делитесь!' },
  { board: 'Игровые системы', title: 'D&D 5e vs Pathfinder 2e', text: 'Вечный спор! Какую систему вы предпочитаете и почему? Я лично перешёл на PF2e из-за более интересной боёвки.' },
  { board: 'Игровые системы', title: 'Системы для новичков', text: 'Какую систему посоветуете тем, кто только начинает? FATE? Dungeon World? Что-то ещё?' },
  { board: 'Поиск мастера и игроков', title: '[Набор] Кампания по Dark Heresy', text: 'Ищу 3-4 игроков для кампании в мире Warhammer 40k. Система Dark Heresy 2ed. Играем по выходным.' },
  { board: 'Конкурсы', title: 'Конкурс коротких рассказов', text: 'Предлагаю провести конкурс! Тема: "Первая встреча". Рассказы до 5000 знаков. Кто за?' },
  { board: 'Котёл идей', title: 'Идея для городской фэнтези', text: 'Думаю над сеттингом где магия вернулась в современный мир 10 лет назад. Какие последствия могли бы быть?' },
  { board: 'Под столом', title: 'Что читаете/смотрите?', text: 'Оффтоп! Какие книги/сериалы/аниме сейчас смотрите? Ищу рекомендации.' },
  { board: 'Неролевые игры', title: 'Настольные игры для компании', text: 'Посоветуйте настолки для компании 4-6 человек. Желательно не слишком сложные правила.' },
  { board: 'Улучшение сайта', title: 'Предложение: тёмная тема', text: 'Было бы круто иметь тёмную тему для сайта. Глаза устают от светлого фона вечером.' },
  { board: 'Для новичков', title: 'Как начать играть на этом сайте?', text: 'Привет! Я новичок, подскажите с чего начать? Как найти игру? Как создать персонажа?' },
  { board: 'Новости проекта', title: 'Обновление движка форума', text: 'Мы обновили движок форума! Теперь работает быстрее и стабильнее. Сообщайте о багах в разделе Ошибки.' }
];

const comments = [
  { topicTitle: 'Как вы начали играть в ролёвки?', text: 'Я начал с форумных игр лет 15 назад. Тогда ещё интернет был по диалапу, но мы умудрялись играть!' },
  { topicTitle: 'Как вы начали играть в ролёвки?', text: 'А я с настолок! D&D 3.5 была моя первая система. Потом уже перешёл в текстовые игры.' },
  { topicTitle: 'Любимые сеттинги для игр', text: 'Обожаю городскую фэнтези! World of Darkness, Dresden Files - всё это моё.' },
  { topicTitle: 'D&D 5e vs Pathfinder 2e', text: 'PF2e однозначно! Боёвка интереснее, классы разнообразнее. 5e слишком упрощена на мой вкус.' },
  { topicTitle: 'D&D 5e vs Pathfinder 2e', text: 'Не согласен, 5e проще для новичков и мастерить легче. Не всем нужна сложность PF2e.' },
  { topicTitle: '[Набор] Кампания по Dark Heresy', text: 'Ооо, DH! Давно хотел поиграть. Какой таймзон? Какие дни?' },
  { topicTitle: 'Что читаете/смотрите?', text: 'Сейчас читаю "Имя ветра" Ротфусса. Очень атмосферно!' },
  { topicTitle: 'Что читаете/смотрите?', text: 'Смотрю Baldur\'s Gate 3 летсплеи. Сам не играю, но смотреть интересно.' },
  { topicTitle: 'Предложение: тёмная тема', text: '+1! Очень нужна тёмная тема. Особенно для ночных сессий.' },
  { topicTitle: 'Как начать играть на этом сайте?', text: 'Привет! Заходи в раздел "Поиск мастера и игроков" и ищи объявления с пометкой [Набор]. Удачи!' }
];

let authToken = null;

function request(method, path, body = null) {
  return new Promise((resolve, reject) => {
    const url = new URL(API_BASE + path);
    const options = {
      hostname: url.hostname,
      port: url.port,
      path: url.pathname + url.search,
      method: method,
      headers: {
        'Content-Type': 'application/json'
      }
    };

    if (authToken) {
      options.headers['X-Dm-Auth-Token'] = authToken;
    }

    const req = http.request(options, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        // Get auth token from response headers
        if (res.headers['x-dm-auth-token']) {
          authToken = res.headers['x-dm-auth-token'];
        }
        try {
          resolve({ status: res.statusCode, data: data ? JSON.parse(data) : null });
        } catch (e) {
          resolve({ status: res.statusCode, data: data });
        }
      });
    });

    req.on('error', reject);

    if (body) {
      req.write(JSON.stringify(body));
    }
    req.end();
  });
}

async function login(login, password) {
  console.log(`Logging in as ${login}...`);
  const result = await request('POST', '/account/login', { login, password });
  if (result.status !== 200) {
    throw new Error(`Login failed: ${JSON.stringify(result.data)}`);
  }
  console.log(`Logged in as ${result.data.resource.login}`);
  return result.data.resource;
}

async function createTopic(boardId, title, text) {
  const encodedBoard = encodeURIComponent(boardId);
  const result = await request('POST', `/boards/${encodedBoard}/topics`, { title, text });
  if (result.status !== 201 && result.status !== 200) {
    console.error(`Failed to create topic "${title}": ${JSON.stringify(result.data)}`);
    return null;
  }
  console.log(`Created topic: ${title}`);
  return result.data.resource;
}

async function createComment(topicId, text) {
  const result = await request('POST', `/topics/${topicId}/comments`, { text });
  if (result.status !== 201 && result.status !== 200) {
    console.error(`Failed to create comment: ${JSON.stringify(result.data)}`);
    return null;
  }
  console.log(`Created comment in topic ${topicId}`);
  return result.data.resource;
}

async function main() {
  try {
    // Login as TestUser
    await login('TestUser', 'Test123!');

    // Create topics
    const createdTopics = {};
    for (const topic of topics) {
      const created = await createTopic(topic.board, topic.title, topic.text);
      if (created) {
        createdTopics[topic.title] = created.id;
      }
      // Small delay to avoid overwhelming the server
      await new Promise(r => setTimeout(r, 100));
    }

    // Create comments
    for (const comment of comments) {
      const topicId = createdTopics[comment.topicTitle];
      if (topicId) {
        await createComment(topicId, comment.text);
        await new Promise(r => setTimeout(r, 100));
      } else {
        console.warn(`Topic not found for comment: ${comment.topicTitle}`);
      }
    }

    console.log('\nDone! Created topics and comments.');
  } catch (error) {
    console.error('Error:', error.message);
  }
}

main();
