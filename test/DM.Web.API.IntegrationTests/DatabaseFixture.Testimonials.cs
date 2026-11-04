using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Community;

namespace DM.Web.API.IntegrationTests;

public partial class DatabaseFixture
{
    // (TestimonialId, UserId, Text, DaysAgo)
    private static readonly (Guid TestimonialId, Guid UserId, string Text, int DaysAgo)[] Testimonials =
    [
        (TestConstants.TestTestimonialId, TestConstants.TestUserId,
            "Пришел сюда по рекомендации друга около трех лет назад, и с тех пор это стало моим основным хобби. Особенно нравится, что здесь можно найти игры на любой вкус — от классического фэнтези до киберпанка и постапокалипсиса. Мастера в большинстве своем отзывчивые и готовы помочь новичкам разобраться в правилах. Отдельный плюс — возможность играть в своем темпе, не подстраиваясь под чужое расписание. Конечно, бывают и неудачные игры, но это скорее исключение. В целом — отличное место для тех, кто любит писать и создавать истории вместе с другими людьми.", 120),
        (TestConstants.AdminTestimonialId, TestConstants.AdminUserId,
            "Лучшее место для текстовых ролевых игр!", 90),
        (TestConstants.SecondTestimonialId, TestConstants.SecondUserId,
            "Удобный интерфейс, дружелюбное комьюнити. Нашел здесь много интересных игр и познакомился с классными людьми.", 45),
        (TestConstants.ModeratorTestimonialId, TestConstants.ModeratorUserId,
            "Играю здесь с 2019 года. За это время площадка сильно изменилась в лучшую сторону — новый дизайн, удобные уведомления, быстрый поиск игр. Модерация работает оперативно, конфликты решаются справедливо. Единственное, чего не хватает — мобильного приложения, но и веб-версия на телефоне работает нормально. Рекомендую всем, кто хочет попробовать форумные ролевые игры.", 15),
        (TestConstants.MentorTestimonialId, TestConstants.MentorUserId,
            "Отличная площадка для новичков. Всегда рады помочь!", 60),
        (TestConstants.SeniorModeratorTestimonialId, TestConstants.SeniorModeratorUserId,
            "Начинал здесь как обычный игрок много лет назад. Тогда площадка была совсем другой — простенький форум, несколько десятков активных пользователей, пара сотен игр. Сейчас это полноценное сообщество с тысячами участников, продуманной системой тегов, удобным редактором постов и множеством других функций. Приятно осознавать, что внес свой вклад в развитие проекта. Здесь я нашел не только увлечение, но и настоящих друзей, с некоторыми из которых общаюсь уже вне площадки. Если вы любите писать, придумывать персонажей и погружаться в интересные сюжеты — вам точно сюда.", 180),
        (TestConstants.AdditionalTestimonialIds[0], TestConstants.TestimonialUserIds[0],
            "Зашел случайно, остался навсегда. Атмосфера здесь особенная — люди действительно любят то, чем занимаются.", 7),
        (TestConstants.AdditionalTestimonialIds[1], TestConstants.TestimonialUserIds[1],
            "Долго искал площадку для словесок, и DM.am оказался именно тем, что нужно. Простой интерфейс, понятные правила, живое сообщество. Особенно радует, что можно найти игры практически в любом жанре — от классического фэнтези до современных детективов. Мастера отзывчивые, всегда готовы объяснить непонятные моменты. Единственный минус — иногда сложно выбрать, в какую игру вступить, потому что интересных слишком много!", 25),
        (TestConstants.AdditionalTestimonialIds[2], TestConstants.TestimonialUserIds[2],
            "Отличное место для творческих людей. Рекомендую!", 35),
        (TestConstants.AdditionalTestimonialIds[3], TestConstants.TestimonialUserIds[3],
            "Площадка, которая объединяет людей с похожими интересами. Здесь я нашла не просто игры, а настоящее хобби, которое помогает отвлечься от рутины и погрузиться в увлекательные истории. Нравится, что каждый может найти что-то свое — есть игры с акцентом на боевку, есть чисто отыгрышевые, есть смешанные. Модерация адекватная, правила понятные.", 52),
        (TestConstants.AdditionalTestimonialIds[4], TestConstants.TestimonialUserIds[4],
            "Супер! Всем советую!", 70),
        (TestConstants.AdditionalTestimonialIds[5], TestConstants.TestimonialUserIds[5],
            "Пишу здесь уже пять лет. За это время сменилось несколько версий сайта, но атмосфера осталась прежней — дружелюбной и творческой. Особенно ценю возможность участвовать в нескольких играх одновременно и писать в удобное время. Форумный формат идеально подходит для тех, кто любит продумывать каждую реплику своего персонажа.", 85),
        (TestConstants.AdditionalTestimonialIds[6], TestConstants.TestimonialUserIds[6],
            "Лучшая площадка для форумных ролевых игр в рунете. Без преувеличения.", 110),
        (TestConstants.AdditionalTestimonialIds[7], TestConstants.TestimonialUserIds[7],
            "Классное комьюнити, интересные игры, удобный сайт. Что еще нужно?", 140),
        (TestConstants.AdditionalTestimonialIds[8], TestConstants.TestimonialUserIds[8],
            "Пришла сюда по рекомендации подруги и не пожалела. Первые дни было немного страшно — все-таки новое место, незнакомые люди. Но оказалось, что здесь очень приветливо относятся к новичкам. Помогли разобраться с правилами, подсказали хорошие игры для старта. Сейчас у меня уже три активных персонажа в разных играх, и я планирую создать свою собственную игру в ближайшее время.", 165),
        (TestConstants.AdditionalTestimonialIds[9], TestConstants.TestimonialUserIds[9],
            "Годы идут, а DM.am остается любимым местом для творчества.", 200),
        (TestConstants.AdditionalTestimonialIds[10], TestConstants.TestimonialUserIds[10],
            "Хороший сайт для тех, кто любит писать.", 230),
        (TestConstants.AdditionalTestimonialIds[11], TestConstants.TestimonialUserIds[11],
            "Не ожидал, что форумные игры могут быть настолько затягивающими! Начал полгода назад с небольшого персонажа в фэнтези-игре, а теперь уже сам вожу две игры и участвую еще в трех. Формат постовой игры отлично подходит для работающих людей — можно писать когда удобно, не нужно подстраиваться под чужое расписание. Сообщество отзывчивое, всегда можно найти партнеров для игры или просто пообщаться на форуме.", 260),
        (TestConstants.AdditionalTestimonialIds[12], TestConstants.TestimonialUserIds[12],
            "10/10, рекомендую всем любителям ролевых игр!", 300),
        (TestConstants.AdditionalTestimonialIds[13], TestConstants.TestimonialUserIds[13],
            "Площадка, на которой хочется оставаться. Здесь собрались действительно увлеченные люди, которые создают потрясающие истории. Каждая игра — это маленький мир со своими правилами и атмосферой. Очень нравится система тегов, которая помогает быстро найти игру по интересам. Отдельное спасибо разработчикам за удобный редактор постов с поддержкой форматирования.", 350),
        (TestConstants.AdditionalTestimonialIds[14], TestConstants.TestimonialUserIds[14],
            "DM — это как таверна на перекрестке миров. Заходишь случайно, а остаешься навсегда.", 5),
        (TestConstants.AdditionalTestimonialIds[15], TestConstants.TestimonialUserIds[15],
            "Когда создаешь очередного персонажа, невольно вкладываешь в него частицу себя. И это прекрасно — здесь можно быть кем угодно, исследовать грани своей личности через призму выдуманных историй. За годы на площадке я создала десятки персонажей, и каждый из них научил меня чему-то новому о себе.", 42),
        (TestConstants.AdditionalTestimonialIds[16], TestConstants.TestimonialUserIds[16],
            "DM — это Сила и Мощь!", 380),
        (TestConstants.AdditionalTestimonialIds[17], TestConstants.TestimonialUserIds[17],
            "Писать здесь — как дышать. Без этого уже не представляю свою жизнь. Форумные игры дают то, чего не дают книги и фильмы — ты не просто наблюдатель, а активный участник истории. Каждый твой выбор влияет на сюжет, каждое слово имеет вес. И когда история получается по-настоящему захватывающей — это непередаваемое чувство.", 18),
        (TestConstants.AdditionalTestimonialIds[18], TestConstants.TestimonialUserIds[18],
            "Место, где рождаются легенды.", 95),
        (TestConstants.AdditionalTestimonialIds[19], TestConstants.TestimonialUserIds[19],
            "Раньше считал, что форумные игры — это что-то несерьезное. Попробовал ради интереса и понял, как сильно ошибался. Здесь собрались талантливые авторы, которые создают тексты уровня профессиональных писателей. Некоторые игровые посты читаются как главы хорошего романа. Спасибо всем, кто делает эту площадку такой особенной.", 155),
        (TestConstants.AdditionalTestimonialIds[20], TestConstants.TestimonialUserIds[20],
            "Идеальное место для творческих людей. Если вы любите писать — вам точно сюда.", 270),
        (TestConstants.AdditionalTestimonialIds[21], TestConstants.TestimonialUserIds[21],
            "Пять звезд из пяти! Без вариантов.", 320),
        (TestConstants.AdditionalTestimonialIds[22], TestConstants.TestimonialUserIds[22],
            "История о том, как я искал площадку для словесных ролевых игр, могла бы занять отдельный пост. Перепробовал все — форумы, дискорд-серверы, специализированные сайты. И только здесь нашел то, что искал: удобный интерфейс, активное сообщество, разнообразие жанров и адекватную модерацию. Уже два года здесь, и уходить не планирую.", 78),
        (TestConstants.AdditionalTestimonialIds[23], TestConstants.TestimonialUserIds[23],
            "Здесь каждый найдет что-то свое.", 400),
        (TestConstants.AdditionalTestimonialIds[24], TestConstants.TestimonialUserIds[24],
            "Лучшая площадка для форумных ролевых игр. Проверено годами.", 450),
        (TestConstants.AdditionalTestimonialIds[25], TestConstants.TestimonialUserIds[25],
            "Сначала пришла просто посмотреть, что это такое. Потом создала пробного персонажа. Потом еще одного. И еще. Сейчас у меня восемь активных персонажей в разных играх, и я веду свою собственную игру по авторскому миру. Эта площадка захватывает — в хорошем смысле. Здесь можно реализовать любые творческие идеи и найти единомышленников.", 130),
        (TestConstants.AdditionalTestimonialIds[26], TestConstants.TestimonialUserIds[26],
            "Годный сайт, рекомендую!", 500),
        (TestConstants.AdditionalTestimonialIds[27], TestConstants.TestimonialUserIds[27],
            "DM научил меня писать. Серьезно. Когда я только пришел сюда, мои посты были короткими и корявыми. Но здесь окружение такое, что хочется стараться, хочется писать лучше. Читаешь посты других игроков и вдохновляешься. Сейчас, спустя несколько лет, я пишу совсем на другом уровне — и это заслуга не только моя, но и этого сообщества.", 210),
    ];

    private static void SeedTestimonials(DmDbContext db)
    {
        if (db.WebsiteTestimonials.Any(t => t.WebsiteTestimonialId == TestConstants.TestTestimonialId)) return;

        db.WebsiteTestimonials.AddRange(Testimonials.Select(t => new WebsiteTestimonial
        {
            WebsiteTestimonialId = t.TestimonialId,
            AuthorId = t.UserId,
            Text = t.Text,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-t.DaysAgo),
            IsRemoved = false
        }));
    }
}
