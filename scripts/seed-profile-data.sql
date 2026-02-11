-- Comprehensive seed data for TestUser profile
DO $$
DECLARE
  -- User IDs
  testuser_id UUID := '7c61ce37-2381-46e3-9b69-408fea58d96d';
  moderator_id UUID := '6e59e196-0778-4248-8381-43b4ccaadfe6';
  player_one_id UUID := '737dd20b-27b2-4cdd-9409-4202dfbf0058';
  player_two_id UUID;

  -- Board ID
  general_board_id UUID := 'c0000000-0000-0000-0000-000000000001';

  -- New entity IDs
  game1_id UUID := '11111111-1111-1111-1111-111111111111';
  game2_id UUID := '22222222-2222-2222-2222-222222222222';
  room1_id UUID := 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  room2_id UUID := 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
  room3_id UUID := 'cccccccc-cccc-cccc-cccc-cccccccccccc';
  char1_id UUID := 'dddddddd-dddd-dddd-dddd-dddddddddddd';
  char2_id UUID := 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee';
  post1_id UUID := 'ffffffff-ffff-ffff-ffff-ffffffffffff';
  post2_id UUID := '00000000-0000-0000-0000-000000000001';
  post3_id UUID := '00000000-0000-0000-0000-000000000002';
  topic1_id UUID := '00000000-0000-0000-0000-000000000010';
  comment1_id UUID := '00000000-0000-0000-0000-000000000011';
  warning1_id UUID := '00000000-0000-0000-0000-000000000020';
  ban1_id UUID := '00000000-0000-0000-0000-000000000021';
  blog1_id UUID := '00000000-0000-0000-0000-000000000030';
  login_hist1_id UUID := '00000000-0000-0000-0000-000000000040';
  login_hist2_id UUID := '00000000-0000-0000-0000-000000000041';
  note1_id UUID := '00000000-0000-0000-0000-000000000050';
  review1_id UUID := '00000000-0000-0000-0000-000000000060';
  review2_id UUID := '00000000-0000-0000-0000-000000000061';
  review3_id UUID := '00000000-0000-0000-0000-000000000062';

BEGIN
  -- Get Player-Two ID
  SELECT "UserId" INTO player_two_id FROM "Users" WHERE "Login" = 'Player-Two';

  -- ============================================
  -- 1. UPDATE USER PROFILE
  -- ============================================
  UPDATE "Users" SET
    "Name" = 'Иван Петров',
    "Status" = 'Любитель настольных и текстовых ролевых игр с 2005 года',
    "Location" = 'Санкт-Петербург, Россия',
    "Gender" = 1,
    "BirthdayDate" = '1990-03-15',
    "Skype" = 'testuser.rpg',
    "TelegramId" = '@testuser_rpg',
    "DiscordId" = 'TestUser#1234',
    "QuantityRating" = 250,
    "QualityRating" = 150,
    "Info" = '[b]Привет![/b]

Меня зовут Иван, я увлекаюсь ролевыми играми уже более 15 лет.

[h]Мои любимые сеттинги:[/h]
[list]
[*] Dark Fantasy
[*] Sci-Fi (особенно киберпанк)
[*] Историческое фэнтези
[/list]

[h]Как мастер:[/h]
Веду игры в стиле sandbox с упором на отыгрыш персонажей и политические интриги.

[h]Как игрок:[/h]
Предпочитаю играть сложных персонажей с неоднозначной моралью.

[quote]Настоящая ролевая игра — это не про кубики, а про истории.[/quote]

