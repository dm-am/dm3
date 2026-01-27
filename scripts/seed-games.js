const http = require('http');

const API_BASE = 'http://localhost:5051/v1';

// Users who will be masters
const masters = [
  { login: 'SolohinLex', password: 'Test123!' },
  { login: 'Rayzen', password: 'Test123!' },
  { login: 'Akkarin', password: 'Test123!' }
];

// Games to create
const games = [
  {
    master: 'SolohinLex',
    game: {
      title: 'Тени над Вестмаром',
      setting: 'Dark Fantasy',
      system: 'D&D 5e',
      info: '[b]Сеттинг:[/b] Тёмное фэнтези в стиле Dark Souls\n[b]Система:[/b] D&D 5e\n[b]Уровень персонажей:[/b] 3-10\n[b]Темп игры:[/b] 2-3 поста в неделю\n\n[b]Описание:[/b]\nКоролевство Вестмар охвачено тьмой. Древнее зло пробудилось в глубинах земли, и его слуги бродят по ночным дорогам. Немногие осмеливаются выходить за стены городов после заката.\n\nИщу 3-4 отважных игроков для долгой кампании в мрачном мире, где смерть подстерегает на каждом шагу, но награда стоит риска.',
      status: 'Active'
    },
    rooms: [
      { title: 'Таверна "Последний приют"', accessType: 'Public', orderNumber: 1 },
      { title: 'Подземелья старого замка', accessType: 'Public', orderNumber: 2 }
    ],
    posts: [
      { room: 'Таверна "Последний приют"', text: '[i]Дождь барабанит по крыше таверны. В камине потрескивают дрова, отбрасывая тёплый свет на усталые лица посетителей. За стойкой хозяин протирает кружки, изредка бросая настороженные взгляды на входную дверь.[/i]\n\nНочь выдалась беспокойной. С севера пришли слухи о странных тварях, бродящих по лесам.' },
      { room: 'Таверна "Последний приют"', text: '[i]Дверь таверны с грохотом распахивается. На пороге стоит промокший до нитки путник в потрёпанном плаще.[/i]\n\n— Эль! И побыстрее! — хрипло бросает он, направляясь к камину.' }
    ]
  },
  {
    master: 'Rayzen',
    game: {
      title: 'Звёздные Рубежи',
      setting: 'Sci-Fi',
      system: 'Savage Worlds',
      info: '[b]Сеттинг:[/b] Космическая опера в духе Mass Effect и Firefly\n[b]Система:[/b] Savage Worlds Adventure Edition\n[b]Темп:[/b] Свободный, 2-4 поста в неделю\n\n[b]Описание:[/b]\nЭкипаж небольшого транспортного корабля "Аврора" пытается выжить на границе освоенного космоса. Контрабанда, наёмная работа, случайные заказы — всё, что угодно, лишь бы держаться на плаву. Но сектор Эпсилон таит в себе опасности, о которых они даже не подозревают.\n\nИщу 3-5 игроков на роли членов экипажа. Приветствуется разнообразие характеров и специализаций.',
      status: 'Active'
    },
    rooms: [
      { title: 'Мостик "Авроры"', accessType: 'Public', orderNumber: 1 },
      { title: 'Каюта капитана', accessType: 'Private', orderNumber: 2 }
    ],
    posts: [
      { room: 'Мостик "Авроры"', text: '[i]"Аврора" мягко покачивается в невесомости. За обзорными экранами мерцают далёкие звёзды, а впереди — пустота сектора Эпсилон.[/i]\n\n[b]БОРТОВОЙ КОМПЬЮТЕР:[/b] Внимание. Обнаружен сигнал бедствия. Источник: грузовой корабль класса "Атлас". Расстояние: 4.7 световых минут.' },
      { room: 'Мостик "Авроры"', text: '— Ещё один? — капитан нахмурился, изучая данные на экране. — Это третий за эту неделю. Что происходит в этом секторе?\n\n[i]Он повернулся к штурману.[/i]\n\n— Проложи курс. Посмотрим, что там.' }
    ]
  },
  {
    master: 'Akkarin',
    game: {
      title: 'Хроники Кровавой Луны',
      setting: 'Urban Fantasy',
      system: 'World of Darkness',
      info: '[b]Сеттинг:[/b] Городское фэнтези/World of Darkness\n[b]Система:[/b] Vampire: The Masquerade 20th Anniversary Edition\n[b]Темп:[/b] Активный (4-5 постов в неделю)\n\n[b]Описание:[/b]\nСовременный мегаполис, где сверхъестественные существа скрываются среди людей. Вампиры, оборотни, маги — все борются за власть в ночном городе. Древние кланы плетут интриги, молодые Сородичи пытаются найти своё место в жёсткой иерархии Камарильи.\n\nИщу 3-4 игроков для политических интриг, ночных охот и борьбы за выживание в мире вечной ночи.',
      status: 'Active'
    },
    rooms: [
      { title: 'Клуб "Элизиум"', accessType: 'Public', orderNumber: 1 },
      { title: 'Тайная комната', accessType: 'Private', orderNumber: 2 }
    ],
    posts: [
      { room: 'Клуб "Элизиум"', text: '[i]Неоновые огни клуба пульсируют в такт басам. На танцполе толпа смертных, не подозревающих, что среди них охотятся хищники. В VIP-зоне, за затемнёнными стёклами, собралась совсем другая публика.[/i]\n\nПринц города объявил собрание. Всем Сородичам надлежит явиться.' },
      { room: 'Клуб "Элизиум"', text: '[i]Высокий мужчина в дорогом костюме поднялся со своего места. Его бледная кожа и хищная грация выдавали в нём одного из Проклятых.[/i]\n\n— Господа, — его голос был тихим, но каждый в комнате услышал каждое слово. — У нас проблема. Охотники вернулись в город.' }
    ]
  }
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
  authToken = null; // Clear previous token
  const result = await request('POST', '/account/login', { login, password });
  if (result.status !== 200) {
    throw new Error(`Login failed: ${JSON.stringify(result.data)}`);
  }
  console.log(`Logged in as ${result.data.resource.login}`);
  return result.data.resource;
}

