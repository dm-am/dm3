namespace DM.Domain.Core.Enums;

/// <summary>
/// Метрика, по которой оценивается прогресс достижения.
/// Каждое значение однозначно отображается на одно поле в профиле
/// пользователя (см. <c>AchievementMetricResolver</c> в Domain.Community).
/// Новая метрика = новое значение enum + новый case в резолвере + (при
/// необходимости) новое денормализованное поле на <c>GeneralUser</c> +
/// парный case в FE <c>getMetricValue</c>.
/// </summary>
public enum AchievementMetric
{
    /// <summary>Кол-во игровых постов (<c>QuantityRating</c>).</summary>
    GamePostsAuthored = 1,

    /// <summary>Дней с регистрации (now − <c>RegisteredUtc</c>).</summary>
    DaysSinceRegistration = 2,

    /// <summary>Сумма очков рейтинга от полученных PostReviews (<c>QualityRating</c>).</summary>
    PostReviewScoreSum = 3,

    /// <summary>Игр, где пользователь — мастер или ассистент (<c>GamesHosting</c>).</summary>
    GamesHosted = 4,

    /// <summary>Игр, где пользователь — игрок (<c>GamesPlaying</c>).</summary>
    GamesPlayed = 5,

    /// <summary>Блогов, где пользователь — автор или ассистент (<c>BlogsHosting</c>).</summary>
    BlogsHosted = 6,

    /// <summary>Кол-во форумных топиков, созданных пользователем.</summary>
    TopicsAuthored = 7,

    /// <summary>Кол-во оставленных комментариев (любого типа).</summary>
    CommentsAuthored = 8,

    /// <summary>Кол-во сообщений в глобальном чате.</summary>
    GlobalChatMessages = 9,

    /// <summary>Кол-во полученных банов (count(Bans) where TargetUser=user).</summary>
    BansReceived = 10,

    /// <summary>
    /// Кол-во дропов — игр, которые пользователь покинул добровольно
    /// (count(Characters) where AuthorId=user AND Status=Retired AND IsPlayerLeft=true).
    /// Exile и death — не дропы, это инициатива GM или ин-гейм событие.
    /// </summary>
    GameDrops = 11,

    /// <summary>
    /// Кол-во публикаций — статей в блогах, написанных пользователем
    /// (count(Publications) where AuthorId=user AND !IsRemoved).
    /// Драфты (IsPublished=false) тоже считаются — пользователь сделал
    /// работу даже если не нажал «Опубликовать».
    /// </summary>
    PublicationsAuthored = 12,

    /// <summary>
    /// Суммарное число «лайков», полученных пользователем на его
    /// контент: топики, публикации, комментарии, чат-сообщения. Игровые
    /// посты (с рейтингом через PostReviews) и сами PostReview-ы не
    /// включены — для постов есть отдельная цепочка «Рейтинг».
    /// Soft-deleted лайки и удаленный контент исключены.
    /// </summary>
    LikesReceived = 13,
}