Всегда рад новым знакомствам!'
  WHERE "UserId" = testuser_id;
  RAISE NOTICE '1. User profile updated';

  -- ============================================
  -- 2. CREATE GAMES
  -- ============================================
  -- Game 1: TestUser as Master
  INSERT INTO "Games" (
    "GameId", "CreatedUtc", "ReleaseDate", "Status", "PremoderationStatus",
    "IsFinished", "IsFrozen", "IsRecruitmentOpen", "MasterId",
    "Title", "SystemName", "NarrativeSetting", "Info",
    "HideTemper", "HideSkills", "HideInventory", "HideStory",
    "DisableAlignment", "HideDiceResult", "ShowPrivateMessages", "HidePostStats",
    "CommentariesAccessMode", "IsRemoved"
  ) VALUES (
    game1_id, NOW() - INTERVAL '30 days', NOW() - INTERVAL '25 days', 1, 1,
    false, false, true, testuser_id,
    'Тени Эльдории', 'D&D 5e', 'Dark Fantasy',
    '[b]Добро пожаловать в Эльдорию![/b]

Мрачный мир, где древнее зло пробуждается в забытых руинах.',
    false, false, false, false,
    false, false, false, false,
    1, false
  ) ON CONFLICT ("GameId") DO UPDATE SET "Title" = EXCLUDED."Title";

  -- Game 2: Player_One as Master (TestUser will be player)
  INSERT INTO "Games" (
    "GameId", "CreatedUtc", "ReleaseDate", "Status", "PremoderationStatus",
    "IsFinished", "IsFrozen", "IsRecruitmentOpen", "MasterId",
    "Title", "SystemName", "NarrativeSetting", "Info",
    "HideTemper", "HideSkills", "HideInventory", "HideStory",
    "DisableAlignment", "HideDiceResult", "ShowPrivateMessages", "HidePostStats",
    "CommentariesAccessMode", "IsRemoved"
  ) VALUES (
    game2_id, NOW() - INTERVAL '60 days', NOW() - INTERVAL '55 days', 1, 1,
    false, false, false, player_one_id,
    'Киберпанк 2087: Неоновые Тени', 'Cyberpunk RED', 'Cyberpunk',
    'Добро пожаловать в Найт-Сити будущего.',
    false, false, false, false,
    true, false, false, false,
    1, false
  ) ON CONFLICT ("GameId") DO UPDATE SET "Title" = EXCLUDED."Title";
  RAISE NOTICE '2. Games created';

  -- ============================================
  -- 3. CREATE ROOMS
  -- ============================================
  INSERT INTO "Rooms" ("RoomId", "GameId", "Title", "AccessType", "Type", "OrderNumber", "ViewPrivateText", "ViewDiceResults", "DiceEnabled", "IsRemoved")
  VALUES
    (room1_id, game1_id, 'Пролог: Встреча в таверне', 0, 0, 1.0, false, true, true, false),
    (room2_id, game1_id, 'Глава 1: Дорога в неизвестность', 0, 0, 2.0, false, true, true, false),
    (room3_id, game2_id, 'Акт 1: Первое задание', 0, 0, 1.0, false, true, true, false)
  ON CONFLICT ("RoomId") DO NOTHING;
  RAISE NOTICE '3. Rooms created';

  -- ============================================
  -- 4. CREATE CHARACTERS
  -- ============================================
  -- TestUser character in Game 2 (as player)
  INSERT INTO "Characters" (
    "CharacterId", "GameId", "UserId", "Status", "IsDead", "IsPlayerLeft", "IsPlayerExiled",
    "CreatedUtc", "Name", "Race", "Class", "Appearance", "Temper", "Story",
    "IsNpc", "AccessPolicy", "IsRemoved"
  ) VALUES (
    char1_id, game2_id, testuser_id, 1, false, false, false,
    NOW() - INTERVAL '50 days', 'Джек "Призрак" Морган', 'Человек', 'Нетраннер',
    'Высокий мужчина с кибернетическим глазом.',
    'Холодный и расчётливый.',
    'Бывший корпоративный хакер.',
    false, 0, false
  ) ON CONFLICT ("CharacterId") DO NOTHING;

  -- Player_One character in Game 1
  INSERT INTO "Characters" (
    "CharacterId", "GameId", "UserId", "Status", "IsDead", "IsPlayerLeft", "IsPlayerExiled",
    "CreatedUtc", "Name", "Race", "Class",
    "IsNpc", "AccessPolicy", "IsRemoved"
  ) VALUES (
    char2_id, game1_id, player_one_id, 1, false, false, false,
    NOW() - INTERVAL '20 days', 'Элара Светлая', 'Полуэльф', 'Паладин',
    false, 0, false
  ) ON CONFLICT ("CharacterId") DO NOTHING;
  RAISE NOTICE '4. Characters created';

  -- ============================================
  -- 5. CREATE POSTS
  -- ============================================
  -- Post in Game 1 (TestUser as master)
  INSERT INTO "Posts" ("PostId", "RoomId", "UserId", "CreatedUtc", "Text", "IsRemoved")
  VALUES (
    post1_id, room1_id, testuser_id, NOW() - INTERVAL '20 days',
    '[b]Таверна "Последний приют"[/b]

Дождь барабанит по крыше старой таверны, словно тысячи маленьких молоточков.',
    false
  ) ON CONFLICT ("PostId") DO NOTHING;

  -- Posts in Game 2 (TestUser as player with character)
  INSERT INTO "Posts" ("PostId", "RoomId", "CharacterId", "UserId", "CreatedUtc", "Text", "IsRemoved")
  VALUES
    (post2_id, room3_id, char1_id, testuser_id, NOW() - INTERVAL '45 days',
    'Джек прислонился к холодной стене переулка, наблюдая за входом в клуб через отражение в витрине напротив.',
    false),
    (post3_id, room3_id, char1_id, testuser_id, NOW() - INTERVAL '40 days',
    'Время замедлилось.

Джек видел, как охранник поднимает оружие, как палец ложится на спусковой крючок...

Нейролинк взорвался потоком данных. За долю секунды он взломал местную сеть, отключил освещение.

Темнота. Грохот металла. Вспышки выстрелов вслепую.

Когда свет вернулся, Джек уже был на три этажа выше, дыша тяжело, но живой.

[i]"Вот поэтому я не люблю импровизацию"[/i], — подумал он.',
    false)
  ON CONFLICT ("PostId") DO NOTHING;
  RAISE NOTICE '5. Posts created';

  -- ============================================
  -- 6. CREATE POST REVIEWS (for Best Post rating)
  -- ============================================
  -- TargetType 1 = Post
  INSERT INTO "Reviews" ("ReviewId", "UserId", "TargetType", "TargetId", "CreatedUtc", "SignValue", "ReasonType", "PostAuthorId", "GameId", "IsApproved", "IsRemoved")
  VALUES
    (review1_id, player_one_id, 1, post3_id, NOW() - INTERVAL '39 days', 1, 1, testuser_id, game2_id, true, false),
    (review2_id, moderator_id, 1, post3_id, NOW() - INTERVAL '38 days', 1, 2, testuser_id, game2_id, true, false)
  ON CONFLICT ("ReviewId") DO NOTHING;

  IF player_two_id IS NOT NULL THEN
    INSERT INTO "Reviews" ("ReviewId", "UserId", "TargetType", "TargetId", "CreatedUtc", "SignValue", "ReasonType", "PostAuthorId", "GameId", "IsApproved", "IsRemoved")
    VALUES (review3_id, player_two_id, 1, post3_id, NOW() - INTERVAL '37 days', 1, 1, testuser_id, game2_id, true, false)
    ON CONFLICT ("ReviewId") DO NOTHING;
  END IF;
  RAISE NOTICE '6. Post reviews created';

  -- ============================================
  -- 7. CREATE BLOG
  -- ============================================
  INSERT INTO "Blogs" (
    "BlogId", "OwnerId", "Title", "Description", "CreatedUtc",
    "Status", "PremoderationStatus", "IsPublic", "CommentsEnabled", "PublicationCount", "IsRemoved"
  ) VALUES (
    blog1_id, testuser_id, 'Записки мастера',
    'Мысли о ролевых играх, советы начинающим и разбор интересных механик.',
    NOW() - INTERVAL '100 days',
    1, 1, true, true, 5, false
  ) ON CONFLICT ("BlogId") DO NOTHING;
  RAISE NOTICE '7. Blog created';

  -- ============================================
  -- 8. CREATE LOGIN HISTORY
  -- ============================================
  INSERT INTO "LoginHistories" ("LoginHistoryId", "UserId", "OldLogin", "NewLogin", "ChangedUtc", "ApprovedByUserId")
  VALUES
    (login_hist1_id, testuser_id, 'TestPlayer', 'TestGamer', NOW() - INTERVAL '2 years', moderator_id),
    (login_hist2_id, testuser_id, 'TestGamer', 'TestUser', NOW() - INTERVAL '1 year', moderator_id)
  ON CONFLICT ("LoginHistoryId") DO NOTHING;
  RAISE NOTICE '8. Login history created';

  -- ============================================
  -- 9. CREATE TOPIC AND COMMENT (for Warning)
  -- ============================================
  INSERT INTO "Topics" ("TopicId", "BoardId", "UserId", "CreatedUtc", "Title", "Text", "IsAttached", "IsClosed", "IsRemoved")
  VALUES (
    topic1_id, general_board_id, testuser_id, NOW() - INTERVAL '6 months',
    'Обсуждение правил форума', 'Давайте обсудим новые правила.',
    false, false, false
  ) ON CONFLICT ("TopicId") DO NOTHING;

  INSERT INTO "Comments" ("CommentId", "EntityId", "UserId", "CreatedUtc", "Text", "IsRemoved")
  VALUES (
    comment1_id, topic1_id, testuser_id, NOW() - INTERVAL '6 months',
    'Согласен с предложенными изменениями.',
    false
  ) ON CONFLICT ("CommentId") DO NOTHING;
  RAISE NOTICE '9. Topic and comment created';

  -- ============================================
  -- 10. CREATE WARNING
  -- ============================================
  -- EntityType: 1 = Comment
  INSERT INTO "Warnings" ("WarningId", "UserId", "ModeratorId", "EntityId", "EntityType", "CreatedUtc", "Text", "Points", "IsRemoved")
  VALUES (
    warning1_id, testuser_id, moderator_id, comment1_id, 1, NOW() - INTERVAL '3 months',
    'Некорректная формулировка. Пожалуйста, соблюдайте правила общения.',
    1, false
  ) ON CONFLICT ("WarningId") DO NOTHING;
  RAISE NOTICE '10. Warning created';

  -- ============================================
  -- 11. CREATE EXPIRED BAN
  -- ============================================
  INSERT INTO "Bans" ("BanId", "UserId", "ModeratorId", "StartedUtc", "EndedUtc", "Comment", "AccessRestrictionPolicy", "IsVoluntary", "IsRemoved")
  VALUES (
    ban1_id, testuser_id, moderator_id,
    NOW() - INTERVAL '4 months', NOW() - INTERVAL '3 months' - INTERVAL '15 days',
    'Временный бан за нарушение правил общения.',
    1, false, false
  ) ON CONFLICT ("BanId") DO NOTHING;
  RAISE NOTICE '11. Ban created';

  -- ============================================
  -- 12. CREATE PROFILE NOTE (from another user about TestUser)
  -- ============================================
  INSERT INTO "ProfileNotes" ("NoteId", "OwnerId", "SubjectUserId", "Text", "CreatedUtc")
  VALUES (
    note1_id, player_one_id, testuser_id,
    'Отличный мастер! Очень интересные сюжеты и внимание к деталям. Рекомендую для игры.',
    NOW() - INTERVAL '2 months'
  ) ON CONFLICT ("NoteId") DO NOTHING;
  RAISE NOTICE '12. Profile note created';

  RAISE NOTICE 'All seed data created successfully!';
END $$;