async function createGame(gameData) {
  const result = await request('POST', '/games', gameData);
  if (result.status !== 201 && result.status !== 200) {
    console.error(`Failed to create game "${gameData.title}": ${JSON.stringify(result.data)}`);
    return null;
  }
  console.log(`Created game: ${gameData.title} (id: ${result.data.resource.id})`);
  return result.data.resource;
}

async function createRoom(gameId, roomData) {
  const result = await request('POST', `/games/${gameId}/rooms`, roomData);
  if (result.status !== 201 && result.status !== 200) {
    console.error(`Failed to create room "${roomData.title}": ${JSON.stringify(result.data)}`);
    return null;
  }
  console.log(`  Created room: ${roomData.title}`);
  return result.data.resource;
}

async function createPost(roomId, text) {
  const result = await request('POST', `/rooms/${roomId}/posts`, { text });
  if (result.status !== 201 && result.status !== 200) {
    console.error(`Failed to create post: ${JSON.stringify(result.data)}`);
    return null;
  }
  console.log(`    Created post in room ${roomId}`);
  return result.data.resource;
}

async function main() {
  try {
    for (const gameConfig of games) {
      // Login as master
      await login(gameConfig.master, 'Test123!');

      // Create game
      const game = await createGame(gameConfig.game);
      if (!game) continue;

      // Create rooms
      const roomMap = {};
      for (const roomData of gameConfig.rooms) {
        const room = await createRoom(game.id, roomData);
        if (room) {
          roomMap[roomData.title] = room.id;
        }
        await new Promise(r => setTimeout(r, 100));
      }

      // Create posts
      for (const postData of gameConfig.posts) {
        const roomId = roomMap[postData.room];
        if (roomId) {
          await createPost(roomId, postData.text);
          await new Promise(r => setTimeout(r, 100));
        }
      }

      console.log('');
    }

    console.log('Done! Created games, rooms and posts.');
  } catch (error) {
    console.error('Error:', error.message);
  }
}

main();
